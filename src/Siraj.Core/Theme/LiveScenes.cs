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
    bool IsBlack);

public sealed record LiveIntensitySpec(string Id, string LabelKey, float MotionScale, float DetailScale, float VeilAdjustment);

public static class LiveScenes
{
    public const string DefaultSceneId = "DesertDunes";

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

    public static LiveSceneInfo Find(string? id) =>
        All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    public static LiveIntensitySpec FindIntensity(string? id) =>
        Intensities.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Intensities[1];

    public static LiveSceneColors ColorsFor(ColorScheme scheme, bool blackMode, bool timeSync = false, int minutesOfDay = 720)
    {
        var bg = scheme.Background;
        var primary = scheme.Primary;
        var secondary = scheme.Secondary;
        var container = scheme.PrimaryContainer;
        var white = Argb.Hex(0xFFFFFFFF);
        var black = Argb.Hex(0xFF000000);

        LiveSceneColors baseColors;
        if (blackMode && scheme.IsDark)
        {
            baseColors = new LiveSceneColors(
                SkyTop: black,
                SkyUpperMid: Argb.Lerp(black, primary, 0.06f),
                SkyLowerMid: Argb.Lerp(black, primary, 0.14f),
                SkyHorizon: Argb.Lerp(black, Argb.Lerp(primary, secondary, 0.45f), 0.24f),
                Accent: Argb.Lerp(primary, white, 0.10f),
                Secondary: Argb.Lerp(secondary, primary, 0.35f),
                Highlight: Argb.Lerp(white, primary, 0.18f),
                Shade: Argb.Lerp(black, primary, 0.18f),
                Ink: Argb.Hex(0xFF050608),
                Veil: black,
                IsDark: true,
                IsBlack: true);
        }
        else if (scheme.IsDark)
        {
            var deep = Argb.Lerp(bg, black, 0.38f);
            baseColors = new LiveSceneColors(
                SkyTop: deep,
                SkyUpperMid: Argb.Lerp(deep, primary, 0.10f),
                SkyLowerMid: Argb.Lerp(bg, primary, 0.18f),
                SkyHorizon: Argb.Lerp(bg, Argb.Lerp(primary, container, 0.35f), 0.32f),
                Accent: Argb.Lerp(primary, white, 0.12f),
                Secondary: Argb.Lerp(secondary, primary, 0.40f),
                Highlight: Argb.Lerp(white, primary, 0.20f),
                Shade: Argb.Lerp(bg, primary, 0.24f),
                Ink: Argb.Lerp(deep, black, 0.45f),
                Veil: deep,
                IsDark: true,
                IsBlack: false);
        }
        else
        {
            var warmPaper = Argb.Lerp(bg, container, 0.42f);
            baseColors = new LiveSceneColors(
                SkyTop: Argb.Lerp(warmPaper, primary, 0.10f),
                SkyUpperMid: Argb.Lerp(warmPaper, primary, 0.05f),
                SkyLowerMid: Argb.Lerp(bg, container, 0.28f),
                SkyHorizon: Argb.Lerp(white, container, 0.22f),
                Accent: primary,
                Secondary: Argb.Lerp(secondary, primary, 0.35f),
                Highlight: white,
                Shade: Argb.Lerp(primary, Argb.Hex(0xFF3E2D20), 0.25f),
                Ink: Argb.Lerp(primary, Argb.Hex(0xFF1F1612), 0.55f),
                Veil: Argb.Lerp(white, bg, 0.65f),
                IsDark: false,
                IsBlack: false);
        }

        if (!timeSync) return baseColors;

        // Gentle hour-of-day shift: warm dawn/dusk, crisp midday, deep midnight.
        float h = (minutesOfDay % 1440) / 60f;
        Argb tint;
        float weight;
        if (h < 5.5f || h >= 20.5f) { tint = Argb.Hex(0xFF0A1430); weight = scheme.IsDark ? 0.24f : 0.12f; }
        else if (h < 8.5f) { tint = Argb.Hex(0xFFFFB673); weight = 0.16f; }
        else if (h < 16.5f) { tint = Argb.Hex(0xFF6DB8F0); weight = 0.10f; }
        else { tint = Argb.Hex(0xFFE87E4A); weight = 0.18f; }

        return baseColors with
        {
            SkyTop = Argb.Lerp(baseColors.SkyTop, tint, weight * 0.7f),
            SkyHorizon = Argb.Lerp(baseColors.SkyHorizon, tint, weight),
            Accent = Argb.Lerp(baseColors.Accent, tint, weight * 0.45f),
        };
    }

    public static float StarVisibility(int minutesOfDay)
    {
        float h = (minutesOfDay % 1440) / 60f;
        if (h >= 20f || h <= 4.5f) return 1f;
        if (h > 4.5f && h < 6.5f) return 1f - (h - 4.5f) / 2f;
        if (h > 18f && h < 20f) return (h - 18f) / 2f;
        return 0f;
    }
}
