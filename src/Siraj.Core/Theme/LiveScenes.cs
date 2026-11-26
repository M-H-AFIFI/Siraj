using Siraj.Core.Sky;

namespace Siraj.Core.Theme;

public sealed record LiveSceneInfo(
    string Id,
    string TitleKey,
    string SubtitleKey,
    string PairedPalette,
    uint SwatchPrimary,
    uint SwatchSecondary);

public sealed record LiveSceneColors(
    Argb SkyTop,
    Argb SkyUpperMid,
    Argb SkyLowerMid,
    Argb SkyHorizon,
    Argb Accent,
    Argb Secondary,
    Argb Highlight,
    Argb Shade,
    Argb Ink,
    Argb Veil,
    bool IsDark,
    bool IsBlack)
{
    public LiveSceneColors LerpTo(LiveSceneColors other, float t)
    {
        if (t <= 0f) return this;
        if (t >= 1f) return other;
        return new LiveSceneColors(
            SkyTop: Argb.Lerp(SkyTop, other.SkyTop, t),
            SkyUpperMid: Argb.Lerp(SkyUpperMid, other.SkyUpperMid, t),
            SkyLowerMid: Argb.Lerp(SkyLowerMid, other.SkyLowerMid, t),
            SkyHorizon: Argb.Lerp(SkyHorizon, other.SkyHorizon, t),
            Accent: Argb.Lerp(Accent, other.Accent, t),
            Secondary: Argb.Lerp(Secondary, other.Secondary, t),
            Highlight: Argb.Lerp(Highlight, other.Highlight, t),
            Shade: Argb.Lerp(Shade, other.Shade, t),
            Ink: Argb.Lerp(Ink, other.Ink, t),
            Veil: Argb.Lerp(Veil, other.Veil, t),
            IsDark: other.IsDark,
            IsBlack: other.IsBlack);
    }
}

public sealed record LiveIntensitySpec(string Id, string LabelKey, float MotionScale, float DetailScale, float VeilAdjustment);

internal readonly record struct TimeSkyPalette(
    Argb SkyTop,
    Argb SkyUpperMid,
    Argb SkyLowerMid,
    Argb SkyHorizon,
    Argb AccentGlow)
{
    public TimeSkyPalette BlendWith(TimeSkyPalette other, float t) => new(
        Argb.Lerp(SkyTop, other.SkyTop, t),
        Argb.Lerp(SkyUpperMid, other.SkyUpperMid, t),
        Argb.Lerp(SkyLowerMid, other.SkyLowerMid, t),
        Argb.Lerp(SkyHorizon, other.SkyHorizon, t),
        Argb.Lerp(AccentGlow, other.AccentGlow, t));
}

internal readonly record struct SkyPhaseSpec(
    TimeSkyPalette Light,
    TimeSkyPalette Dark,
    float StarVisibility);

public static class LiveScenes
{
    public const string DefaultSceneId = "DesertDunes";
    public const float RestSeconds = 4.2f;

