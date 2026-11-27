using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Theme;
using Siraj.Core.Sky;
using Siraj.Core.Theme;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Siraj.App.Services;

internal sealed class ThemeService
{
    private readonly UISettings _ui = new();
    private readonly List<WeakReference<FrameworkElement>> _roots = [];

    public ColorScheme Scheme { get; private set; } = Palettes.Scheme("Emerald", false, false);
    public GlassSpec Glass { get; private set; } = GlassSpec.For(Palettes.Scheme("Emerald", false, false), false, "Clear");

    public bool IsDark => Scheme.IsDark;
    public bool IsGlass => App.Settings.Current.GlassUi;
    public bool IsLiveBackground => App.Settings.Current.LiveBackground;

    public event Action? Changed;

    public ThemeService()
    {
        _ui.ColorValuesChanged += (_, _) => App.Dispatcher.TryEnqueue(Apply);
    }

    public static Color ToColor(Argb c) => Color.FromArgb(c.A, c.R, c.G, c.B);
    public static Color ToColor(Argb c, float alpha) => Color.FromArgb((byte)Math.Clamp(alpha * 255f, 0, 255), c.R, c.G, c.B);

    private bool SystemIsDark()
    {
        var background = _ui.GetColorValue(UIColorType.Background);
        return background.R + background.G + background.B < 3 * 128;
    }

    public void Register(FrameworkElement root)
    {
        _roots.Add(new WeakReference<FrameworkElement>(root));
        root.RequestedTheme = IsDark ? ElementTheme.Dark : ElementTheme.Light;
    }

