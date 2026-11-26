using Siraj.Core.Prayer;

namespace Siraj.Core.Sky;

public sealed record AdhanSkySpec(
    SkyColors SkyTheme,
    bool IsDaytime,
    bool IsCrescent,
    bool ShowStars,
    float Warmth,
    float DuskUndersideGlow,
    float StarDensity,
    float MoteStrength,
    Argb HorizonGlowColor,
    float HorizonGlowAlpha)
{
    public static readonly Argb DeepBase = Argb.Hex(0xFF060A13);

    public static AdhanSkySpec For(PrayerType prayer)
    {
        var sky = SkyTimeline.SkyForPrayer(prayer);
        return prayer switch
        {
            PrayerType.Fajr => new AdhanSkySpec(
                sky, IsDaytime: false, IsCrescent: true, ShowStars: true,
                Warmth: 0.35f, DuskUndersideGlow: 0.22f, StarDensity: 0.55f, MoteStrength: 0f,
                HorizonGlowColor: Argb.Hex(0xFF93B8E8), HorizonGlowAlpha: 0.24f),
            PrayerType.Sunrise => new AdhanSkySpec(
                sky, IsDaytime: true, IsCrescent: false, ShowStars: false,
                Warmth: 0.86f, DuskUndersideGlow: 0.65f, StarDensity: 0f, MoteStrength: 0.72f,
                HorizonGlowColor: sky.OrbGlow, HorizonGlowAlpha: 0.30f),
            PrayerType.Dhuhr => new AdhanSkySpec(
                sky, IsDaytime: true, IsCrescent: false, ShowStars: false,
                Warmth: 0.12f, DuskUndersideGlow: 0f, StarDensity: 0f, MoteStrength: 0.28f,
                HorizonGlowColor: sky.OrbGlow, HorizonGlowAlpha: 0.16f),
            PrayerType.Asr => new AdhanSkySpec(
                sky, IsDaytime: true, IsCrescent: false, ShowStars: false,
                Warmth: 0.78f, DuskUndersideGlow: 0.36f, StarDensity: 0f, MoteStrength: 0.85f,
                HorizonGlowColor: Argb.Hex(0xFFFFB74D), HorizonGlowAlpha: 0.30f),
            PrayerType.Maghrib => new AdhanSkySpec(
                sky, IsDaytime: false, IsCrescent: true, ShowStars: true,
                Warmth: 0.92f, DuskUndersideGlow: 0.86f, StarDensity: 0.42f, MoteStrength: 0.35f,
                HorizonGlowColor: Argb.Hex(0xFFE8743B), HorizonGlowAlpha: 0.36f),
            _ => new AdhanSkySpec(
                sky, IsDaytime: false, IsCrescent: true, ShowStars: true,
                Warmth: 0f, DuskUndersideGlow: 0f, StarDensity: 0.88f, MoteStrength: 0f,
                HorizonGlowColor: Argb.Hex(0xFF94A3B8), HorizonGlowAlpha: 0.12f),
        };
    }

    public (float Position, Argb Color)[] BuildStops()
    {
        var g = SkyTheme.Sky;
        if (g.Count == 0) return [(0f, DeepBase), (1f, DeepBase)];

        var top = Argb.Lerp(Sample(g, 0f), Argb.Hex(0xFF060B14), 0.04f);
        var upperMid = Sample(g, 0.28f);

        if (SkyTheme.IsWarmGlow && !SkyTheme.ShowCrescent)
        {
            // Daytime palettes end in pale sand for the horizon; anchor the lower fade through the
            // deeper upper blue so blending toward DeepBase avoids a muddy band behind the title.
            var midSky = Sample(g, 0.52f);
            var horizon = Argb.Lerp(Sample(g, 0.72f), Argb.Lerp(upperMid, DeepBase, 0.42f), 0.48f);
            var lowerMid = Argb.Lerp(upperMid, DeepBase, 0.78f);
            var lowerAnchor = Argb.Lerp(upperMid, DeepBase, 0.94f);
            return
            [
                (0.00f, top), (0.24f, upperMid), (0.48f, midSky), (0.64f, horizon),
                (0.78f, lowerMid), (0.90f, lowerAnchor), (1.00f, DeepBase),
            ];
        }

        var nightMid = Sample(g, 0.62f);
        var nightHorizon = Argb.Lerp(Sample(g, 0.94f), DeepBase, 0.22f);
        var nightLowerMid = Argb.Lerp(g[^1], DeepBase, 0.72f);
        var nightLowerAnchor = Argb.Lerp(g[^1], DeepBase, 0.92f);
        return
        [
            (0.00f, top), (0.24f, upperMid), (0.48f, nightMid), (0.64f, nightHorizon),
            (0.78f, nightLowerMid), (0.90f, nightLowerAnchor), (1.00f, DeepBase),
        ];
    }

    private static Argb Sample(IReadOnlyList<Argb> colors, float fraction)
    {
        if (colors.Count == 1) return colors[0];
        float f = Math.Clamp(fraction, 0f, 1f) * (colors.Count - 1);
        int lo = Math.Clamp((int)f, 0, colors.Count - 1);
        int hi = Math.Min(lo + 1, colors.Count - 1);
        return Argb.Lerp(colors[lo], colors[hi], f - lo);
    }
}