    public static readonly IReadOnlyList<LiveSceneInfo> All =
    [
        new("DesertDunes", "live_theme_desert", "live_theme_desert_sub", "Sand", 0xFFD69A52, 0xFF8C5327),
        new("SeaWaves", "live_theme_sea", "live_theme_sea_sub", "TealCaspian", 0xFF1E9BB0, 0xFF66D4DF),
        new("Aurora", "live_theme_aurora", "live_theme_aurora_sub", "Emerald", 0xFF28C997, 0xFF6A7FDB),
        new("NightStars", "live_theme_night", "live_theme_night_sub", "Gold", 0xFFE3BC63, 0xFF3E5282),
        new("RosePetals", "live_theme_rose", "live_theme_rose_sub", "Rose", 0xFFDE718C, 0xFFF5B5C5),
        new("Snow", "live_theme_snow", "live_theme_snow_sub", "Slate", 0xFF8AB8DC, 0xFFEAF4FC),
        new("PurpleButterflies", "live_theme_butterfly", "live_theme_butterfly_sub", "RoyalIndigo", 0xFF9F7AEA, 0xFFD6BCFA),
        new("Rings", "live_theme_rings", "live_theme_rings_sub", "OceanBlue", 0xFF3B82F6, 0xFF93C5FD),
        new("Circles", "live_theme_circles", "live_theme_circles_sub", "Burgundy", 0xFF9D174D, 0xFFF472B6),
        new("Particles", "live_theme_particles", "live_theme_particles_sub", "TealCaspian", 0xFF14B8A6, 0xFF5EEAD4),
        new("LavaLamp", "live_theme_lava_lamp", "live_theme_lava_lamp_sub", "Coffee", 0xFFD97706, 0xFFFBBF24),
        new("Space", "live_theme_space", "live_theme_space_sub", "RoyalIndigo", 0xFF6366F1, 0xFFA5B4FC),
        new("Silk", "live_theme_silk", "live_theme_silk_sub", "Rose", 0xFFEC4899, 0xFFFBCFE8),
        new("Contours", "live_theme_contours", "live_theme_contours_sub", "Olive", 0xFF65A30D, 0xFFBEF264),
        new("Mosaic", "live_theme_mosaic", "live_theme_mosaic_sub", "Gold", 0xFFD4AF37, 0xFFFDE68A),
        new("Waves", "live_theme_waves", "live_theme_waves_sub", "OceanBlue", 0xFF0EA5E9, 0xFF7DD3FC),
        new("Harvest", "live_theme_harvest", "live_theme_harvest_sub", "Sand", 0xFFCA8A04, 0xFFFDE047),
    ];

    public static readonly IReadOnlyList<LiveIntensitySpec> Intensities =
    [
        new("Subtle", "live_theme_intensity_subtle", 0.65f, 0.68f, 0.10f),
        new("Balanced", "live_theme_intensity_balanced", 1.00f, 1.00f, 0.00f),
        new("Vivid", "live_theme_intensity_vivid", 1.24f, 1.26f, -0.09f),
    ];

    // 6 prayer-aligned solar phases for Time-of-Day Sky Sync.
    private static readonly SkyPhaseSpec FajrDawn = new(
        Light: new(Argb.Hex(0xFFC9D4EE), Argb.Hex(0xFFDED8EC), Argb.Hex(0xFFF2DFD9), Argb.Hex(0xFFFCEAD7), Argb.Hex(0xFFE8A47C)),
        Dark: new(Argb.Hex(0xFF090E1E), Argb.Hex(0xFF161B33), Argb.Hex(0xFF2B263D), Argb.Hex(0xFF4A333B), Argb.Hex(0xFFE59A73)),
        StarVisibility: 0.42f);

    private static readonly SkyPhaseSpec MorningDuha = new(
        Light: new(Argb.Hex(0xFFC4E1F7), Argb.Hex(0xFFD9EDFC), Argb.Hex(0xFFEBF6FE), Argb.Hex(0xFFFFFBF2), Argb.Hex(0xFFF2C46D)),
        Dark: new(Argb.Hex(0xFF0B1628), Argb.Hex(0xFF132640), Argb.Hex(0xFF1D3654), Argb.Hex(0xFF2B4763), Argb.Hex(0xFFE8BE6B)),
        StarVisibility: 0.0f);

    private static readonly SkyPhaseSpec MiddayDhuhr = new(
        Light: new(Argb.Hex(0xFFBDDDF7), Argb.Hex(0xFFD3EAFB), Argb.Hex(0xFFE7F4FD), Argb.Hex(0xFFF7FBFF), Argb.Hex(0xFF4EA5E8)),
        Dark: new(Argb.Hex(0xFF0A1424), Argb.Hex(0xFF11223B), Argb.Hex(0xFF193252), Argb.Hex(0xFF234369), Argb.Hex(0xFF68B5F2)),
        StarVisibility: 0.0f);