    public void Apply()
    {
        var s = App.Settings.Current;
        bool dark = s.ThemeMode switch { "Dark" => true, "Light" => false, _ => SystemIsDark() };
        bool black = s.BlackMode && dark;
        Scheme = Palettes.Scheme(s.Palette, dark, black);
        Glass = GlassSpec.For(Scheme, black, s.GlassFinish);

        var r = Application.Current.Resources;
        Set(r, "SirajPrimaryBrush", Scheme.Primary);
        Set(r, "SirajOnPrimaryBrush", Scheme.OnPrimary);
        Set(r, "SirajPrimaryContainerBrush", Scheme.PrimaryContainer);
        Set(r, "SirajOnPrimaryContainerBrush", Scheme.OnPrimaryContainer);
        Set(r, "SirajSecondaryContainerBrush", Scheme.SecondaryContainer);
        Set(r, "SirajOnSecondaryContainerBrush", Scheme.OnSecondaryContainer);
        Set(r, "SirajBackgroundBrush", Scheme.Background);
        Set(r, "SirajSurfaceBrush", Scheme.Surface);
        Set(r, "SirajSurfaceVariantBrush", Scheme.SurfaceVariant);
        Set(r, "SirajSurfaceContainerLowBrush", Scheme.SurfaceContainerLow);
        Set(r, "SirajSurfaceContainerBrush", Scheme.SurfaceContainer);
        Set(r, "SirajSurfaceContainerHighBrush", Scheme.SurfaceContainerHigh);
        Set(r, "SirajOnSurfaceBrush", Scheme.OnSurface);
        Set(r, "SirajOnSurfaceVariantBrush", Scheme.OnSurfaceVariant);
        Set(r, "SirajOutlineBrush", Scheme.Outline);
        Set(r, "SirajOutlineVariantBrush", Scheme.OutlineVariant);
        Set(r, "SirajGoldBrush", Scheme.Gold);

        Set(r, "SirajPrimarySoftBrush", WithAlpha(Scheme.Primary, dark ? 0.18f : 0.11f));
        Set(r, "SirajPrimaryBorderBrush", WithAlpha(Scheme.Primary, dark ? 0.34f : 0.22f));
        Set(r, "SirajGoldSoftBrush", WithAlpha(Scheme.Gold, dark ? 0.20f : 0.14f));

        // Touch deferred App.xaml glass brushes so they register before UpdateRole pushes active colors.
        _ = r["SirajGlassCardBrush"];
        _ = r["SirajGlassElevatedBrush"];
        _ = r["SirajGlassPillBrush"];
        if (s.GlassUi)
        {
            SirajGlassBrush.UpdateRole("Card", ToColor(Glass.LensBodyTint), Glass.BlurAmount, ToColor(Glass.CardFillTop));
            SirajGlassBrush.UpdateRole("Elevated", ToColor(Glass.ChromeLensTint), Glass.ChromeBlurAmount, ToColor(Glass.ElevatedFill));
            SirajGlassBrush.UpdateRole("Pill", ToColor(Glass.PillFill), MathF.Max(6f, Glass.BlurAmount * 0.75f), ToColor(Glass.PillFill));
            SetGradient5(r, "SirajGlassBorderBrush",
                Glass.SpecularTopStart, Glass.SpecularUpperMid, Glass.SpecularMid, Glass.SpecularLowerMid, Glass.SpecularBottomEnd);
            Set(r, "SirajGlassCardHoverBrush", Glass.CardHoverFill);
            Set(r, "SirajDividerBrush", Glass.Divider);
        }
        else if (s.LiveBackground)
        {
            float cardAlpha = dark ? 0.90f : 0.92f;
            var cardFill = WithAlpha(black ? Argb.Hex(0xFF101216) : Scheme.Surface, cardAlpha);
            var borderCol = WithAlpha(Scheme.OutlineVariant, dark ? 0.40f : 0.58f);
            var elevFill = WithAlpha(black ? Argb.Hex(0xFF13151A) : Scheme.SurfaceContainerHigh, 0.95f);
            var pillFill = WithAlpha(Scheme.SurfaceVariant, dark ? 0.65f : 0.78f);
            SirajGlassBrush.UpdateRole("Card", ToColor(cardFill), 0f, ToColor(cardFill));
            SirajGlassBrush.UpdateRole("Elevated", ToColor(elevFill), 0f, ToColor(elevFill));
            SirajGlassBrush.UpdateRole("Pill", ToColor(pillFill), 0f, ToColor(pillFill));
            SetGradient5(r, "SirajGlassBorderBrush", borderCol, borderCol, borderCol, borderCol, borderCol);
            Set(r, "SirajGlassCardHoverBrush", WithAlpha(Scheme.SurfaceContainerHigh, 0.96f));
            Set(r, "SirajDividerBrush", WithAlpha(Scheme.OutlineVariant, 0.45f));
        }
        else
        {
            var cardFill = black ? Argb.Hex(0xFF0E1014) : Scheme.Surface;
            var borderCol = WithAlpha(Scheme.OutlineVariant, dark ? 0.42f : 0.62f);
            var elevFill = black ? Argb.Hex(0xFF111318) : Scheme.SurfaceContainerHigh;
            SirajGlassBrush.UpdateRole("Card", ToColor(cardFill), 0f, ToColor(cardFill));
            SirajGlassBrush.UpdateRole("Elevated", ToColor(elevFill), 0f, ToColor(elevFill));
            SirajGlassBrush.UpdateRole("Pill", ToColor(Scheme.SurfaceVariant), 0f, ToColor(Scheme.SurfaceVariant));
            SetGradient5(r, "SirajGlassBorderBrush", borderCol, borderCol, borderCol, borderCol, borderCol);
            Set(r, "SirajGlassCardHoverBrush", Scheme.SurfaceContainerHigh);
            Set(r, "SirajDividerBrush", WithAlpha(Scheme.OutlineVariant, 0.55f));
        }

        var light = Palettes.Scheme(s.Palette, false, false).Primary;
        var darkPrimary = Palettes.Scheme(s.Palette, true, false).Primary;
        var black0 = Argb.Hex(0xFF000000);
        var white0 = Argb.Hex(0xFFFFFFFF);
        r["SystemAccentColor"] = ToColor(light);
        r["SystemAccentColorDark1"] = ToColor(light);
        r["SystemAccentColorDark2"] = ToColor(Argb.Lerp(light, black0, 0.2f));
        r["SystemAccentColorDark3"] = ToColor(Argb.Lerp(light, black0, 0.4f));
        r["SystemAccentColorLight1"] = ToColor(Argb.Lerp(darkPrimary, light, 0.5f));
        r["SystemAccentColorLight2"] = ToColor(darkPrimary);
        r["SystemAccentColorLight3"] = ToColor(Argb.Lerp(darkPrimary, white0, 0.3f));

        var requested = dark ? ElementTheme.Dark : ElementTheme.Light;
        _roots.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var w in _roots)
        {
            if (!w.TryGetTarget(out var root)) continue;
            root.RequestedTheme = requested == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
            root.RequestedTheme = requested;
        }
        Changed?.Invoke();
    }

    private static Argb WithAlpha(Argb c, float a) => new((byte)Math.Clamp(a * 255f, 0, 255), c.R, c.G, c.B);

    private static void Set(ResourceDictionary dictionary, string key, Argb color)
    {
        if (dictionary.TryGetValue(key, out var value) && value is SolidColorBrush brush)
            brush.Color = ToColor(color);
        else
            dictionary[key] = new SolidColorBrush(ToColor(color));
    }

    private static void SetGradient5(ResourceDictionary dictionary, string key, Argb c0, Argb c1, Argb c2, Argb c3, Argb c4)
    {
        if (dictionary.TryGetValue(key, out var value) && value is LinearGradientBrush lg && lg.GradientStops.Count == 5)
        {
            lg.GradientStops[0].Color = ToColor(c0);
            lg.GradientStops[1].Color = ToColor(c1);
            lg.GradientStops[2].Color = ToColor(c2);
            lg.GradientStops[3].Color = ToColor(c3);
            lg.GradientStops[4].Color = ToColor(c4);
        }
        else
        {
            dictionary[key] = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = ToColor(c0), Offset = 0.00 },
                    new GradientStop { Color = ToColor(c1), Offset = 0.22 },
                    new GradientStop { Color = ToColor(c2), Offset = 0.50 },
                    new GradientStop { Color = ToColor(c3), Offset = 0.78 },
                    new GradientStop { Color = ToColor(c4), Offset = 1.00 },
                },
            };
        }
    }
}
