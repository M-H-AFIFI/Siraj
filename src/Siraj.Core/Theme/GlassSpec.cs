using Siraj.Core.Sky;

namespace Siraj.Core.Theme;

public sealed record GlassSpec(
    string Finish,
    bool IsDark,
    bool IsBlack,
    Argb CardFillTop,
    Argb CardFillBottom,
    Argb CardHoverFill,
    Argb ElevatedFill,
    Argb PillFill,
    Argb LensBodyTint,
    Argb ChromeLensTint,
    Argb SpecularTopStart,
    Argb SpecularUpperMid,
    Argb SpecularMid,
    Argb SpecularLowerMid,
    Argb SpecularBottomEnd,
    Argb InnerSheen,
    Argb Divider,
    Argb AmbientPrimary,
    Argb AmbientSecondary,
    float BlurAmount,
    float ChromeBlurAmount,
    float TintOpacity,
    float LuminosityOpacity)
{
    private static float Pick(string finish, float clear, float satin, float frosted) => finish switch
    {
        "Satin" => satin,
        "Frosted" => frosted,
        _ => clear,
    };

    public static GlassSpec For(ColorScheme scheme, bool blackMode, string finish)
    {
        string f = finish is "Satin" or "Frosted" ? finish : "Clear";
        var primary = scheme.Primary;
        var secondary = scheme.Secondary;
        var tertiary = scheme.Gold;
        var surface = scheme.Surface;
        var white = Argb.Hex(0xFFFFFFFF);

        if (blackMode && scheme.IsDark)
        {
            var top = Argb.Lerp(Argb.Hex(0xFF1A202E), primary, Pick(f, 0.12f, 0.10f, 0.08f));
            var bot = Argb.Lerp(Argb.Hex(0xFF0A0D14), secondary, 0.06f);
            var elev = Argb.Lerp(Argb.Hex(0xFF14171F), primary, 0.08f);
            var lens = Argb.Lerp(Argb.Hex(0xFF0E131D), primary, Pick(f, 0.09f, 0.12f, 0.09f));
            var chrome = Argb.Lerp(Argb.Hex(0xFF121216), primary, Pick(f, 0.05f, 0.08f, 0.05f));
            return new GlassSpec(
                f, true, true,
                CardFillTop: top.WithAlpha(Pick(f, 0.54f, 0.66f, 0.76f)),
                CardFillBottom: bot.WithAlpha(Pick(f, 0.44f, 0.58f, 0.70f)),
                CardHoverFill: Argb.Lerp(top, white, 0.09f).WithAlpha(Pick(f, 0.66f, 0.75f, 0.84f)),
                ElevatedFill: elev.WithAlpha(Pick(f, 0.72f, 0.80f, 0.88f)),
                PillFill: Argb.Lerp(Argb.Hex(0xFF1C2230), primary, 0.11f).WithAlpha(Pick(f, 0.58f, 0.68f, 0.78f)),
                LensBodyTint: lens.WithAlpha(Pick(f, 0.40f, 0.54f, 0.66f)),
                ChromeLensTint: chrome.WithAlpha(Pick(f, 0.60f, 0.68f, 0.76f)),
                SpecularTopStart: white.WithAlpha(Pick(f, 0.46f, 0.36f, 0.26f)),
                SpecularUpperMid: Argb.Lerp(white, primary, Pick(f, 0.26f, 0.32f, 0.26f)).WithAlpha(Pick(f, 0.22f, 0.17f, 0.12f)),
                SpecularMid: white.WithAlpha(Pick(f, 0.085f, 0.065f, 0.045f)),
                SpecularLowerMid: Argb.Lerp(white, tertiary, 0.35f).WithAlpha(Pick(f, 0.12f, 0.09f, 0.055f)),
                SpecularBottomEnd: Argb.Lerp(secondary, primary, 0.45f).WithAlpha(Pick(f, 0.28f, 0.22f, 0.17f)),
                InnerSheen: white.WithAlpha(Pick(f, 0.14f, 0.11f, 0.06f)),
                Divider: white.WithAlpha(0.09f),
                AmbientPrimary: primary.WithAlpha(Pick(f, 0.16f, 0.135f, 0.11f)),
                AmbientSecondary: secondary.WithAlpha(Pick(f, 0.11f, 0.09f, 0.075f)),
                BlurAmount: Pick(f, 9f, 18f, 30f),
                ChromeBlurAmount: Pick(f, 16f, 24f, 36f),
                TintOpacity: Pick(f, 0.42f, 0.56f, 0.70f),
                LuminosityOpacity: Pick(f, 0.62f, 0.74f, 0.84f));
        }
        if (scheme.IsDark)
        {
            var top = Argb.Lerp(Argb.Lerp(surface, white, 0.10f), primary, Pick(f, 0.10f, 0.085f, 0.065f));
            var bot = Argb.Lerp(Argb.Lerp(surface, scheme.Background, 0.24f), secondary, 0.05f);
            var elev = Argb.Lerp(surface, primary, 0.08f);
            var lens = Argb.Lerp(surface, primary, Pick(f, 0.08f, 0.11f, 0.08f));
            var chrome = Argb.Lerp(Argb.Hex(0xFF141820), primary, Pick(f, 0.06f, 0.09f, 0.06f));
            return new GlassSpec(
                f, true, false,
                CardFillTop: top.WithAlpha(Pick(f, 0.56f, 0.66f, 0.76f)),
                CardFillBottom: bot.WithAlpha(Pick(f, 0.46f, 0.56f, 0.68f)),
                CardHoverFill: Argb.Lerp(top, white, 0.08f).WithAlpha(Pick(f, 0.68f, 0.76f, 0.84f)),
                ElevatedFill: elev.WithAlpha(Pick(f, 0.72f, 0.80f, 0.88f)),
                PillFill: Argb.Lerp(surface, primary, 0.12f).WithAlpha(Pick(f, 0.56f, 0.66f, 0.76f)),
                LensBodyTint: lens.WithAlpha(Pick(f, 0.38f, 0.52f, 0.64f)),
                ChromeLensTint: chrome.WithAlpha(Pick(f, 0.58f, 0.66f, 0.74f)),
                SpecularTopStart: white.WithAlpha(Pick(f, 0.50f, 0.40f, 0.28f)),
                SpecularUpperMid: Argb.Lerp(white, primary, Pick(f, 0.22f, 0.28f, 0.22f)).WithAlpha(Pick(f, 0.24f, 0.18f, 0.13f)),
                SpecularMid: white.WithAlpha(Pick(f, 0.095f, 0.072f, 0.05f)),
                SpecularLowerMid: Argb.Lerp(white, tertiary, 0.32f).WithAlpha(Pick(f, 0.13f, 0.095f, 0.06f)),
                SpecularBottomEnd: Argb.Lerp(secondary, primary, 0.45f).WithAlpha(Pick(f, 0.30f, 0.24f, 0.18f)),
                InnerSheen: white.WithAlpha(Pick(f, 0.16f, 0.12f, 0.07f)),
                Divider: white.WithAlpha(0.10f),
                AmbientPrimary: primary.WithAlpha(Pick(f, 0.22f, 0.19f, 0.16f)),
                AmbientSecondary: secondary.WithAlpha(Pick(f, 0.15f, 0.125f, 0.10f)),
                BlurAmount: Pick(f, 9f, 18f, 30f),
                ChromeBlurAmount: Pick(f, 16f, 24f, 36f),
                TintOpacity: Pick(f, 0.44f, 0.58f, 0.72f),
                LuminosityOpacity: Pick(f, 0.64f, 0.76f, 0.86f));
        }

        var qTop = Argb.Lerp(white, primary, Pick(f, 0.022f, 0.042f, 0.022f));
        var qBot = Argb.Lerp(surface, secondary, 0.04f);
        var qLens = Argb.Lerp(white, primary, Pick(f, 0.022f, 0.048f, 0.024f));
        var qChrome = Argb.Lerp(white, primary, Pick(f, 0.028f, 0.050f, 0.028f));
        return new GlassSpec(
            f, false, false,
            CardFillTop: qTop.WithAlpha(Pick(f, 0.68f, 0.78f, 0.86f)),
            CardFillBottom: qBot.WithAlpha(Pick(f, 0.56f, 0.66f, 0.76f)),
            CardHoverFill: white.WithAlpha(Pick(f, 0.84f, 0.89f, 0.94f)),
            ElevatedFill: white.WithAlpha(Pick(f, 0.78f, 0.84f, 0.90f)),
            PillFill: white.WithAlpha(Pick(f, 0.64f, 0.74f, 0.84f)),
            LensBodyTint: qLens.WithAlpha(Pick(f, 0.48f, 0.62f, 0.74f)),
            ChromeLensTint: qChrome.WithAlpha(Pick(f, 0.62f, 0.72f, 0.82f)),
            SpecularTopStart: white.WithAlpha(0.98f),
            SpecularUpperMid: white.WithAlpha(Pick(f, 0.82f, 0.72f, 0.60f)),
            SpecularMid: Argb.Lerp(white, primary, 0.18f).WithAlpha(Pick(f, 0.42f, 0.34f, 0.26f)),
            SpecularLowerMid: Argb.Lerp(primary, tertiary, 0.45f).WithAlpha(Pick(f, 0.16f, 0.13f, 0.10f)),
            SpecularBottomEnd: Argb.Lerp(secondary, primary, 0.45f).WithAlpha(Pick(f, 0.26f, 0.22f, 0.16f)),
            InnerSheen: white.WithAlpha(Pick(f, 0.65f, 0.52f, 0.36f)),
            Divider: primary.WithAlpha(0.10f),
            AmbientPrimary: primary.WithAlpha(Pick(f, 0.16f, 0.14f, 0.12f)),
            AmbientSecondary: secondary.WithAlpha(Pick(f, 0.12f, 0.10f, 0.085f)),
            BlurAmount: Pick(f, 10f, 18f, 30f),
            ChromeBlurAmount: Pick(f, 16f, 24f, 36f),
            TintOpacity: Pick(f, 0.50f, 0.64f, 0.76f),
            LuminosityOpacity: Pick(f, 0.74f, 0.82f, 0.90f));
    }
}