    private static readonly SkyPhaseSpec GoldenAsr = new(
        Light: new(Argb.Hex(0xFFD9D8EC), Argb.Hex(0xFFEBE1D8), Argb.Hex(0xFFF7E8CE), Argb.Hex(0xFFFFF3DC), Argb.Hex(0xFFE8A248)),
        Dark: new(Argb.Hex(0xFF101121), Argb.Hex(0xFF1E1A2B), Argb.Hex(0xFF33252B), Argb.Hex(0xFF4D3226), Argb.Hex(0xFFE6A14C)),
        StarVisibility: 0.08f);

    private static readonly SkyPhaseSpec MaghribDusk = new(
        Light: new(Argb.Hex(0xFFC8C5E4), Argb.Hex(0xFFE0C8DC), Argb.Hex(0xFFF4D3CD), Argb.Hex(0xFFFCE2C9), Argb.Hex(0xFFE27A68)),
        Dark: new(Argb.Hex(0xFF0B0A1A), Argb.Hex(0xFF1B132B), Argb.Hex(0xFF331C31), Argb.Hex(0xFF52282E), Argb.Hex(0xFFE8806B)),
        StarVisibility: 0.55f);

    private static readonly SkyPhaseSpec IshaNight = new(
        Light: new(Argb.Hex(0xFFD0D8EE), Argb.Hex(0xFFDDE4F5), Argb.Hex(0xFFEAEEF9), Argb.Hex(0xFFF5F7FC), Argb.Hex(0xFFD6AC54)),
        Dark: new(Argb.Hex(0xFF050812), Argb.Hex(0xFF0A1021), Argb.Hex(0xFF111A33), Argb.Hex(0xFF192647), Argb.Hex(0xFFE6C16A)),
        StarVisibility: 1.0f);

