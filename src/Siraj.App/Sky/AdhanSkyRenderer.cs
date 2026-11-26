using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Siraj.App.Services;
using Siraj.Core.Sky;
using Windows.UI;

namespace Siraj.App.Sky;

internal static class AdhanSkyRenderer
{
    private static readonly (float X, float Y, float R)[] Stars =
    [
        (0.07f, 0.07f, 1.30f), (0.93f, 0.08f, 1.40f), (0.14f, 0.16f, 1.10f), (0.86f, 0.17f, 1.25f),
        (0.26f, 0.19f, 1.05f), (0.74f, 0.18f, 1.15f), (0.09f, 0.25f, 1.20f), (0.37f, 0.21f, 0.95f),
        (0.58f, 0.19f, 1.30f), (0.90f, 0.27f, 1.40f), (0.18f, 0.32f, 1.15f), (0.81f, 0.31f, 1.10f),
        (0.08f, 0.37f, 1.10f), (0.30f, 0.35f, 1.15f), (0.70f, 0.36f, 1.10f), (0.92f, 0.40f, 1.20f),
        (0.19f, 0.46f, 1.00f), (0.79f, 0.47f, 1.00f), (0.12f, 0.54f, 0.90f), (0.85f, 0.55f, 1.05f),
    ];

    public static void Draw(
        CanvasDrawingSession ds,
        ICanvasResourceCreator rc,
        float w,
        float h,
        AdhanSkySpec spec,
        float centerpieceY,
        double seconds,
        bool sounding,
        bool motion)
    {
        if (w < 1 || h < 1) return;

        bool active = sounding && motion;
        float t = (float)seconds;
        float twinkle = active ? 0.5f - 0.5f * MathF.Cos(t * MathF.PI / 7.5f) : 0.5f;
        float motePhase = active ? (t / 18f) % 1f : 0.32f;
        float wavePhase = active ? (t / 3.6f) % 1f : -1f;
        float breath = active ? 0.5f - 0.5f * MathF.Cos(t * MathF.PI / 3.2f) : 0.5f;

        DrawBackdrop(ds, rc, w, h, spec, twinkle, motePhase);
        DrawCenterpiece(ds, rc, new Vector2(w * 0.5f, centerpieceY), MathF.Min(w, h), spec, wavePhase, breath, sounding);
    }

