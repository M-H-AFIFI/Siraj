using Siraj.Core.Adhan;
using Siraj.Core.Places;

namespace Siraj.Core.Prayer;

public enum PrayerType { Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha }

public sealed record PrayerTime(PrayerType Type, DateTimeOffset Time, bool IsNext = false, DateTimeOffset? Iqama = null)
{
    public DateTimeOffset WindowEnd => (Iqama ?? Time.AddMinutes(20)).AddMinutes(15);
}

public sealed record PrayerDay(DateOnly Date, IReadOnlyList<PrayerTime> Times, Place Place)
{
    public PrayerTime this[PrayerType type] => Times.First(t => t.Type == type);

    public PrayerTime? Next => Times.FirstOrDefault(t => t.IsNext);
}

public sealed record CalculationSettings(
    string Method = "MuslimWorldLeague",
    string Madhab = "Shafi",
    string HighLatitudeRule = "MiddleOfTheNight",
    IReadOnlyDictionary<string, int>? MinuteOffsets = null);

public static class PrayerCalculator
{
    public static int IqamaMinutes(PrayerType type) => type switch
    {
        PrayerType.Fajr => 25,
        PrayerType.Dhuhr => 20,
        PrayerType.Asr => 20,
        PrayerType.Maghrib => 10,
        PrayerType.Isha => 20,
        _ => 0
    };

    public static CalculationParameters Parameters(CalculationSettings settings)
    {
        var parameters = settings.Method switch
        {
            "MWL" or "MuslimWorldLeague" => CalculationParameters.For(CalculationMethod.MuslimWorldLeague),
            "ISNA" or "NorthAmerica" => CalculationParameters.For(CalculationMethod.NorthAmerica),
            "Egypt" or "Egyptian" => CalculationParameters.For(CalculationMethod.Egyptian),
            "Karachi" => CalculationParameters.For(CalculationMethod.Karachi),
            "UmmAlQura" => CalculationParameters.For(CalculationMethod.UmmAlQura),
            "Dubai" => CalculationParameters.For(CalculationMethod.Dubai),
            "Moonsighting" or "MoonsightingCommittee" => CalculationParameters.For(CalculationMethod.MoonSightingCommittee),
            "Singapore" => CalculationParameters.For(CalculationMethod.Singapore),
            "Kuwait" => CalculationParameters.For(CalculationMethod.Kuwait),
            "Qatar" => CalculationParameters.For(CalculationMethod.Qatar),
            "Other" => CalculationParameters.For(CalculationMethod.Other),
            "Turkey" or "Diyanet" => new CalculationParameters(18.0, 17.0, CalculationMethod.Other)
                .WithMethodAdjustments(new PrayerAdjustments(0, -7, 5, 4, 7, 0)),
            // Uses standard sunset for Maghrib; CalculationParameters does not model a Maghrib angle.
            "Tehran" => new CalculationParameters(17.7, 14.0, CalculationMethod.Other),
            _ => CalculationParameters.For(CalculationMethod.MuslimWorldLeague)
        };
        parameters.Madhab = settings.Madhab == "Hanafi" ? Madhab.Hanafi : Madhab.Shafi;
        parameters.HighLatitudeRule = settings.HighLatitudeRule switch
        {
            "SeventhOfTheNight" => HighLatitudeRule.SeventhOfTheNight,
            "TwilightAngle" or "AngleBased" => HighLatitudeRule.TwilightAngle,
            _ => HighLatitudeRule.MiddleOfTheNight
        };
        foreach (var (prayer, offset) in settings.MinuteOffsets ?? new Dictionary<string, int>())
        {
            switch (prayer.ToUpperInvariant())
            {
                case "FAJR": parameters.Adjustments.Fajr = offset; break;
                case "DHUHR": parameters.Adjustments.Dhuhr = offset; break;
                case "ASR": parameters.Adjustments.Asr = offset; break;
                case "MAGHRIB": parameters.Adjustments.Maghrib = offset; break;
                case "ISHA": parameters.Adjustments.Isha = offset; break;
            }
        }
        return parameters;
    }

    public static IReadOnlyList<PrayerTime> Raw(Place place, DateOnly date, CalculationSettings settings)
    {
        var parameters = Parameters(settings);
        var times = new PrayerTimes(new Coordinates(place.Latitude, place.Longitude), date, parameters);
        var zone = place.Zone;

        PrayerTime Make(PrayerType type, DateTime? utc, int fallbackHour)
        {
            var time = utc is { } u
                ? TimeZoneInfo.ConvertTime(new DateTimeOffset(u, TimeSpan.Zero), zone)
                : Zones.AtLocal(date, new TimeOnly(fallbackHour, 0), zone);
            int iqama = IqamaMinutes(type);
            return new PrayerTime(type, time, false, iqama > 0 ? time.AddMinutes(iqama) : null);
        }

        return
        [
            Make(PrayerType.Fajr, times.Fajr, 5),
            Make(PrayerType.Sunrise, times.Sunrise, 6),
            Make(PrayerType.Dhuhr, times.Dhuhr, 12),
            Make(PrayerType.Asr, times.Asr, 15),
            Make(PrayerType.Maghrib, times.Maghrib, 18),
            Make(PrayerType.Isha, times.Isha, 20),
        ];
    }

    public static PrayerDay Day(Place place, DateOnly date, CalculationSettings settings, DateTimeOffset now)
    {
        var raw = Raw(place, date, settings);
        int nextIndex = -1;
        for (int i = 0; i < raw.Count; i++)
        {
            var p = raw[i];
            var activeEnd = p.Type == PrayerType.Sunrise ? p.Time.AddMinutes(15) : p.WindowEnd;
            if (p.Time > now || now < activeEnd)
            {
                nextIndex = i;
                break;
            }
        }

        if (nextIndex != -1)
            return new PrayerDay(date, raw.Select((p, i) => p with { IsNext = i == nextIndex }).ToList(), place);

        var tomorrowFajr = Raw(place, date.AddDays(1), settings)[0];
        return new PrayerDay(date,
            raw.Select(p => p.Type == PrayerType.Fajr ? tomorrowFajr with { IsNext = true } : p with { IsNext = false }).ToList(),
            place);
    }

    public static PrayerDay Today(Place place, CalculationSettings settings, DateTimeOffset now) =>
        Day(place, place.Today(now), settings, now);
}
