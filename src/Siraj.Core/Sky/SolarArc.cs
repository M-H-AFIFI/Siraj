using Siraj.Core.Prayer;

namespace Siraj.Core.Sky;

public sealed record ArcMilestone(PrayerType? Type, float Progress, bool IsPast, bool IsNext);

public sealed record SolarArc(float Progress, bool IsDaytime, string PhaseKey, IReadOnlyList<ArcMilestone> Milestones)
{
    public const float SunriseOnArc = 0.18f;
    public const float SunsetOnArc = 0.84f;

    public static SolarArc At(DateTimeOffset now, IReadOnlyList<PrayerTime> prayers, PrayerTime? next)
    {
        DateTimeOffset? Find(PrayerType t) => prayers.FirstOrDefault(p => p.Type == t)?.Time;
        DateTimeOffset At(int h, int m) => new(now.Date.AddHours(h).AddMinutes(m), now.Offset);
        var sunrise = Find(PrayerType.Sunrise) ?? At(6, 0);
        var dhuhr = Find(PrayerType.Dhuhr) ?? At(12, 30);
        var asr = Find(PrayerType.Asr) ?? At(16, 0);
        var maghrib = Find(PrayerType.Maghrib) ?? At(18, 30);
        var fajr = Find(PrayerType.Fajr) ?? sunrise.AddMinutes(-90);
        var isha = Find(PrayerType.Isha) ?? maghrib.AddMinutes(90);
        // After Isha, the prayer list carries tomorrow's Fajr; shift back a day for today's arc.
        if (fajr > sunrise) fajr = fajr.AddDays(-1);

        bool IsNext(PrayerType type) => next != null
            ? next.Type == type
            : prayers.Where(p => p.Time > now).MinBy(p => p.Time)?.Type == type;

        static float Segment(DateTimeOffset now, DateTimeOffset from, DateTimeOffset to, float pFrom, float pTo)
        {
            double total = Math.Max(1, (to - from).TotalMilliseconds);
            double elapsed = Math.Max(0, (now - from).TotalMilliseconds);
            return pFrom + (float)Math.Clamp(elapsed / total, 0, 1) * (pTo - pFrom);
        }

        bool daytime = now >= sunrise && now < maghrib;
        if (daytime)
        {
            const float pFajr = 0.08f, pDhuhr = 0.50f, pIsha = 0.92f;
            float asrRatio = asr > dhuhr && maghrib > asr
                ? (float)Math.Clamp((asr - dhuhr).TotalMilliseconds / Math.Max(1, (maghrib - dhuhr).TotalMilliseconds), 0.35, 0.65)
                : 0.52f;
            float pAsr = Math.Clamp(pDhuhr + asrRatio * (SunsetOnArc - pDhuhr), 0.62f, 0.74f);
            float progress = now < dhuhr ? Segment(now, sunrise, dhuhr, SunriseOnArc, pDhuhr)
                : now < asr ? Segment(now, dhuhr, asr, pDhuhr, pAsr)
                : Segment(now, asr, maghrib, pAsr, SunsetOnArc);

            string phase;
            if (now < dhuhr)
            {
                double factor = Math.Clamp((now - sunrise).TotalMilliseconds / Math.Max(1, (dhuhr - sunrise).TotalMilliseconds), 0, 1);
                phase = factor < 0.15 ? "phase_sunrise" : factor > 0.88 ? "phase_solar_noon" : "phase_duha";
            }
            else
            {
                phase = now > maghrib.AddMinutes(-35) ? "phase_golden_sunset"
                    : now >= asr ? "phase_asr"
                    : (now - dhuhr).TotalMinutes < 45 ? "phase_solar_noon"
                    : "phase_dhuhr";
            }

            return new SolarArc(Math.Clamp(progress, 0.08f, 0.92f), true, phase,
            [
                new(PrayerType.Fajr, pFajr, now > fajr, IsNext(PrayerType.Fajr)),
                new(PrayerType.Sunrise, SunriseOnArc, now > sunrise, IsNext(PrayerType.Sunrise)),
                new(PrayerType.Dhuhr, pDhuhr, now > dhuhr, IsNext(PrayerType.Dhuhr)),
                new(PrayerType.Asr, pAsr, now > asr, IsNext(PrayerType.Asr)),
                new(PrayerType.Maghrib, SunsetOnArc, now > maghrib, IsNext(PrayerType.Maghrib)),
                new(PrayerType.Isha, pIsha, now > isha, IsNext(PrayerType.Isha)),
            ]);
        }

        // Night arc runs from previous Maghrib to next Fajr.
        bool dawnWindow = now >= fajr && now < sunrise;
        var nextFajr = fajr > now || dawnWindow ? fajr : fajr.AddDays(1);
        var prevMaghrib = maghrib > now ? maghrib.AddDays(-1) : maghrib;
        double night = Math.Max(1, (nextFajr - prevMaghrib).TotalMilliseconds);
        var midnight = prevMaghrib.AddMilliseconds(night / 2);
        var tahajjud = prevMaghrib.AddMilliseconds(night * 2 / 3);
        var nightIsha = now < sunrise ? isha.AddDays(-1) : isha;
        if (nightIsha < prevMaghrib) nightIsha = nightIsha.AddDays(1);

        const float pMaghribNight = 0.08f, pMidnight = 0.50f, pTahajjud = 0.72f, pFajrNight = 0.92f;
        float ishaRatio = nightIsha > prevMaghrib && midnight > nightIsha
            ? (float)Math.Clamp((nightIsha - prevMaghrib).TotalMilliseconds / Math.Max(1, (midnight - prevMaghrib).TotalMilliseconds), 0.20, 0.50)
            : 0.35f;
        float pIshaNight = Math.Clamp(pMaghribNight + ishaRatio * (pMidnight - pMaghribNight), 0.18f, 0.28f);

        float nightProgress = now < nightIsha ? Segment(now, prevMaghrib, nightIsha, pMaghribNight, pIshaNight)
            : now < midnight ? Segment(now, nightIsha, midnight, pIshaNight, pMidnight)
            : now < tahajjud ? Segment(now, midnight, tahajjud, pMidnight, pTahajjud)
            : now < nextFajr ? Segment(now, tahajjud, nextFajr, pTahajjud, pFajrNight)
            : pFajrNight;

        float nightFactor = (float)Math.Clamp((now - prevMaghrib).TotalMilliseconds / night, 0, 1);
        string nightPhase = dawnWindow ? "phase_true_dawn"
            : nightFactor > 0.92f && now < nextFajr ? "phase_sahur"
            : nightFactor is >= 0.66f and <= 0.92f ? "phase_last_third"
            : nightFactor is >= 0.45f and <= 0.66f ? "phase_midnight"
            : now < nightIsha ? "phase_maghrib"
            : "phase_isha";

        bool isFajrNext = IsNext(PrayerType.Fajr);
        bool isIshaNext = IsNext(PrayerType.Isha);
        bool isMaghribNext = IsNext(PrayerType.Maghrib);

        return new SolarArc(Math.Clamp(nightProgress, 0.08f, 0.92f), false, nightPhase,
        [
            new(PrayerType.Maghrib, pMaghribNight, now > prevMaghrib, isMaghribNext),
            new(PrayerType.Isha, pIshaNight, now > nightIsha, isIshaNext),
            new(null, pMidnight, now > midnight, false),
            new(null, pTahajjud, now > tahajjud, false),
            new(PrayerType.Fajr, pFajrNight, now > nextFajr, isFajrNext || (!isIshaNext && !isMaghribNext)),
        ]);
    }
}
