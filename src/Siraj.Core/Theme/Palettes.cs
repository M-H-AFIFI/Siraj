using Siraj.Core.Sky;

namespace Siraj.Core.Theme;

public readonly record struct PaletteRoles(
    uint Primary, uint OnPrimary, uint PrimaryContainer, uint OnPrimaryContainer,
    uint Background, uint Surface, uint SurfaceVariant, uint OnBackground, uint OnSurface);

public sealed record ColorScheme(
    Argb Primary, Argb OnPrimary, Argb PrimaryContainer, Argb OnPrimaryContainer,
    Argb Secondary, Argb SecondaryContainer, Argb OnSecondaryContainer,
    Argb Background, Argb Surface, Argb SurfaceVariant, Argb OnBackground, Argb OnSurface, Argb OnSurfaceVariant,
    Argb Outline, Argb OutlineVariant,
    Argb SurfaceContainerLow, Argb SurfaceContainer, Argb SurfaceContainerHigh,
    Argb Gold, bool IsDark);

public static partial class Palettes
{
    public static readonly Argb SirajGold = Argb.Hex(0xFFD4AF37);
    public static readonly Argb SirajGoldDeep = Argb.Hex(0xFF8A6A16);

    public static string Resolve(string? name) => name switch
    {
        "RoyalIndigo" or "ImperialOud" or "RoyalAmethyst" => "RoyalIndigo",
        "Emerald" or "MedinaDawn" => "Emerald",
        "Rose" or "CrimsonVelvet" or "SaharaDusk" => "Rose",
        "OceanBlue" or "BlueNile" => "OceanBlue",
        "Coffee" or "Violet" or "GoldenAmber" or "Daylight" or "GoldenOasis" => "Coffee",
        "TealCaspian" or "Midnight" => "TealCaspian",
        "Sand" or "OliveSage" or "AmoledBlack" or "PureNight" => "Sand",
        "Olive" => "Olive",
        "Burgundy" => "Burgundy",
        "Slate" => "Slate",
        "Lime" => "Lime",
        "Gold" => "Gold",
        _ => "Emerald"
    };

    public static Argb Swatch(string name) => Argb.Hex(Base[Resolve(name)].Light.Primary);

    public static ColorScheme Scheme(string palette, bool dark, bool black)
    {
        var roles = dark || black ? Base[Resolve(palette)].Dark : Base[Resolve(palette)].Light;
        bool isDark = dark || black;
        var primary = Argb.Hex(roles.Primary);
        var background = Argb.Hex(roles.Background);
        var surface = Argb.Hex(roles.Surface);
        var surfaceVariant = Argb.Hex(roles.SurfaceVariant);
        var onSurface = Argb.Hex(roles.OnSurface);
        if (black)
        {
            background = Argb.Hex(0xFF000000);
            surface = Argb.Hex(0xFF000000);
            surfaceVariant = Argb.Hex(0xFF121212);
        }
        var black0 = Argb.Hex(0xFF000000);
        var secondary = Argb.Lerp(primary, isDark ? surfaceVariant : onSurface, 0.30f);
        var secondaryContainer = Argb.Lerp(surface, primary, isDark ? 0.30f : 0.15f);
        return new ColorScheme(
            Primary: primary,
            OnPrimary: Argb.Hex(roles.OnPrimary),
            PrimaryContainer: Argb.Hex(roles.PrimaryContainer),
            OnPrimaryContainer: Argb.Hex(roles.OnPrimaryContainer),
            Secondary: secondary,
            SecondaryContainer: secondaryContainer,
            OnSecondaryContainer: OnColorFor(secondaryContainer),
            Background: background,
            Surface: surface,
            SurfaceVariant: surfaceVariant,
            OnBackground: Argb.Hex(roles.OnBackground),
            OnSurface: onSurface,
            OnSurfaceVariant: Argb.Lerp(surface, onSurface, 0.70f),
            Outline: Argb.Lerp(surface, onSurface, 0.50f),
            OutlineVariant: Argb.Lerp(surface, onSurface, 0.20f),
            SurfaceContainerLow: isDark ? Argb.Lerp(background, surface, 0.5f) : Argb.Lerp(surface, background, 0.55f),
            SurfaceContainer: isDark ? surface : background,
            SurfaceContainerHigh: isDark ? Argb.Lerp(surface, surfaceVariant, 0.6f) : Argb.Lerp(background, surfaceVariant, 0.7f),
            Gold: isDark ? SirajGold : SirajGoldDeep,
            IsDark: isDark);
    }

    public static Argb OnColorFor(Argb background) =>
        background.Luminance > 0.45 ? Argb.Hex(0xFF1A1A1A) : Argb.Hex(0xFFFFFFFF);
}