    public static string NormalizeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return DefaultSceneId;
        if (id.Equals("FloatingLanterns", StringComparison.OrdinalIgnoreCase) ||
            id.Equals("Lanterns", StringComparison.OrdinalIgnoreCase)) return "DesertDunes";
        if (id.Equals("Bokeh", StringComparison.OrdinalIgnoreCase)) return "Circles";
        if (id.Equals("Constellation", StringComparison.OrdinalIgnoreCase)) return "Particles";
        if (id.Equals("Ripples", StringComparison.OrdinalIgnoreCase)) return "Rings";
        var match = All.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? DefaultSceneId;
    }

    public static LiveSceneInfo Find(string? id)
    {
        var normalized = NormalizeId(id);
        return All.FirstOrDefault(s => s.Id.Equals(normalized, StringComparison.OrdinalIgnoreCase)) ?? All[0];
    }

    public static LiveIntensitySpec FindIntensity(string? id) =>
        Intensities.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Intensities[1];

    public static LiveSceneColors ColorsFor(ColorScheme scheme, bool blackMode, bool timeSync = false, int minutesOfDay = 720)
    {
        var bg = Opaque(scheme.Background);
        var p = Opaque(scheme.Primary);
        var t = Opaque(scheme.Tertiary);
        var white = Argb.Hex(0xFFFFFFFF);
        var black = Argb.Hex(0xFF000000);
        bool isDark = scheme.IsDark;
        bool isAmoled = blackMode && isDark;

        LiveSceneColors baseColors;
        if (isDark)
        {
            var night = isAmoled ? black : Argb.Lerp(bg, black, 0.38f);
            float lift = isAmoled ? 0.72f : 1f;
            baseColors = new LiveSceneColors(
                SkyTop: night,
                SkyUpperMid: Argb.Lerp(night, p, 0.07f * lift),
                SkyLowerMid: Argb.Lerp(night, p, 0.15f * lift),
                SkyHorizon: Argb.Lerp(night, Argb.Lerp(p, t, 0.35f), 0.27f * lift),
                Accent: p,
                Secondary: t,
                Highlight: Argb.Lerp(white, p, 0.16f),
                Shade: Argb.Lerp(night, p, 0.24f * lift),
                Ink: Argb.Lerp(night, p, 0.08f * lift),
                Veil: night,
                IsDark: true,
                IsBlack: isAmoled);
        }
        else
        {
            var paper = Argb.Lerp(bg, white, 0.45f);
            baseColors = new LiveSceneColors(
                SkyTop: Argb.Lerp(paper, p, 0.22f),
                SkyUpperMid: Argb.Lerp(paper, p, 0.13f),
                SkyLowerMid: Argb.Lerp(paper, p, 0.06f),
                SkyHorizon: Argb.Lerp(paper, t, 0.03f),
                Accent: p,
                Secondary: t,
                Highlight: white,
                Shade: Argb.Lerp(paper, p, 0.26f),
                Ink: Argb.Lerp(paper, p, 0.50f),
                Veil: paper,
                IsDark: false,
                IsBlack: false);
        }

        if (!timeSync) return baseColors;

        var timeTint = TimeTintFor(isDark, minutesOfDay);
        float w = isAmoled ? 0.20f : 0.30f;
        return baseColors with
        {
            SkyTop = isAmoled ? black : Argb.Lerp(baseColors.SkyTop, timeTint.SkyTop, w),
            SkyUpperMid = Argb.Lerp(baseColors.SkyUpperMid, timeTint.SkyUpperMid, w),
            SkyLowerMid = Argb.Lerp(baseColors.SkyLowerMid, timeTint.SkyLowerMid, w),
            SkyHorizon = Argb.Lerp(baseColors.SkyHorizon, timeTint.SkyHorizon, w),
            Secondary = Argb.Lerp(baseColors.Secondary, timeTint.AccentGlow, 0.35f),
        };
    }

    public static float StarVisibility(int minutesOfDay)
    {
        var (primary, next, blend) = PhaseForMinutesOfDay(minutesOfDay);
        return primary.StarVisibility + (next.StarVisibility - primary.StarVisibility) * blend;
    }

    private static TimeSkyPalette TimeTintFor(bool isDark, int minutesOfDay)
    {
        var (primary, next, blend) = PhaseForMinutesOfDay(minutesOfDay);
        var p1 = isDark ? primary.Dark : primary.Light;
        var p2 = isDark ? next.Dark : next.Light;
        return p1.BlendWith(p2, blend);
    }

    private static (SkyPhaseSpec Primary, SkyPhaseSpec Next, float Blend) PhaseForMinutesOfDay(int minutesOfDay)
    {
        int m = ((minutesOfDay % 1440) + 1440) % 1440;
        if (m is >= 255 and < 390)
        {
            float t = (m - 255f) / (390f - 255f);
            return (FajrDawn, MorningDuha, SmoothStep(t));
        }
        if (m is >= 390 and < 690)
        {
            float t = Math.Clamp((m - 600f) / 90f, 0f, 1f);
            return (MorningDuha, MiddayDhuhr, SmoothStep(t));
        }
        if (m is >= 690 and < 915)
        {
            float t = Math.Clamp((m - 840f) / 75f, 0f, 1f);
            return (MiddayDhuhr, GoldenAsr, SmoothStep(t));
        }
        if (m is >= 915 and < 1065)
        {
            float t = Math.Clamp((m - 990f) / 75f, 0f, 1f);
            return (GoldenAsr, MaghribDusk, SmoothStep(t));
        }
        if (m is >= 1065 and < 1185)
        {
            float t = (m - 1065f) / (1185f - 1065f);
            return (MaghribDusk, IshaNight, SmoothStep(t));
        }
        float nightT = m is >= 210 and < 255 ? (m - 210f) / 45f : 0f;
        return (IshaNight, FajrDawn, SmoothStep(nightT));
    }

    private static float SmoothStep(float x)
    {
        float c = Math.Clamp(x, 0f, 1f);
        return c * c * (3f - 2f * c);
    }

    private static Argb Opaque(Argb c) => new(255, c.R, c.G, c.B);
}
