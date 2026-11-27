using Siraj.Core.Prayer;

namespace Siraj.Core.Sky;

public readonly record struct Argb(byte A, byte R, byte G, byte B)
{
    public static Argb Hex(uint argb) => new((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    public Argb WithAlpha(float alpha) => this with { A = (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255) };

    public float Alpha => A / 255f;

    public static Argb Lerp(Argb a, Argb b, float f) => new(
        (byte)Math.Round(a.A + (b.A - a.A) * f), (byte)Math.Round(a.R + (b.R - a.R) * f),
        (byte)Math.Round(a.G + (b.G - a.G) * f), (byte)Math.Round(a.B + (b.B - a.B) * f));

    public double Luminance
    {
        get
        {
            static double Linear(byte v)
            {
                double s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Linear(R) + 0.7152 * Linear(G) + 0.0722 * Linear(B);
        }
    }
}

public sealed record SkyColors(
    IReadOnlyList<Argb> Sky,
    IReadOnlyList<Argb> Arc,
    Argb OrbGlow,
    Argb OrbCore,
    Argb CardBorderTop,
    Argb CardBorderBottom,
    Argb FooterBg,
    Argb CardBg,
    bool ShowCrescent,
    bool IsWarmGlow,
    Argb CloudLight,
    Argb CloudDark,
    float CloudAlpha,
    float StarAlpha,
    float RayAlpha);

public sealed record SkyTwilight(
    float FalseDawn = 0, float TrueDawn = 0, float SunriseGlow = 0, float SunsetGlow = 0,
    float Shafaq = 0, float MilkyWay = 0, float Birds = 0, float Mist = 0);

public sealed record SkyMoment(SkyColors Colors, SkyTwilight Twilight);

public static class SkyTimeline
{
    private const int SkyStops = 6;
    private const int ArcStops = 3;
    private const float MiddayBirds = 0.4f;

    public static SkyColors NightColors => Palettes.IshaNightfall;

    public static SkyColors SkyForPrayer(PrayerType prayer) => prayer switch
    {
        PrayerType.Fajr => Palettes.FajrSubhSadiq,
        PrayerType.Sunrise => Palettes.SunriseGolden,
        PrayerType.Dhuhr => Palettes.DhuhrZenith,
        PrayerType.Asr => Palettes.Afternoon,
        PrayerType.Maghrib => Palettes.MaghribDusk,
        _ => Palettes.IshaNightfall,
    };

    public static SkyMoment At(DateTimeOffset now, IReadOnlyList<PrayerTime> prayers)
    {
        var day = DayTimes.From(now, prayers);
        var keyframes = new List<(DateTimeOffset At, SkyColors Colors)>(48);
        for (int shift = -1; shift <= 1; shift++) keyframes.AddRange(KeyframesFor(day.Shifted(shift)));
        var ordered = new List<(DateTimeOffset At, SkyColors Colors)>(keyframes.Count);
        foreach (var frame in keyframes.OrderBy(k => k.At))
        {
            if (ordered.Count == 0 || frame.At > ordered[^1].At) ordered.Add(frame);
        }
        int after = ordered.FindIndex(k => k.At > now);
        SkyColors colors;
        if (after <= 0)
        {
            colors = ordered.Count > 0 ? ordered[0].Colors : Palettes.Isha;
        }
        else
        {
            var (from, a) = ordered[after - 1];
            var (to, b) = ordered[after];
            double span = (to - from).TotalMilliseconds;
            float f = span <= 0 ? 1f : (float)Math.Clamp((now - from).TotalMilliseconds / span, 0, 1);
            colors = Blend(a, b, Smooth(f));
        }
        return new SkyMoment(colors, TwilightAt(now, day));
    }

    private static IEnumerable<(DateTimeOffset, SkyColors)> KeyframesFor(DayTimes d)
    {
        var night = d.NextFajr - d.Maghrib;
        var midnight = d.Maghrib + night / 2;
        var lastThird = d.Maghrib + night * 2 / 3;
        var goldenEnd = Min(d.Sunrise.AddMinutes(50), Mid(d.Sunrise, d.Dhuhr));
        return
        [
            (d.Fajr.AddMinutes(-50), Palettes.PreDawn),
            (d.Fajr, Palettes.Fajr),
            (d.Sunrise.AddMinutes(-18), Palettes.BlueHourDawn),
            (d.Sunrise, Palettes.Sunrise),
            (goldenEnd, Palettes.Morning),
            (d.Dhuhr, Palettes.Noon),
            (d.Asr, Palettes.Afternoon),
            (Max(d.Maghrib.AddMinutes(-55), Mid(d.Asr, d.Maghrib)), Palettes.GoldenHour),
            (d.Maghrib.AddMinutes(-12), Palettes.Sunset),
            (d.Maghrib.AddMinutes(4), Palettes.Afterglow),
            (d.Isha, Palettes.Isha),
            (midnight, Palettes.Midnight),
            (lastThird, Palettes.LastThird),
        ];
    }

    private static SkyTwilight TwilightAt(DateTimeOffset now, DayTimes today)
    {
        float falseDawn = 0, trueDawn = 0, sunriseGlow = 0, sunsetGlow = 0, shafaq = 0, milkyWay = 0, birds = 0, mist = 0;
        for (int shift = -1; shift <= 1; shift++)
        {
            var d = today.Shifted(shift);
            falseDawn = Math.Max(falseDawn, Tent(now, d.Fajr.AddMinutes(-70), d.Fajr.AddMinutes(-20), d.Fajr.AddMinutes(2)));
            trueDawn = Math.Max(trueDawn, Plateau(now, d.Fajr.AddMinutes(-4), d.Sunrise.AddMinutes(-28), d.Sunrise.AddMinutes(-6), d.Sunrise.AddMinutes(14)));
            sunriseGlow = Math.Max(sunriseGlow, Tent(now, d.Sunrise.AddMinutes(-22), d.Sunrise.AddMinutes(6), d.Sunrise.AddMinutes(55)));
            sunsetGlow = Math.Max(sunsetGlow, Tent(now, d.Maghrib.AddMinutes(-70), d.Maghrib.AddMinutes(-6), d.Maghrib.AddMinutes(16)));
            shafaq = Math.Max(shafaq, Tent(now, d.Maghrib.AddMinutes(-6), d.Maghrib.AddMinutes(6), d.Isha));
            milkyWay = Math.Max(milkyWay, Plateau(now, d.Isha.AddMinutes(35), d.Isha.AddMinutes(95), d.NextFajr.AddMinutes(-70), d.NextFajr.AddMinutes(-30)));
            birds = Math.Max(birds, Math.Max(
                Plateau(now, d.Sunrise.AddMinutes(-5), d.Sunrise.AddMinutes(15), d.Sunrise.AddMinutes(100), d.Sunrise.AddMinutes(150)),
                Math.Max(
                    Plateau(now, d.Maghrib.AddMinutes(-110), d.Maghrib.AddMinutes(-70), d.Maghrib.AddMinutes(-4), d.Maghrib.AddMinutes(20)),
                    MiddayBirds * Plateau(now, d.Sunrise.AddMinutes(60), d.Sunrise.AddMinutes(150), d.Maghrib.AddMinutes(-110), d.Maghrib.AddMinutes(-60)))));
            mist = Math.Max(mist, Plateau(now, d.Fajr.AddMinutes(5), d.Sunrise.AddMinutes(-25), d.Sunrise.AddMinutes(20), d.Sunrise.AddMinutes(75)));
        }
        return new SkyTwilight(falseDawn, trueDawn, sunriseGlow, sunsetGlow, shafaq, milkyWay, birds, mist);
    }

    private static SkyColors Blend(SkyColors a, SkyColors b, float f)
    {
        var near = f < 0.5f ? a : b;
        return new SkyColors(
            Sky: Enumerable.Range(0, SkyStops).Select(i => Argb.Lerp(Stop(a.Sky, i, SkyStops), Stop(b.Sky, i, SkyStops), f)).ToArray(),
            Arc: Enumerable.Range(0, ArcStops).Select(i => Argb.Lerp(Stop(a.Arc, i, ArcStops), Stop(b.Arc, i, ArcStops), f)).ToArray(),
            OrbGlow: Argb.Lerp(a.OrbGlow, b.OrbGlow, f),
            OrbCore: Argb.Lerp(a.OrbCore, b.OrbCore, f),
            CardBorderTop: Argb.Lerp(a.CardBorderTop, b.CardBorderTop, f),
            CardBorderBottom: Argb.Lerp(a.CardBorderBottom, b.CardBorderBottom, f),
            FooterBg: Argb.Lerp(a.FooterBg, b.FooterBg, f),
            CardBg: Argb.Lerp(a.CardBg, b.CardBg, f),
            ShowCrescent: near.ShowCrescent,
            IsWarmGlow: near.IsWarmGlow,
            CloudLight: Argb.Lerp(a.CloudLight, b.CloudLight, f),
            CloudDark: Argb.Lerp(a.CloudDark, b.CloudDark, f),
            CloudAlpha: a.CloudAlpha + (b.CloudAlpha - a.CloudAlpha) * f,
            StarAlpha: a.StarAlpha + (b.StarAlpha - a.StarAlpha) * f,
            RayAlpha: a.RayAlpha + (b.RayAlpha - a.RayAlpha) * f);
    }

    public static Argb Stop(IReadOnlyList<Argb> colors, int i, int count)
    {
        if (colors.Count == 1) return colors[0];
        float at = (float)i / (count - 1) * (colors.Count - 1);
        int lo = Math.Clamp((int)at, 0, colors.Count - 1);
        int hi = Math.Min(lo + 1, colors.Count - 1);
        return Argb.Lerp(colors[lo], colors[hi], at - lo);
    }

    private static float Smooth(float f) => f * f * (3f - 2f * f);

    private static float Fraction(DateTimeOffset now, DateTimeOffset from, DateTimeOffset to)
    {
        double span = (to - from).TotalMilliseconds;
        if (span <= 0) return now < to ? 0f : 1f;
        return (float)Math.Clamp((now - from).TotalMilliseconds / span, 0, 1);
    }

    private static float Tent(DateTimeOffset now, DateTimeOffset start, DateTimeOffset peak, DateTimeOffset end)
    {
        if (now < start || now >= end) return 0f;
        return now < peak ? Smooth(Fraction(now, start, peak)) : 1f - Smooth(Fraction(now, peak, end));
    }

    private static float Plateau(DateTimeOffset now, DateTimeOffset start, DateTimeOffset full, DateTimeOffset fade, DateTimeOffset end)
    {
        if (now < start || now >= end) return 0f;
        if (now < full) return Smooth(Fraction(now, start, full));
        if (now < fade) return 1f;
        return 1f - Smooth(Fraction(now, fade, end));
    }

    private static DateTimeOffset Mid(DateTimeOffset a, DateTimeOffset b) => a + (b - a) / 2;
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    internal sealed record DayTimes(DateTimeOffset Fajr, DateTimeOffset Sunrise, DateTimeOffset Dhuhr, DateTimeOffset Asr, DateTimeOffset Maghrib, DateTimeOffset Isha)
    {
        public DateTimeOffset NextFajr => Fajr.AddDays(1);

        public DayTimes Shifted(int days) => days == 0 ? this :
            new DayTimes(Fajr.AddDays(days), Sunrise.AddDays(days), Dhuhr.AddDays(days), Asr.AddDays(days), Maghrib.AddDays(days), Isha.AddDays(days));

        public static DayTimes From(DateTimeOffset now, IReadOnlyList<PrayerTime> prayers)
        {
            DateTimeOffset? Find(PrayerType t) => prayers.FirstOrDefault(p => p.Type == t)?.Time;
            var dhuhr = Within(Find(PrayerType.Dhuhr) ?? new DateTimeOffset(now.Date.AddHours(12.5), now.Offset), now.AddHours(-12), now.AddHours(12));
            var sunrise = Within(Find(PrayerType.Sunrise) ?? dhuhr.AddMinutes(-390), dhuhr.AddHours(-24), dhuhr);
            var fajr = Within(Find(PrayerType.Fajr) ?? sunrise.AddMinutes(-90), sunrise.AddHours(-24), sunrise);
            var asr = Within(Find(PrayerType.Asr) ?? dhuhr.AddMinutes(210), dhuhr, dhuhr.AddHours(24));
            var maghrib = Within(Find(PrayerType.Maghrib) ?? dhuhr.AddMinutes(360), asr, asr.AddHours(24));
            var isha = Within(Find(PrayerType.Isha) ?? maghrib.AddMinutes(90), maghrib, maghrib.AddHours(24));
            return new DayTimes(fajr, sunrise, dhuhr, asr, maghrib, isha);
        }

        private static DateTimeOffset Within(DateTimeOffset time, DateTimeOffset from, DateTimeOffset until)
        {
            int guard = 0;
            while (time < from && guard++ < 4) time = time.AddDays(1);
            while (time >= until && guard++ < 8) time = time.AddDays(-1);
            return time;
        }
    }

    internal static class Palettes
    {
        private static Argb C(uint argb) => Argb.Hex(argb);
        private static Argb C(uint argb, float alpha) => Argb.Hex(argb).WithAlpha(alpha);

        private static SkyColors Sky(Argb[] sky, Argb[] arc, Argb orbGlow, Argb orbCore, Argb borderTop, Argb borderBottom,
            Argb footer, Argb card, bool stars, bool rays, bool crescent, bool warm, Argb cloudLight, Argb cloudDark, float cloudAlpha,
            float? starAlpha = null) =>
            new(sky, arc, orbGlow, orbCore, borderTop, borderBottom, footer, card, crescent, warm, cloudLight, cloudDark, cloudAlpha,
                starAlpha ?? (stars ? 1f : 0f), rays ? 1f : 0f);

        public static readonly SkyColors DeepNight = Sky(
            [C(0xFF02050E), C(0xFF060B1E), C(0xFF09142E), C(0xFF0D1D40), C(0xFF122854)],
            [C(0xFFD4AF37, 0.20f), C(0xFFFFD54F, 0.85f), C(0xFFD4AF37, 0.20f)],
            C(0xFFE2E8F0), C(0xFFFFFFFF), C(0xFFD4AF37, 0.55f), C(0xFF0A1530, 0.25f), C(0xFF02040A), C(0xFF040816),
            stars: true, rays: false, crescent: true, warm: false, C(0xFFD6E4FA), C(0xFF141D32), 0.42f);

        public static readonly SkyColors TahajjudVigil = Sky(
            [C(0xFF02040D), C(0xFF050A1A), C(0xFF081228), C(0xFF0B1936), C(0xFF102246)],
            [C(0xFFE5B558, 0.25f), C(0xFFFFD54F, 0.90f), C(0xFFE5B558, 0.25f)],
            C(0xFFE2E8F0), C(0xFFFFFFFF), C(0xFFE5B558, 0.65f), C(0xFF0A142E, 0.25f), C(0xFF020308), C(0xFF030714),
            stars: true, rays: false, crescent: true, warm: false, C(0xFFD4E2F8), C(0xFF121B30), 0.42f);

        public static readonly SkyColors PreDawnSahur = Sky(
            [C(0xFF030612), C(0xFF080D22), C(0xFF101432), C(0xFF1C1942), C(0xFF2E204E)],
            [C(0xFFE5B558, 0.30f), C(0xFFFFD166), C(0xFFE5B558, 0.30f)],
            C(0xFFFED7AA), C(0xFFFFF7ED), C(0xFFFFD166, 0.65f), C(0xFF121532, 0.30f), C(0xFF03050E), C(0xFF060A1C),
            stars: true, rays: false, crescent: true, warm: false, C(0xFFDCD6F4), C(0xFF1C1838), 0.48f);

        public static readonly SkyColors FajrSubhSadiq = Sky(
            [C(0xFF050816), C(0xFF0D122B), C(0xFF181B3E), C(0xFF28244E), C(0xFF422E58), C(0xFF6B4268)],
            [C(0xFFF4A261, 0.30f), C(0xFFFFD166), C(0xFFE76F51, 0.40f)],
            C(0xFFFFD166), C(0xFFFFF6D6), C(0xFFF4A261, 0.65f), C(0xFF161B3D, 0.35f), C(0xFF050816), C(0xFF080D22),
            stars: true, rays: false, crescent: true, warm: true, C(0xFFFFE0D4), C(0xFF38294B), 0.65f);

        public static readonly SkyColors SunriseGolden = Sky(
            [C(0xFF0C254C), C(0xFF174278), C(0xFF286498), C(0xFF5A8EBE), C(0xFFECA060), C(0xFFFFD266)],
            [C(0xFFFFB74D, 0.35f), C(0xFFFFE082), C(0xFFFF9800, 0.45f)],
            C(0xFFFFD54F), C(0xFFFFFDE7), C(0xFFFFD54F, 0.70f), C(0xFF1E6091, 0.35f), C(0xFF071F3E), C(0xFF09254C),
            stars: false, rays: true, crescent: false, warm: true, C(0xFFFFF2D8), C(0xFF784856), 0.78f);

        public static readonly SkyColors DuhaMorning = Sky(
            [C(0xFF0A2E5C), C(0xFF144D8A), C(0xFF2170AE), C(0xFF4C9ED2), C(0xFF8CD2F6)],
            [C(0xFFFFC107, 0.35f), C(0xFFFFE082), C(0xFFFF9800, 0.45f)],
            C(0xFFFFE082), C(0xFFFFFFFF), C(0xFFFFD54F, 0.70f), C(0xFF1E6091, 0.35f), C(0xFF072142), C(0xFF092950),
            stars: false, rays: true, crescent: false, warm: true, C(0xFFFFFFFF), C(0xFFBCD8EE), 0.80f);

        public static readonly SkyColors DhuhrZenith = Sky(
            [C(0xFF072440), C(0xFF0E4373), C(0xFF176096), C(0xFF2F85B8), C(0xFF78BCE0)],
            [C(0xFFFFD54F, 0.35f), C(0xFFFFF59D), C(0xFFFFB74D, 0.35f)],
            C(0xFFFFF59D), C(0xFFFFFFFF), C(0xFFFFD54F, 0.65f), C(0xFF0E4373, 0.30f), C(0xFF051B30), C(0xFF07223C),
            stars: false, rays: true, crescent: false, warm: true, C(0xFFFFFFFF), C(0xFFAED0EA), 0.82f);

        public static readonly SkyColors SunsetTwilight = Sky(
            [C(0xFF0F0B24), C(0xFF26123A), C(0xFF541C44), C(0xFF942F3C), C(0xFFDC602A), C(0xFFFAA434)],
            [C(0xFFE76F51, 0.35f), C(0xFFFF9800), C(0xFFD35400, 0.40f)],
            C(0xFFF39C12), C(0xFFFFF0D4), C(0xFFFF9800, 0.75f), C(0xFF26123A, 0.35f), C(0xFF0B071A), C(0xFF110826),
            stars: false, rays: true, crescent: false, warm: true, C(0xFFFFD6AA), C(0xFF58223E), 0.82f);

        public static readonly SkyColors MaghribDusk = Sky(
            [C(0xFF0B061A), C(0xFF1C0D2E), C(0xFF35143E), C(0xFF581D40), C(0xFF882C36), C(0xFFB84C2A)],
            [C(0xFFE76F51, 0.35f), C(0xFFF39C12), C(0xFFD35400, 0.40f)],
            C(0xFFF39C12), C(0xFFFFF0D4), C(0xFFF39C12, 0.75f), C(0xFF260B33, 0.35f), C(0xFF0A0414), C(0xFF110722),
            stars: true, rays: false, crescent: true, warm: true, C(0xFFE89E80), C(0xFF361839), 0.72f);

        public static readonly SkyColors IshaNightfall = Sky(
            [C(0xFF030610), C(0xFF060E22), C(0xFF0B1736), C(0xFF102048), C(0xFF152A5C)],
            [C(0xFFE5B558, 0.25f), C(0xFFFFD54F, 0.90f), C(0xFFE5B558, 0.25f)],
            C(0xFFE2E8F0), C(0xFFFFFFFF), C(0xFFE5B558, 0.65f), C(0xFF102048, 0.25f), C(0xFF030610), C(0xFF050B1C),
            stars: true, rays: false, crescent: true, warm: false, C(0xFFD4E2F8), C(0xFF131D32), 0.45f);

        public static readonly SkyColors PreDawn = PreDawnSahur with { StarAlpha = 0.9f };
        public static readonly SkyColors Fajr = FajrSubhSadiq with { StarAlpha = 0.45f };

        public static readonly SkyColors BlueHourDawn = Sky(
            [C(0xFF0F2349), C(0xFF1D3B6B), C(0xFF3E5C8E), C(0xFF8A7DA6), C(0xFFE0A08E), C(0xFFFFC489)],
            [C(0xFFFFB38A, 0.32f), C(0xFFFFD9A0), C(0xFFFF9E80, 0.40f)],
            C(0xFFFFC27A), C(0xFFFFF3E0), C(0xFFFFC27A, 0.66f), C(0xFF1D3B6B, 0.32f), C(0xFF0A1A38), C(0xFF0E2143),
            stars: false, rays: false, crescent: true, warm: true, C(0xFFFFD4C2), C(0xFF45365A), 0.72f, starAlpha: 0.12f);

        public static readonly SkyColors Sunrise = SunriseGolden;

        public static readonly SkyColors Morning = DuhaMorning with
        {
            Sky = [C(0xFF1A4C8E), C(0xFF2565AA), C(0xFF3884C4), C(0xFF5FA6D8), C(0xFF9CCDEB), C(0xFFDDE6DA)],
            CardBg = C(0xFF123C70)
        };

        public static readonly SkyColors Noon = DhuhrZenith with
        {
            Sky = [C(0xFF15457F), C(0xFF205E9E), C(0xFF337DBA), C(0xFF62A2D1), C(0xFFA3CCE4), C(0xFFE9D8AE)],
            CardBg = C(0xFF0F3666)
        };

        public static readonly SkyColors Afternoon = Sky(
            [C(0xFF2F5E8C), C(0xFF4D7FAA), C(0xFF8AA6B8), C(0xFFD2B48C), C(0xFFE8A35C), C(0xFFE08A3E)],
            [C(0xFFFFCA7A, 0.35f), C(0xFFFFE0A0), C(0xFFFFA94D, 0.40f)],
            C(0xFFFFD27A), C(0xFFFFF6DD), C(0xFFFFC870, 0.70f), C(0xFF2F5E8C, 0.32f), C(0xFF16293F), C(0xFF1C3350),
            stars: false, rays: true, crescent: false, warm: true, C(0xFFFFF2DD), C(0xFF8A6B75), 0.82f);

        public static readonly SkyColors GoldenHour = Sky(
            [C(0xFF203458), C(0xFF3F5079), C(0xFF86697D), C(0xFFD58A5E), C(0xFFF2A048), C(0xFFFFBE5C)],
            [C(0xFFFF9E5E, 0.35f), C(0xFFFFC870), C(0xFFE8743B, 0.40f)],
            C(0xFFFFB04A), C(0xFFFFEBC6), C(0xFFFFB04A, 0.74f), C(0xFF3F5079, 0.34f), C(0xFF141D33), C(0xFF1A2440),
            stars: false, rays: true, crescent: false, warm: true, C(0xFFFFD89E), C(0xFF6B384A), 0.80f);

        public static readonly SkyColors Sunset = SunsetTwilight;
        public static readonly SkyColors Afterglow = MaghribDusk with { StarAlpha = 0.30f };
        public static readonly SkyColors Isha = IshaNightfall with { StarAlpha = 0.90f };
        public static readonly SkyColors Midnight = DeepNight;
        public static readonly SkyColors LastThird = TahajjudVigil;
    }
}