    private static void DrawBackdrop(
        CanvasDrawingSession ds,
        ICanvasResourceCreator rc,
        float w,
        float h,
        AdhanSkySpec spec,
        float twinkle,
        float motePhase)
    {
        var stops = spec.BuildStops()
            .Select(s => new CanvasGradientStop { Position = s.Position, Color = ThemeService.ToColor(s.Color) })
            .ToArray();
        using (var bg = new CanvasLinearGradientBrush(rc, stops) { StartPoint = Vector2.Zero, EndPoint = new Vector2(0, h) })
        {
            ds.FillRectangle(0, 0, w, h, bg);
        }

        var bloomCenter = new Vector2(w * 0.50f, h * 0.52f);
        using (var bloom = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = ThemeService.ToColor(spec.HorizonGlowColor, spec.HorizonGlowAlpha) },
            new CanvasGradientStop { Position = 0.48f, Color = ThemeService.ToColor(spec.HorizonGlowColor, spec.HorizonGlowAlpha * 0.36f) },
            new CanvasGradientStop { Position = 1.00f, Color = ThemeService.ToColor(spec.HorizonGlowColor, 0f) },
        ]) { Center = bloomCenter, RadiusX = w * 1.04f, RadiusY = w * 0.56f })
        {
            ds.FillEllipse(bloomCenter, w * 1.04f, w * 0.56f, bloom);
        }

        // Subtle dark veil behind white text on bright daytime skies.
        if (spec.IsDaytime)
        {
            var titleAnchor = new Vector2(w * 0.50f, h * 0.56f);
            using var veil = new CanvasRadialGradientBrush(rc, [
                new CanvasGradientStop { Position = 0.00f, Color = Rgba(10, 18, 32, 0.24f) },
                new CanvasGradientStop { Position = 0.55f, Color = Rgba(10, 18, 32, 0.10f) },
                new CanvasGradientStop { Position = 1.00f, Color = Rgba(10, 18, 32, 0f) },
            ]) { Center = titleAnchor, RadiusX = w * 0.90f, RadiusY = w * 0.42f };
            ds.FillEllipse(titleAnchor, w * 0.90f, w * 0.42f, veil);
        }

        float starVis = Math.Clamp(MathF.Max(spec.SkyTheme.StarAlpha, spec.ShowStars ? spec.StarDensity * 0.85f : 0f), 0f, 1f);
        if (starVis > 0.05f)
        {
            for (int i = 0; i < Stars.Length; i++)
            {
                var (px, py, r) = Stars[i];
                float altFade = spec.Warmth > 0.5f
                    ? Math.Clamp((0.38f - py) / 0.26f, 0f, 1f)
                    : Math.Clamp((0.60f - py) / 0.28f, 0f, 1f);
                if (altFade <= 0.02f) continue;

                float wave = MathF.Sin(twinkle * MathF.PI + i * 0.85f);
                float alpha = starVis * altFade * (0.22f + 0.54f * wave * wave);
                float cx = w * px, cy = h * py;
                ds.FillCircle(cx, cy, r, Rgba(255, 255, 255, alpha));
                if (r >= 1.3f && alpha > 0.20f)
                {
                    using var halo = new CanvasRadialGradientBrush(rc, [
                        new CanvasGradientStop { Position = 0f, Color = Rgba(255, 255, 255, alpha * 0.48f) },
                        new CanvasGradientStop { Position = 1f, Color = Rgba(255, 255, 255, 0f) },
                    ]) { Center = new Vector2(cx, cy), RadiusX = r * 3.4f, RadiusY = r * 3.4f };
                    ds.FillCircle(cx, cy, r * 3.4f, halo);
                }
            }
        }

        if (spec.MoteStrength > 0.08f)
        {
            var moteCol = Argb.Lerp(spec.SkyTheme.OrbCore, Argb.Hex(0xFFFFE4A8), 0.5f);
            for (int i = 0; i < 12; i++)
            {
                float seedX = ((i * 73 + 19) % 100) / 100f;
                float seedY = ((i * 41 + 7) % 100) / 100f;
                float life = (motePhase + i * 0.13f) % 1f;
                float mx = w * (0.12f + seedX * 0.76f + 0.04f * MathF.Sin(life * MathF.Tau + i));
                float my = h * (0.14f + ((seedY + 1f - life * 0.35f) % 0.48f));
                float fade = Math.Clamp(MathF.Sin(life * MathF.PI), 0f, 1f);
                float r = 1.2f + (i % 3) * 0.5f;
                ds.FillCircle(mx, my, r, ThemeService.ToColor(moteCol, 0.24f * spec.MoteStrength * fade));
            }
        }
    }

    private static void DrawCenterpiece(
        CanvasDrawingSession ds,
        ICanvasResourceCreator rc,
        Vector2 center,
        float minDim,
        AdhanSkySpec spec,
        float wavePhase,
        float breath,
        bool sounding)
    {
        float orbRadius = Math.Clamp(minDim * 0.062f, 24f, 32f);
        float baseRipple = orbRadius * 1.18f;
        float maxRipple = Math.Clamp(minDim * 0.205f, 78f, 98f);

        var rippleTint = spec.IsDaytime
            ? Argb.Lerp(spec.SkyTheme.OrbCore, Argb.Hex(0xFFFFFFFF), 0.38f)
            : spec.DuskUndersideGlow > 0.5f
                ? Argb.Hex(0xFFFFE0BE)
                : Argb.Hex(0xFFE2E8F0);

        float bloomStrength = 0.90f + 0.20f * breath;
        float glowRadius = maxRipple * (0.96f + 0.06f * breath);
        using (var glow = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = ThemeService.ToColor(spec.SkyTheme.OrbGlow, 0.36f * bloomStrength) },
            new CanvasGradientStop { Position = 0.42f, Color = ThemeService.ToColor(spec.SkyTheme.OrbGlow, 0.14f * bloomStrength) },
            new CanvasGradientStop { Position = 0.74f, Color = ThemeService.ToColor(spec.SkyTheme.OrbGlow, 0.04f * bloomStrength) },
            new CanvasGradientStop { Position = 1.00f, Color = ThemeService.ToColor(spec.SkyTheme.OrbGlow, 0f) },
        ]) { Center = center, RadiusX = glowRadius, RadiusY = glowRadius })
        {
            ds.FillCircle(center, glowRadius, glow);
        }

        for (int i = 0; i < 3; i++)
        {
            float p = wavePhase >= 0f ? (wavePhase + i / 3f) % 1f : (i + 1) / 4f;
            float eased = 1f - (1f - p) * (1f - p) * (1f - p);
            float alpha = wavePhase >= 0f
                ? Math.Clamp(MathF.Sin(p * MathF.PI), 0f, 1f) * 0.22f * (1f - p * 0.32f)
                : (sounding ? 0.11f : 0.06f) * (1f - p);
            float r = baseRipple + (maxRipple - baseRipple) * eased;
            ds.DrawCircle(center, r, ThemeService.ToColor(rippleTint, alpha), 1.05f);
        }

        if (spec.IsDaytime)
        {
            float chromo = orbRadius * 2.15f;
            var midTint = Argb.Lerp(Argb.Hex(0xFFFFFFFF), spec.SkyTheme.OrbGlow, 0.35f);
            using (var ch = new CanvasRadialGradientBrush(rc, [
                new CanvasGradientStop { Position = 0.00f, Color = Rgba(255, 255, 255, 0.90f) },
                new CanvasGradientStop { Position = 0.45f, Color = ThemeService.ToColor(midTint, 0.46f) },
                new CanvasGradientStop { Position = 0.74f, Color = ThemeService.ToColor(spec.SkyTheme.OrbGlow, 0.14f) },
                new CanvasGradientStop { Position = 1.00f, Color = ThemeService.ToColor(spec.SkyTheme.OrbGlow, 0f) },
            ]) { Center = center, RadiusX = chromo, RadiusY = chromo })
            {
                ds.FillCircle(center, chromo, ch);
            }

            var coreEdge = Argb.Lerp(spec.SkyTheme.OrbCore, spec.SkyTheme.OrbGlow, 0.22f);
            using var core = new CanvasRadialGradientBrush(rc, [
                new CanvasGradientStop { Position = 0.00f, Color = Rgba(255, 254, 250, 1f) },
                new CanvasGradientStop { Position = 0.58f, Color = ThemeService.ToColor(spec.SkyTheme.OrbCore) },
                new CanvasGradientStop { Position = 1.00f, Color = ThemeService.ToColor(coreEdge) },
            ])
            {
                Center = new Vector2(center.X - orbRadius * 0.14f, center.Y - orbRadius * 0.14f),
                RadiusX = orbRadius * 1.15f,
                RadiusY = orbRadius * 1.15f,
            };
            ds.FillCircle(center, orbRadius, core);
        }
        else if (spec.IsCrescent)
        {
            using var disc = CanvasGeometry.CreateCircle(rc, center, orbRadius);
            using var cutout = CanvasGeometry.CreateCircle(rc,
                new Vector2(center.X + orbRadius * 0.38f, center.Y - orbRadius * 0.18f),
                orbRadius * 0.86f);
            using var crescent = disc.CombineWith(cutout, Matrix3x2.Identity, CanvasGeometryCombine.Exclude);

            var light = spec.DuskUndersideGlow > 0.5f ? Rgba(255, 247, 230, 1f) : Rgba(248, 250, 252, 1f);
            var shadow = spec.DuskUndersideGlow > 0.5f ? Rgba(255, 214, 153, 1f) : Rgba(203, 213, 225, 1f);
            using var fill = new CanvasLinearGradientBrush(rc, [
                new CanvasGradientStop { Position = 0f, Color = light },
                new CanvasGradientStop { Position = 1f, Color = shadow },
            ])
            {
                StartPoint = new Vector2(center.X - orbRadius, center.Y - orbRadius),
                EndPoint = new Vector2(center.X + orbRadius, center.Y + orbRadius),
            };
            ds.FillGeometry(crescent, fill);
        }
    }

    private static Color Rgba(byte r, byte g, byte b, float a) =>
        Color.FromArgb((byte)Math.Clamp(a * 255f, 0, 255), r, g, b);
}
