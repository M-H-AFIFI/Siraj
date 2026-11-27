using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Siraj.App.Services;
using Siraj.Core.Sky;
using Siraj.Core.Theme;
using Windows.UI;

namespace Siraj.App.Theme;

internal static class LiveSceneRenderer
{
    private const float TwoPi = MathF.PI * 2f;
    private const float DegToRad = MathF.PI / 180f;
    private const float RadToDeg = 180f / MathF.PI;
    private static readonly float[] SoftRadialStops = [0.0f, 0.15f, 0.30f, 0.46f, 0.62f, 0.76f, 0.90f, 1.0f];
    private static float Hypot(float x, float y) => MathF.Sqrt(x * x + y * y);

    public static void DrawBackdrop(
        CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h,
        string sceneId, LiveSceneColors colors, LiveIntensitySpec intensity,
        GlassSpec glass, bool liveEnabled, bool glassEnabled, bool timeSync, int minutesOfDay,
        float t, bool drawVeil = true, string? previousSceneId = null, float crossfade = 1f)
    {
        if (w < 1f || h < 1f) return;
        float k = Math.Clamp(intensity.DetailScale, 0.35f, 1.40f);
        string resolved = LiveScenes.NormalizeId(sceneId);

        if (liveEnabled)
        {
            DrawSkyGradient(ds, rc, w, h, colors);

            bool blending = previousSceneId != null && crossfade is > 0.001f and < 0.999f;
            if (blending)
            {
                string prevResolved = LiveScenes.NormalizeId(previousSceneId);
                using (ds.CreateLayer(1f - crossfade))
                {
                    DrawSceneWithStars(ds, rc, w, h, prevResolved, colors, k, timeSync, minutesOfDay, t);
                }
                using (ds.CreateLayer(crossfade))
                {
                    DrawSceneWithStars(ds, rc, w, h, resolved, colors, k, timeSync, minutesOfDay, t);
                }
            }
            else
            {
                DrawSceneWithStars(ds, rc, w, h, resolved, colors, k, timeSync, minutesOfDay, t);
            }

            if (drawVeil) DrawVeil(ds, rc, w, h, colors, intensity, glassEnabled);
        }
        else
        {
            ds.Clear(ThemeService.ToColor(colors.Veil));
        }

        if (glassEnabled)
        {
            float scale = liveEnabled ? 0.38f : 1.0f;
            DrawAmbientGlassPools(ds, rc, w, h, glass, scale, t);
        }
    }

    private static void DrawSceneWithStars(
        CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h,
        string sceneId, LiveSceneColors colors, float k, bool timeSync, int minutesOfDay, float t)
    {
        if (timeSync && sceneId is not ("NightStars" or "Aurora" or "Space"))
        {
            float starVis = LiveScenes.StarVisibility(minutesOfDay) * (colors.IsDark ? 0.55f : 0.10f);
            if (starVis > 0.02f) DrawSyncedStars(ds, w, h, colors.Highlight, starVis, k, t);
        }

        switch (sceneId)
        {
            case "SeaWaves": DrawSeaWaves(ds, rc, w, h, colors, k, t); break;
            case "Aurora": DrawAurora(ds, rc, w, h, colors, k, t); break;
            case "NightStars": DrawNightStars(ds, rc, w, h, colors, k, t); break;
            case "RosePetals": DrawRosePetals(ds, rc, w, h, colors, k, t); break;
            case "Snow": DrawSnow(ds, rc, w, h, colors, k, t); break;
            case "PurpleButterflies": DrawButterflies(ds, rc, w, h, colors, k, t); break;
            case "Rings": DrawRings(ds, rc, w, h, colors, k, t); break;
            case "Circles": DrawCircles(ds, rc, w, h, colors, k, t); break;
            case "Particles": DrawParticles(ds, rc, w, h, colors, k, t); break;
            case "LavaLamp": DrawLavaLamp(ds, rc, w, h, colors, k, t); break;
            case "Space": DrawSpace(ds, rc, w, h, colors, k, t); break;
            case "Silk": DrawSilk(ds, rc, w, h, colors, k, t); break;
            case "Contours": DrawContours(ds, rc, w, h, colors, k, t); break;
            case "Mosaic": DrawMosaic(ds, rc, w, h, colors, k, t); break;
            case "Waves": DrawWaves(ds, rc, w, h, colors, k, t); break;
            case "Harvest": DrawHarvest(ds, rc, w, h, colors, k, t); break;
            default: DrawDesertDunes(ds, rc, w, h, colors, k, t); break;
        }
    }

    private static void DrawSkyGradient(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c)
    {
        using var sky = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = C(c.SkyTop) },
            new CanvasGradientStop { Position = 0.17f, Color = C(Argb.Lerp(c.SkyTop, c.SkyUpperMid, 0.50f)) },
            new CanvasGradientStop { Position = 0.34f, Color = C(c.SkyUpperMid) },
            new CanvasGradientStop { Position = 0.51f, Color = C(Argb.Lerp(c.SkyUpperMid, c.SkyLowerMid, 0.50f)) },
            new CanvasGradientStop { Position = 0.68f, Color = C(c.SkyLowerMid) },
            new CanvasGradientStop { Position = 0.84f, Color = C(Argb.Lerp(c.SkyLowerMid, c.SkyHorizon, 0.50f)) },
            new CanvasGradientStop { Position = 1.00f, Color = C(c.SkyHorizon) },
        ]) { StartPoint = new Vector2(0, 0), EndPoint = new Vector2(0, h) };
        ds.FillRectangle(0, 0, w, h, sky);
    }

    private static void DrawVeil(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, LiveIntensitySpec intensity, bool glass)
    {
        float adj = intensity.VeilAdjustment;
        float top, mid, bot;
        if (glass)
        {
            top = Math.Clamp((c.IsDark ? 0.20f : 0.14f) + adj, 0.04f, 0.42f);
            mid = Math.Clamp((c.IsDark ? 0.06f : 0.05f) + adj * 0.6f, 0f, 0.26f);
            bot = Math.Clamp((c.IsDark ? 0.24f : 0.16f) + adj, 0.06f, 0.46f);
        }
        else
        {
            top = Math.Clamp((c.IsDark ? 0.30f : 0.24f) + adj, 0.08f, 0.52f);
            mid = Math.Clamp((c.IsDark ? 0.16f : 0.14f) + adj, 0.03f, 0.38f);
            bot = Math.Clamp((c.IsDark ? 0.32f : 0.24f) + adj, 0.10f, 0.54f);
        }
        float upperMid = Math.Clamp(top * 0.44f + mid * 0.56f, 0f, 1f);
        float lowerMid = Math.Clamp(mid * 0.52f + bot * 0.48f, 0f, 1f);

        using var brush = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = Ca(c.Veil, top) },
            new CanvasGradientStop { Position = 0.22f, Color = Ca(c.Veil, upperMid) },
            new CanvasGradientStop { Position = 0.46f, Color = Ca(c.Veil, mid) },
            new CanvasGradientStop { Position = 0.73f, Color = Ca(c.Veil, lowerMid) },
            new CanvasGradientStop { Position = 1.00f, Color = Ca(c.Veil, bot) },
        ]) { StartPoint = new Vector2(0, 0), EndPoint = new Vector2(0, h) };
        ds.FillRectangle(0, 0, w, h, brush);
    }

    private static void DrawSyncedStars(CanvasDrawingSession ds, float w, float h, Argb starColor, float syncedStars, float detail, float t)
    {
        for (int i = 0; i < 22; i++)
        {
            float sx = SceneHash(i, 11) * w;
            float sy = SceneHash(i, 23) * h * 0.55f;
            float twinkle = 0.4f + 0.6f * (0.5f + 0.5f * MathF.Sin(t * 0.85f + i * 1.6f));
            float a = Math.Clamp(syncedStars * twinkle * detail, 0f, 0.75f);
            ds.FillCircle(sx, sy, i % 5 == 0 ? 2.0f : 1.25f, Ca(starColor, a));
        }
    }

    private static void DrawAmbientGlassPools(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, GlassSpec g, float scale, float t)
    {
        float dx = MathF.Sin(t * 0.14f) * w * 0.04f;
        float dy = MathF.Cos(t * 0.11f) * h * 0.03f;
        float r0 = MathF.Max(w, h) * 0.45f;
        float r1 = MathF.Max(w, h) * 0.42f;
        float r2 = MathF.Max(w, h) * 0.34f;
        using var b0 = SoftRadial(rc, g.AmbientPrimary, (g.AmbientPrimary.A / 255f) * scale, r0, g.AmbientPrimary);
        using var b1 = SoftRadial(rc, g.AmbientSecondary, (g.AmbientSecondary.A / 255f) * scale, r1, g.AmbientSecondary);
        using var b2 = SoftRadial(rc, g.AmbientPrimary, (g.AmbientPrimary.A / 255f) * 0.55f * scale, r2, g.AmbientPrimary);
        DrawSoftDisc(ds, b0, w * 0.18f + dx, h * 0.16f + dy, r0);
        DrawSoftDisc(ds, b1, w * 0.84f - dx, h * 0.78f - dy, r1);
        DrawSoftDisc(ds, b2, w * 0.55f + dy, h * 0.42f - dx * 0.5f, r2);
    }

    private readonly record struct LanternFlightSpec(
        float XBase, float Phase, float Speed, float Scale,
        float SwayAmp, float SwayFreq, float FlickerOffset, int Layer);

    private static readonly LanternFlightSpec[] LanternFlights =
    [
        new(0.18f, 0.84f, 0.0046f, 0.44f, 0.012f, 0.26f, 0.4f, 0),
        new(0.56f, 0.92f, 0.0052f, 0.40f, 0.010f, 0.24f, 1.9f, 0),
        new(0.84f, 0.72f, 0.0048f, 0.46f, 0.014f, 0.28f, 3.2f, 0),
        new(0.17f, 0.18f, 0.0068f, 0.78f, 0.018f, 0.31f, 1.1f, 1),
        new(0.83f, 0.34f, 0.0074f, 0.74f, 0.019f, 0.29f, 2.7f, 1),
        new(0.24f, 0.66f, 0.0064f, 0.70f, 0.016f, 0.33f, 4.4f, 1),
        new(0.76f, 0.10f, 0.0088f, 1.04f, 0.021f, 0.27f, 5.1f, 2),
    ];

    private static void DrawDesertDunes(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        var lanternWarm = Argb.Hex(0xFFFFB24D);
        var lanternAmber = Argb.Lerp(lanternWarm, c.Accent, 0.22f);
        var lanternCore = Argb.Hex(0xFFFFEECB);
        var lanternSilkTop = Argb.Lerp(Argb.Hex(0xFFC85D2C), c.Accent, 0.20f);
        var lanternSilkBottom = Argb.Hex(0xFFFFD07B);

        var starTint = Argb.Lerp(c.Highlight, lanternCore, 0.18f);
        float starAlphaScale = isDark ? 0.50f : 0.16f;

        for (int i = 0; i < 18; i++)
        {
            float xn = 0.05f + 0.90f * SceneHash(i, 11);
            float yn = 0.04f + 0.34f * SceneHash(i, 23);
            float r = minDim * (0.0014f + 0.0018f * SceneHash(i, 37));
            float phase = SceneHash(i, 53) * TwoPi;
            float twinkle = 0.48f + 0.52f * MathF.Sin(t * 0.65f + phase);
            float calm = SceneCalmMask(yn, 0.48f, 0.22f, 0.72f);
            float a = starAlphaScale * twinkle * calm * k;
            if (a > 0.01f) ds.FillCircle(w * xn, h * yn, r, Ca(starTint, a));
        }

        float bokehBrushR = MathF.Max(1f, minDim * 0.040f);
        using (var bokehBrush = SoftRadial(rc, lanternAmber, (isDark ? 0.16f : 0.12f) * k, bokehBrushR, lanternSilkTop))
        {
            for (int i = 0; i < 4; i++)
            {
                float bx = 0.10f + 0.80f * SceneHash(i, 71);
                float bp = SceneHash(i, 89);
                float bs = 0.0020f + 0.0018f * SceneHash(i, 101);
                float br = minDim * (0.016f + 0.022f * SceneHash(i, 113));
                float drift = (bp + t * bs) % 1.15f;
                float yn = 0.28f - drift * 0.26f;
                float xn = bx + 0.014f * MathF.Sin(t * 0.22f + i * 1.4f);
                float pulse = 0.68f + 0.32f * MathF.Sin(t * 0.35f + i * 2.1f);
                float sc = br / bokehBrushR;
                DrawSoftDisc(ds, bokehBrush, w * xn, h * yn, bokehBrushR, sc, sc, 0f, pulse);
            }
        }

        using (var mistBrush = VerticalGrad(rc, h * 0.68f, h * 0.92f, [
            (0.00f, Colors.Transparent),
            (0.48f, Ca(Argb.Lerp(lanternAmber, c.SkyHorizon, 0.55f), (isDark ? 0.10f : 0.14f) * k)),
            (1.00f, Colors.Transparent),
        ]))
        {
            ds.FillRectangle(0, h * 0.68f, w, h * 0.24f, mistBrush);
        }

        BuildRidgeAndRibbon(rc, w, h, 72, h * 0.012f, 0.14f, 0.15f,
            xn => h * 0.77f + h * 0.036f * MathF.Sin(xn * 3.1f + 0.45f) + h * 0.016f * MathF.Cos(xn * 6.4f + 1.15f),
            out var farDune, out var farRibbon);
        var crestColor = Argb.Lerp(lanternAmber, c.Highlight, 0.35f);
        using (farDune)
        using (farRibbon)
        using (var farDuneBrush = VerticalGrad(rc, h * 0.71f, h, [
            (0f, Ca(Argb.Lerp(c.Shade, lanternAmber, isDark ? 0.14f : 0.18f), (isDark ? 0.78f : 0.40f) * k)),
            (1f, Ca(Argb.Lerp(c.Ink, c.Shade, 0.35f), (isDark ? 0.90f : 0.56f) * k)),
        ]))
        using (var farRibBrush = VerticalGrad(rc, h * 0.71f, h * 0.84f, [
            (0f, Ca(crestColor, (isDark ? 0.18f : 0.22f) * k)),
            (1f, Colors.Transparent),
        ]))
        {
            ds.FillGeometry(farDune, farDuneBrush);
            ds.FillGeometry(farRibbon, farRibBrush);
        }

        for (int idx = 0; idx < LanternFlights.Length; idx++)
        {
            var spec = LanternFlights[idx];
            float cycle = (spec.Phase + t * spec.Speed) % 1.24f;
            float yn = 0.94f - cycle * 0.90f;
            float xn = spec.XBase + spec.SwayAmp * MathF.Sin(t * spec.SwayFreq + spec.FlickerOffset);
            float edgeFade = yn > 0.88f
                ? Math.Clamp((0.95f - yn) / 0.07f, 0f, 1f)
                : yn < 0.08f
                    ? Math.Clamp((yn + 0.04f) / 0.12f, 0f, 1f)
                    : 1f;
            float calm = SceneCalmMask(yn, 0.48f, 0.25f, 0.66f);
            float masterAlpha = Math.Clamp(edgeFade * calm, 0f, 1f);
            if (masterAlpha <= 0.015f) continue;

            float flicker = 0.88f + 0.12f * MathF.Sin(t * 1.15f + spec.FlickerOffset) * MathF.Cos(t * 0.68f + spec.FlickerOffset * 0.6f);
            float tiltRad = 2.4f * MathF.Cos(t * spec.SwayFreq + spec.FlickerOffset) * DegToRad;

            float lw = MathF.Max(2f, minDim * 0.032f * spec.Scale);
            float lh = lw * 1.44f;
            float topR = lw * 0.52f, waistR = lw * 0.60f, baseR = lw * 0.34f;
            float topY = -lh * 0.50f, shoulderY = -lh * 0.28f, waistY = lh * 0.04f, botY = lh * 0.48f;

            float envAlpha = spec.Layer switch { 0 => 0.55f, 1 => 0.84f, _ => 0.92f };
            float haloPeak = (spec.Layer switch
            {
                0 => isDark ? 0.22f : 0.16f,
                1 => isDark ? 0.34f : 0.24f,
                _ => isDark ? 0.40f : 0.28f,
            }) * k;
            float haloR = lw * 3.1f;

            using var envPb = new CanvasPathBuilder(rc);
            envPb.BeginFigure(0f, topY);
            envPb.AddCubicBezier(new Vector2(topR * 0.88f, topY), new Vector2(waistR, shoulderY), new Vector2(waistR, waistY));
            envPb.AddCubicBezier(new Vector2(waistR, lh * 0.27f), new Vector2(baseR * 1.18f, botY * 0.90f), new Vector2(baseR, botY));
            envPb.AddLine(-baseR, botY);
            envPb.AddCubicBezier(new Vector2(-baseR * 1.18f, botY * 0.90f), new Vector2(-waistR, lh * 0.27f), new Vector2(-waistR, waistY));
            envPb.AddCubicBezier(new Vector2(-waistR, shoulderY), new Vector2(-topR * 0.88f, topY), new Vector2(0f, topY));
            envPb.EndFigure(CanvasFigureLoop.Closed);
            using var envGeo = CanvasGeometry.CreatePath(envPb);

            using var haloBrush = SoftRadial(rc, lanternAmber, haloPeak, haloR, lanternSilkTop);
            using var envBrush = VerticalGrad(rc, topY, botY, [
                (0.00f, Ca(lanternSilkTop, envAlpha * 0.72f)),
                (0.38f, Ca(lanternAmber, envAlpha * 0.90f)),
                (0.78f, Ca(lanternSilkBottom, envAlpha)),
                (1.00f, Ca(lanternCore, envAlpha * 0.95f)),
            ]);
            float innerY = lh * 0.20f, innerR = lw * 0.78f;
            using var innerBrush = SoftRadial(rc, lanternCore, isDark ? 0.62f : 0.50f, innerR, lanternWarm);
            float mouthY = botY * 0.96f, mouthR = baseR * 1.45f;
            using var mouthBrush = SoftRadial(rc, lanternCore, 0.52f, mouthR, lanternAmber);

            var saved = ds.Transform;
            ds.Transform = Matrix3x2.CreateRotation(tiltRad) * Matrix3x2.CreateTranslation(w * xn, h * yn) * saved;

            float glowA = Math.Clamp(masterAlpha * flicker, 0f, 1f);
            haloBrush.Opacity = glowA;
            ds.FillCircle(0f, 0f, haloR, haloBrush);

            envBrush.Opacity = masterAlpha;
            ds.FillGeometry(envGeo, envBrush);

            innerBrush.Opacity = glowA;
            innerBrush.Center = new Vector2(0f, innerY);
            ds.FillCircle(0f, innerY, innerR, innerBrush);

            var saved2 = ds.Transform;
            ds.Transform = Matrix3x2.CreateScale(1.35f, 0.52f) * Matrix3x2.CreateTranslation(0f, mouthY) * saved2;
            mouthBrush.Opacity = glowA;
            ds.FillCircle(0f, 0f, mouthR, mouthBrush);
            ds.Transform = saved2;

            ds.FillCircle(0f, mouthY * 0.86f, lw * 0.14f, Ca(lanternCore, glowA * 0.78f));
            ds.Transform = saved;
        }

        BuildRidgeAndRibbon(rc, w, h, 72, h * 0.014f, 0.14f, 0.15f,
            xn => h * 0.855f + h * 0.032f * MathF.Sin(xn * 2.5f + 2.35f) + h * 0.015f * MathF.Sin(xn * 5.7f - 0.55f),
            out var nearDune, out var nearRibbon);
        using (nearDune)
        using (nearRibbon)
        using (var nearDuneBrush = VerticalGrad(rc, h * 0.80f, h, [
            (0f, Ca(Argb.Lerp(c.Ink, c.Shade, 0.42f), (isDark ? 0.92f : 0.58f) * k)),
            (1f, Ca(c.Ink, (isDark ? 0.97f : 0.72f) * k)),
        ]))
        using (var nearRibBrush = VerticalGrad(rc, h * 0.80f, h * 0.92f, [
            (0f, Ca(crestColor, (isDark ? 0.14f : 0.18f) * k)),
            (1f, Colors.Transparent),
        ]))
        {
            ds.FillGeometry(nearDune, nearDuneBrush);
            ds.FillGeometry(nearRibbon, nearRibBrush);
        }

        float ffR = MathF.Max(1f, minDim * 0.017f);
        using var ffBrush = SoftRadial(rc, lanternCore, (isDark ? 0.42f : 0.28f) * k, ffR, lanternWarm);
        for (int i = 0; i < 10; i++)
        {
            float fx0 = 0.07f + 0.86f * SceneHash(i, 131);
            float fy0 = 0.68f + 0.22f * SceneHash(i, 149);
            float fp = SceneHash(i, 163) * TwoPi;
            float fx = w * (fx0 + 0.018f * MathF.Sin(t * 0.24f + fp));
            float fy = h * (fy0 + 0.014f * MathF.Cos(t * 0.30f + fp * 1.2f));
            float breath = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(t * 0.72f + fp));
            float calm = SceneCalmMask(fy0, 0.48f, 0.24f, 0.60f);
            float a = Math.Clamp(breath * calm, 0f, 1f);
            if (a > 0.02f)
            {
                DrawSoftDisc(ds, ffBrush, fx, fy, ffR, 1f, 1f, 0f, a);
                ds.FillCircle(fx, fy, minDim * 0.0022f, Ca(lanternCore, a * 0.62f * k));
            }
        }
    }

    private readonly record struct OceanSwellSpec(
        float DepthFraction, float AmpRatio, float FreqA, float SpeedA,
        float FreqB, float SpeedB, float PhaseOffset);

    private static readonly OceanSwellSpec[] OceanSwells =
    [
        new(0.00f, 0.0040f, 3.4f, 0.24f, 6.2f, -0.16f, 0.2f),
        new(0.11f, 0.0058f, 3.0f, 0.30f, 5.5f, -0.20f, 1.3f),
        new(0.24f, 0.0078f, 2.6f, 0.36f, 4.9f, -0.24f, 2.6f),
        new(0.39f, 0.0102f, 2.3f, 0.42f, 4.3f, -0.28f, 3.9f),
        new(0.56f, 0.0128f, 2.0f, 0.48f, 3.8f, -0.32f, 0.9f),
        new(0.74f, 0.0150f, 1.75f, 0.54f, 3.3f, -0.36f, 2.2f),
        new(0.90f, 0.0168f, 1.50f, 0.60f, 2.9f, -0.40f, 4.4f),
    ];

    private static void DrawSeaWaves(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        float horizonY = h * 0.58f;
        float waterDepth = h - horizonY;
        float orbRestX = w * 0.52f;
        float orbRestY = h * 0.42f;
        float orbDriftX = orbRestX + w * 0.015f * MathF.Sin(t * 0.22f);

        float orbRadius = minDim * 0.115f;
        float haloRadius = minDim * 0.36f;
        var orbColor = Argb.Lerp(c.Highlight, c.Accent, 0.32f);

        using (var haloBrush = SoftRadial(rc, orbColor, (isDark ? 0.16f : 0.24f) * k, haloRadius, c.SkyLowerMid))
            DrawSoftDisc(ds, haloBrush, orbDriftX, orbRestY, haloRadius);

        using (var orbBrush = VerticalGrad(rc, orbRestY - orbRadius, orbRestY + orbRadius * 0.9f, [
            (0.00f, Ca(c.Highlight, (isDark ? 0.24f : 0.36f) * k)),
            (0.65f, Ca(Argb.Lerp(c.Accent, c.Highlight, 0.45f), (isDark ? 0.12f : 0.20f) * k)),
            (1.00f, Colors.Transparent),
        ]))
            ds.FillCircle(orbDriftX, orbRestY, orbRadius, orbBrush);

        using (var mistBrush = VerticalGrad(rc, horizonY - h * 0.07f, horizonY + h * 0.08f, [
            (0.00f, Colors.Transparent),
            (0.45f, Ca(Argb.Lerp(c.SkyHorizon, orbColor, 0.25f), (isDark ? 0.14f : 0.22f) * k)),
            (1.00f, Colors.Transparent),
        ]))
            ds.FillRectangle(0f, horizonY - h * 0.07f, w, h * 0.15f, mistBrush);

        float reflR = minDim * 0.32f;
        float glowPulse = 0.88f + 0.12f * MathF.Sin(t * 0.72f);
        using (var reflBrush = SoftRadial(rc, Argb.Lerp(c.Highlight, c.Accent, 0.36f), (isDark ? 0.14f : 0.20f) * k, reflR, c.SkyHorizon))
            DrawSoftDisc(ds, reflBrush, orbDriftX, horizonY + waterDepth * 0.30f, reflR, 0.72f, 1.15f, 0f, glowPulse);

        var farWater = Argb.Lerp(c.Accent, c.SkyHorizon, 0.38f);
        var midWater = Argb.Lerp(c.Shade, c.Accent, 0.44f);
        var deepWater = Argb.Lerp(c.Ink, c.Shade, 0.28f);

        const int steps = 56;
        float SwellY(OceanSwellSpec spec, float xn)
        {
            float baseY = horizonY + waterDepth * spec.DepthFraction;
            float amp = h * spec.AmpRatio;
            float p1 = MathF.Sin(xn * TwoPi * spec.FreqA + t * spec.SpeedA + spec.PhaseOffset);
            float p2 = MathF.Cos(xn * TwoPi * spec.FreqB - t * spec.SpeedB + spec.PhaseOffset * 0.6f);
            return baseY + amp * (0.68f * p1 + 0.32f * p2);
        }

        for (int b = 0; b < OceanSwells.Length; b++)
        {
            var spec = OceanSwells[b];
            float f = spec.DepthFraction;
            float crestY = horizonY + waterDepth * f - h * spec.AmpRatio * 1.4f;
            float fadeBot = MathF.Min(h, horizonY + waterDepth * (f + 0.32f));
            var topCol = Argb.Lerp(farWater, deepWater, f * 0.72f);
            var midCol = Argb.Lerp(midWater, deepWater, f * 0.85f);
            float topA = Math.Clamp((isDark ? 0.16f + f * 0.22f : 0.20f + f * 0.22f) * k, 0f, 0.72f);
            float botA = Math.Clamp((isDark ? 0.34f + f * 0.34f : 0.30f + f * 0.32f) * k, 0f, 0.90f);

            using var pb = new CanvasPathBuilder(rc);
            var trace = new SmoothTrace(pb);
            float x0 = -w * 0.03f;
            float xLast = w * 1.03f;
            for (int s = 0; s <= steps; s++)
            {
                float x = x0 + w * 1.06f * (s / (float)steps);
                float y = SwellY(spec, x / w);
                if (s == 0) trace.Start(x, y);
                else trace.To(x, y);
            }
            trace.End();
            pb.AddLine(xLast, h);
            pb.AddLine(x0, h);
            pb.EndFigure(CanvasFigureLoop.Closed);

            using var geo = CanvasGeometry.CreatePath(pb);
            using var brush = VerticalGrad(rc, crestY, b >= OceanSwells.Length - 2 ? h : fadeBot, [
                (0.00f, Ca(topCol, topA)),
                (0.45f, Ca(midCol, Math.Clamp((topA + botA) * 0.55f, 0f, 0.82f))),
                (1.00f, Ca(deepWater, botA)),
            ]);
            ds.FillGeometry(geo, brush);
        }

        float sparkleR = minDim * 0.0026f;
        var sparkleColor = Argb.Lerp(c.Highlight, c.Accent, 0.22f);
        for (int i = 0; i < 12; i++)
        {
            int swellIdx = Math.Min(2 + (i % 4), OceanSwells.Length - 1);
            float xn = 0.14f + SceneHash(i, 97) * 0.72f;
            float cy = SwellY(OceanSwells[swellIdx], xn);
            float flash = SoftAbs(MathF.Sin(t * 1.15f + SceneHash(i, 103) * TwoPi));
            float a = Math.Clamp(flash * flash * 0.42f * k, 0f, 0.48f);
            if (a > 0.03f) ds.FillCircle(xn * w, cy, sparkleR * (0.7f + 0.5f * flash), Ca(sparkleColor, a));
        }
    }

    private readonly record struct AuroraCurtainSpec(
        float BaseHemY, float WaveAmp, float FreqA, float SpeedA,
        float FreqB, float SpeedB, float Phase, float AlphaScale);

    private static readonly AuroraCurtainSpec[] AuroraCurtains =
    [
        new(0.28f, 0.042f, 1.6f, 0.14f, 3.1f, -0.09f, 0.2f, 1.00f),
        new(0.37f, 0.036f, 2.0f, -0.11f, 3.7f, 0.13f, 1.8f, 0.82f),
    ];

    private static void DrawAurora(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        var emerald = Argb.Lerp(Argb.Hex(0xFF3EF0B4), c.Accent, 0.24f);
        var cyan = Argb.Lerp(Argb.Hex(0xFF42D6E8), c.Highlight, 0.20f);
        var violet = Argb.Lerp(Argb.Hex(0xFF9664F0), c.Secondary, 0.30f);

        var starColor = Argb.Lerp(c.Highlight, cyan, 0.22f);
        float starBaseA = isDark ? 0.56f : 0.14f;
        for (int i = 0; i < 24; i++)
        {
            float xn = 0.04f + 0.92f * SceneHash(i, 101);
            float yn = 0.03f + 0.44f * SceneHash(i, 113);
            float r = minDim * (0.0014f + 0.0018f * SceneHash(i, 127));
            float phase = SceneHash(i, 139) * TwoPi;
            float tw = 0.45f + 0.55f * MathF.Sin(t * 0.60f + phase);
            float calm = SceneCalmMask(yn, 0.48f, 0.22f, 0.70f);
            float a = starBaseA * tw * calm * k;
            if (a > 0.01f) ds.FillCircle(w * xn, h * yn, r, Ca(starColor, a));
        }

        float skyGlowR = minDim * 0.65f;
        using (var skyGlow = SoftRadial(rc, violet, (isDark ? 0.15f : 0.12f) * k, skyGlowR, emerald))
        {
            float bloomX = w * (0.48f + 0.04f * MathF.Sin(t * 0.07f));
            DrawSoftDisc(ds, skyGlow, bloomX, h * 0.18f, skyGlowR, 1.4f, 0.72f, 0f, 0.88f + 0.12f * MathF.Cos(t * 0.11f));
        }

        const int samples = 44;
        float[][] hemYs = [new float[samples + 1], new float[samples + 1]];
        float[] featherOffsets = [-h * 0.016f, -h * 0.0053f, h * 0.0053f, h * 0.016f];
        float[] featherWeights = [0.16f, 0.34f, 0.34f, 0.16f];
        float offscreenTop = -h * 0.12f;

        using var curtainBrush0 = VerticalGrad(rc, h * 0.02f, h * 0.36f, [
            (0.00f, Colors.Transparent),
            (0.26f, Ca(violet, (isDark ? 0.14f : 0.10f) * k)),
            (0.68f, Ca(cyan, (isDark ? 0.24f : 0.16f) * k)),
            (1.00f, Ca(emerald, (isDark ? 0.34f : 0.22f) * k)),
        ]);
        using var curtainBrush1 = VerticalGrad(rc, h * 0.08f, h * 0.45f, [
            (0.00f, Colors.Transparent),
            (0.30f, Ca(violet, (isDark ? 0.12f : 0.08f) * k)),
            (0.72f, Ca(emerald, (isDark ? 0.22f : 0.14f) * k)),
            (1.00f, Ca(cyan, (isDark ? 0.30f : 0.18f) * k)),
        ]);
        CanvasLinearGradientBrush[] curtainBrushes = [curtainBrush0, curtainBrush1];

        for (int ci = 0; ci < AuroraCurtains.Length; ci++)
        {
            var spec = AuroraCurtains[ci];
            float baseY = h * spec.BaseHemY;
            float amp = h * spec.WaveAmp;
            var ys = hemYs[ci];
            using var pb = new CanvasPathBuilder(rc);
            var trace = new SmoothTrace(pb);
            for (int s = 0; s <= samples; s++)
            {
                float xn = s / (float)samples;
                float w1 = MathF.Sin(xn * TwoPi * spec.FreqA + t * spec.SpeedA + spec.Phase);
                float w2 = MathF.Cos(xn * TwoPi * spec.FreqB + t * spec.SpeedB + spec.Phase * 1.3f);
                float arch = -h * 0.024f * MathF.Sin(xn * MathF.PI);
                ys[s] = baseY + arch + amp * (0.64f * w1 + 0.36f * w2);
                if (s == 0) trace.Start(w * xn, ys[s]);
                else trace.To(w * xn, ys[s]);
            }
            trace.End();
            pb.AddLine(w, offscreenTop);
            pb.AddLine(0f, offscreenTop);
            pb.EndFigure(CanvasFigureLoop.Closed);
            using var geo = CanvasGeometry.CreatePath(pb);

            var saved = ds.Transform;
            for (int p = 0; p < 4; p++)
            {
                ds.Transform = Matrix3x2.CreateTranslation(0f, featherOffsets[p]) * saved;
                curtainBrushes[ci].Opacity = Math.Clamp(featherWeights[p] * spec.AlphaScale, 0f, 1f);
                ds.FillGeometry(geo, curtainBrushes[ci]);
            }
            ds.Transform = saved;
        }

        const float rayUnitR = 80f;
        using (var rayEm = SoftRadial(rc, emerald, (isDark ? 0.20f : 0.14f) * k, rayUnitR, violet))
        using (var rayCy = SoftRadial(rc, cyan, (isDark ? 0.17f : 0.12f) * k, rayUnitR, emerald))
        {
            for (int i = 0; i < 12; i++)
            {
                float rx0 = 0.06f + 0.88f * (i / 11f) + (SceneHash(i, 19) - 0.5f) * 0.05f;
                int cIdx = i % 2;
                float halfW = w * (0.048f + 0.042f * SceneHash(i, 31));
                float halfH = h * (0.095f + 0.065f * SceneHash(i, 43));
                float phase = SceneHash(i, 61) * TwoPi;
                float speed = 0.28f + 0.22f * SceneHash(i, 79);

                float xn = Math.Clamp(rx0 + 0.022f * MathF.Sin(t * 0.09f + phase), 0.02f, 0.98f);
                float samplePos = xn * samples;
                int s0 = Math.Clamp((int)samplePos, 0, samples - 1);
                float frac = samplePos - s0;
                float hemY = hemYs[cIdx][s0] * (1f - frac) + hemYs[cIdx][s0 + 1] * frac;
                float cy = hemY - halfH * 0.58f;
                float calm = SceneCalmMask(cy / h, 0.50f, 0.22f, 0.65f);
                float shimmer = 0.45f + 0.55f * (0.5f + 0.5f * MathF.Sin(t * speed + phase));
                float a = Math.Clamp(shimmer * calm * 0.85f, 0f, 1f);
                if (a > 0.015f)
                    DrawSoftDisc(ds, i % 2 == 0 ? rayEm : rayCy, w * xn, cy, rayUnitR, halfW / rayUnitR, halfH / rayUnitR, 0f, a);
            }
        }

        BuildRidgeAndRibbon(rc, w, h, 96, h * 0.011f, 0.12f, 0.15f,
            xn => h * 0.81f
                - h * 0.070f * MathF.Exp(-((xn - 0.24f) * (xn - 0.24f)) / 0.016f)
                - h * 0.085f * MathF.Exp(-((xn - 0.72f) * (xn - 0.72f)) / 0.022f)
                + h * 0.018f * MathF.Sin(xn * 5.2f + 0.7f) + h * 0.008f * MathF.Cos(xn * 11.4f),
            out var farMount, out var farRib);
        BuildRidgeAndRibbon(rc, w, h, 96, h * 0.013f, 0.12f, 0.15f,
            xn => h * 0.88f
                - h * 0.058f * MathF.Exp(-((xn - 0.48f) * (xn - 0.48f)) / 0.020f)
                - h * 0.042f * MathF.Exp(-((xn - 0.08f) * (xn - 0.08f)) / 0.018f)
                + h * 0.014f * MathF.Sin(xn * 4.4f + 2.1f),
            out var nearMount, out var nearRib);

        var peakHi = Argb.Lerp(emerald, cyan, 0.45f);
        using (farMount)
        using (farRib)
        using (nearMount)
        using (nearRib)
        using (var farBrush = VerticalGrad(rc, h * 0.70f, h, [
            (0f, Ca(Argb.Lerp(c.Shade, cyan, isDark ? 0.10f : 0.14f), (isDark ? 0.78f : 0.42f) * k)),
            (1f, Ca(Argb.Lerp(c.Ink, c.Shade, 0.3f), (isDark ? 0.92f : 0.58f) * k)),
        ]))
        using (var farRibBrush = VerticalGrad(rc, h * 0.69f, h * 0.83f, [
            (0f, Ca(peakHi, (isDark ? 0.19f : 0.22f) * k)),
            (1f, Colors.Transparent),
        ]))
        using (var nearBrush = VerticalGrad(rc, h * 0.80f, h, [
            (0f, Ca(Argb.Lerp(c.Ink, c.Shade, 0.35f), (isDark ? 0.92f : 0.56f) * k)),
            (1f, Ca(c.Ink, (isDark ? 0.98f : 0.72f) * k)),
        ]))
        using (var nearRibBrush = VerticalGrad(rc, h * 0.79f, h * 0.92f, [
            (0f, Ca(Argb.Lerp(peakHi, c.Highlight, 0.25f), (isDark ? 0.14f : 0.18f) * k)),
            (1f, Colors.Transparent),
        ]))
        {
            ds.FillGeometry(farMount, farBrush);
            ds.FillGeometry(farRib, farRibBrush);
            ds.FillGeometry(nearMount, nearBrush);
            ds.FillGeometry(nearRib, nearRibBrush);
        }

        float moteR = MathF.Max(1f, minDim * 0.013f);
        using var moteBrush = SoftRadial(rc, Argb.Lerp(cyan, c.Highlight, 0.45f), (isDark ? 0.18f : 0.15f) * k, moteR, emerald);
        for (int i = 0; i < 8; i++)
        {
            float mx0 = 0.08f + 0.84f * SceneHash(i, 151);
            float my0 = 0.66f + 0.22f * SceneHash(i, 167);
            float mp = SceneHash(i, 181) * TwoPi;
            float mx = w * (mx0 + 0.012f * MathF.Sin(t * 0.20f + mp));
            float yn = my0 + 0.010f * MathF.Cos(t * 0.18f + mp * 1.1f);
            float breath = 0.40f + 0.60f * (0.5f + 0.5f * MathF.Sin(t * 0.45f + mp));
            float calm = SceneCalmMask(yn, 0.48f, 0.24f, 0.65f);
            float a = Math.Clamp(breath * calm * 0.78f, 0f, 1f);
            if (a > 0.015f) DrawSoftDisc(ds, moteBrush, mx, h * yn, moteR, 1f, 1f, 0f, a);
        }
    }

    private static void DrawNightStars(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        var starWhite = Argb.Lerp(c.Highlight, Argb.Hex(0xFFFFFFFF), 0.40f);
        var starWarm = Argb.Lerp(c.Highlight, Argb.Hex(0xFFFFDFB0), 0.35f);
        var starCool = Argb.Lerp(c.Highlight, c.Accent, 0.32f);
        var nebulaPrimary = Argb.Lerp(c.Accent, c.Secondary, 0.42f);

        float milkyR = MathF.Max(1f, minDim * 0.24f);
        ReadOnlySpan<float> milkyXn = [0.14f, 0.32f, 0.50f, 0.68f, 0.85f];
        ReadOnlySpan<float> milkyYn = [0.32f, 0.26f, 0.20f, 0.14f, 0.09f];
        ReadOnlySpan<float> milkySx = [1.45f, 1.65f, 1.55f, 1.40f, 1.30f];
        ReadOnlySpan<float> milkySy = [0.62f, 0.68f, 0.60f, 0.56f, 0.52f];
        float milkyBreath = 0.92f + 0.08f * MathF.Sin(t * 0.09f);
        using (var milkyA = SoftRadial(rc, nebulaPrimary, (isDark ? 0.11f : 0.08f) * k, milkyR, c.SkyUpperMid))
        using (var milkyB = SoftRadial(rc, Argb.Lerp(c.Highlight, nebulaPrimary, 0.55f), (isDark ? 0.09f : 0.06f) * k, milkyR, nebulaPrimary))
        {
            for (int i = 0; i < 5; i++)
            {
                float cx = w * (milkyXn[i] + 0.008f * MathF.Sin(t * 0.06f + i * 1.1f));
                DrawSoftDisc(ds, i % 2 == 0 ? milkyA : milkyB, cx, h * milkyYn[i], milkyR, milkySx[i], milkySy[i], -22f, milkyBreath);
            }
        }

        float baseSkyAlpha = isDark ? 0.64f : 0.18f;
        for (int i = 0; i < 42; i++)
        {
            float xn = 0.03f + 0.94f * SceneHash(i, 13);
            float yn = 0.02f + 0.68f * SceneHash(i, 29);
            float r = minDim * (0.0011f + 0.0013f * SceneHash(i, 41));
            float phase = SceneHash(i, 59) * TwoPi;
            float speed = 0.30f + 0.35f * SceneHash(i, 73);
            float depthAtten = yn > 0.48f ? Math.Clamp(1f - (yn - 0.48f) / 0.24f, 0f, 1f) : 1f;
            float calm = SceneCalmMask(yn, 0.50f, 0.24f, 0.74f);
            float tw = 0.52f + 0.48f * MathF.Sin(t * speed + phase);
            float a = baseSkyAlpha * tw * depthAtten * calm * k;
            if (a > 0.01f) ds.FillCircle(w * xn, h * yn, r, Ca(starWhite, a));
        }

        for (int i = 0; i < 22; i++)
        {
            float xn = 0.04f + 0.92f * SceneHash(i, 97);
            float yn = 0.03f + 0.62f * SceneHash(i, 113);
            float r = minDim * (0.0018f + 0.0016f * SceneHash(i, 131));
            float phase = SceneHash(i, 149) * TwoPi;
            float speed = 0.38f + 0.40f * SceneHash(i, 163);
            float warmth = SceneHash(i, 179);
            float calm = SceneCalmMask(yn, 0.50f, 0.24f, 0.76f);
            float tw = 0.45f + 0.55f * MathF.Sin(t * speed + phase);
            float a = baseSkyAlpha * 1.15f * tw * calm * k;
            if (a > 0.01f) ds.FillCircle(w * xn, h * yn, r, Ca(Argb.Lerp(starCool, starWarm, warmth), a));
        }

        float heroR = MathF.Max(1f, minDim * 0.022f);
        using (var heroWarm = SoftRadial(rc, starWarm, (isDark ? 0.42f : 0.24f) * k, heroR, c.Accent))
        using (var heroCool = SoftRadial(rc, starCool, (isDark ? 0.40f : 0.22f) * k, heroR, nebulaPrimary))
        {
            for (int i = 0; i < 7; i++)
            {
                float xn = 0.08f + 0.72f * SceneHash(i, 193);
                float yn = 0.05f + 0.36f * SceneHash(i, 211);
                float phase = SceneHash(i, 229) * TwoPi;
                bool warm = SceneHash(i, 241) > 0.5f;
                float calm = SceneCalmMask(yn, 0.50f, 0.24f, 0.70f);
                float pulse = 0.58f + 0.42f * MathF.Sin(t * 0.48f + phase);
                float sc = 0.85f + 0.25f * pulse;
                DrawSoftDisc(ds, warm ? heroWarm : heroCool, w * xn, h * yn, heroR, sc, sc, 0f, pulse * calm);
                float coreA = (isDark ? 0.82f : 0.40f) * pulse * calm * k;
                if (coreA > 0.01f) ds.FillCircle(w * xn, h * yn, minDim * 0.0024f, Ca(starWhite, coreA));
            }
        }

        var moonCenter = new Vector2(w * 0.78f, h * 0.15f);
        float moonR = MathF.Max(2f, minDim * 0.044f);
        float moonPulse = 0.94f + 0.06f * MathF.Sin(t * 0.16f);

        using (var outerHalo = SoftRadial(rc, starWarm, (isDark ? 0.16f : 0.18f) * k, moonR * 5.6f, c.SkyUpperMid))
            DrawSoftDisc(ds, outerHalo, moonCenter.X, moonCenter.Y, moonR * 5.6f, 1f, 1f, 0f, moonPulse);
        using (var innerHalo = SoftRadial(rc, starWhite, (isDark ? 0.24f : 0.26f) * k, moonR * 2.2f, starWarm))
            DrawSoftDisc(ds, innerHalo, moonCenter.X, moonCenter.Y, moonR * 2.2f, 1f, 1f, 0f, moonPulse);

        using (var moonPb = new CanvasPathBuilder(rc))
        {
            float innerRx = moonR * 0.54f;
            moonPb.BeginFigure(0f, -moonR);
            moonPb.AddArc(Vector2.Zero, moonR, moonR, -MathF.PI * 0.5f, -MathF.PI);
            moonPb.AddArc(Vector2.Zero, innerRx, moonR, MathF.PI * 0.5f, MathF.PI);
            moonPb.EndFigure(CanvasFigureLoop.Closed);
            using var crescentGeo = CanvasGeometry.CreatePath(moonPb);
            float kc = Math.Clamp(k, 0.6f, 1f);
            using var crescentBrush = new CanvasLinearGradientBrush(rc, [
                new CanvasGradientStop { Position = 0f, Color = Ca(starWhite, (isDark ? 0.92f : 0.85f) * kc) },
                new CanvasGradientStop { Position = 1f, Color = Ca(starWarm, (isDark ? 0.72f : 0.68f) * kc) },
            ]) { StartPoint = new Vector2(-moonR, -moonR * 0.5f), EndPoint = new Vector2(0f, moonR) };

            var saved = ds.Transform;
            ds.Transform = Matrix3x2.CreateRotation(22f * DegToRad) * Matrix3x2.CreateTranslation(moonCenter) * saved;
            ds.FillCircle(0f, 0f, moonR * 0.98f, Ca(starWhite, (isDark ? 0.035f : 0.040f) * k));
            ds.FillGeometry(crescentGeo, crescentBrush);
            ds.Transform = saved;
        }

        DrawMeteor(ds, rc, w, h, minDim, starCool, starWhite, k, isDark, t, 18f, 1.15f, 0.2f, 0.14f, 0.22f, 0.10f, 0.34f, 307, 313, 0.0022f, 0.58f);

        BuildRidgeAndRibbon(rc, w, h, 72, h * 0.011f, 0.12f, 0.15f,
            xn => h * 0.80f + h * 0.030f * MathF.Sin(xn * 3.4f + 0.7f) + h * 0.014f * MathF.Cos(xn * 7.1f - 0.4f),
            out var farHill, out var farRib);
        BuildRidgeAndRibbon(rc, w, h, 72, h * 0.013f, 0.12f, 0.15f,
            xn => h * 0.875f + h * 0.025f * MathF.Sin(xn * 2.8f + 2.2f) + h * 0.010f * MathF.Sin(xn * 6.2f + 0.9f),
            out var nearHill, out var nearRib);

        var crestTint = Argb.Lerp(starCool, starWhite, 0.45f);
        using (farHill)
        using (farRib)
        using (nearHill)
        using (nearRib)
        using (var farBrush = VerticalGrad(rc, h * 0.74f, h, [
            (0f, Ca(Argb.Lerp(c.Shade, c.SkyHorizon, 0.35f), (isDark ? 0.78f : 0.40f) * k)),
            (1f, Ca(c.Ink, (isDark ? 0.92f : 0.58f) * k)),
        ]))
        using (var farRibBrush = VerticalGrad(rc, h * 0.74f, h * 0.86f, [
            (0f, Ca(crestTint, (isDark ? 0.16f : 0.18f) * k)),
            (1f, Colors.Transparent),
        ]))
        using (var nearBrush = VerticalGrad(rc, h * 0.82f, h, [
            (0f, Ca(Argb.Lerp(c.Ink, c.Shade, 0.32f), (isDark ? 0.92f : 0.56f) * k)),
            (1f, Ca(c.Ink, (isDark ? 0.98f : 0.72f) * k)),
        ]))
        using (var nearRibBrush = VerticalGrad(rc, h * 0.82f, h * 0.93f, [
            (0f, Ca(crestTint, (isDark ? 0.12f : 0.15f) * k)),
            (1f, Colors.Transparent),
        ]))
        {
            ds.FillGeometry(farHill, farBrush);
            ds.FillGeometry(farRib, farRibBrush);
            ds.FillGeometry(nearHill, nearBrush);
            ds.FillGeometry(nearRib, nearRibBrush);
        }
    }

    private static readonly float[] RoseBokehX = [0.16f, 0.52f, 0.88f, 0.30f, 0.70f];
    private static readonly float[] RoseBokehY = [0.09f, 0.20f, 0.13f, 0.30f, 0.27f];

    private static void DrawRosePetals(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        if (w < 2f || h < 2f) return;
        float k = Math.Clamp(detail, 0.35f, 1.40f);
        float ka = MathF.Min(k, 1.15f);
        bool dark = c.IsDark;
        float minDim = MathF.Min(w, h);

        var light = new Vector2(w * 0.82f, h * 0.07f);
        float hazeR = MathF.Max(w, h * 0.5f) * 0.80f;
        var hazeTint = Argb.Lerp(c.Accent, c.Highlight, dark ? 0.30f : 0.70f);
        float breathe = 0.93f + 0.07f * MathF.Sin(t * 0.19f);
        using (var haze = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = Ca(hazeTint, (dark ? 0.22f : 0.50f) * ka) },
            new CanvasGradientStop { Position = 0.40f, Color = Ca(hazeTint, (dark ? 0.08f : 0.16f) * ka) },
            new CanvasGradientStop { Position = 1.00f, Color = Ca(hazeTint, 0f) },
        ]) { Center = light, RadiusX = hazeR, RadiusY = hazeR, Opacity = breathe })
            ds.FillCircle(light, hazeR, haze);

        for (int i = 0; i < 5; i++)
        {
            var tint = Argb.Lerp(i % 2 == 0 ? c.Accent : c.Secondary, c.Highlight, dark ? 0.12f : 0.40f);
            float r = MathF.Max(0.5f, minDim * (0.15f + 0.12f * SceneHash(i, 5)));
            float peak = (dark ? 0.16f : 0.26f) * ka;
            float pulse = 0.72f + 0.28f * MathF.Sin(t * 0.16f + i * 1.37f);
            float dx = MathF.Sin(t * 0.07f + i * 2.1f) * w * 0.022f;
            float dy = MathF.Sin(t * 0.05f + i * 1.3f + 1.1f) * h * 0.012f;
            using var b = new CanvasRadialGradientBrush(rc, [
                new CanvasGradientStop { Position = 0.00f, Color = Ca(tint, peak) },
                new CanvasGradientStop { Position = 0.62f, Color = Ca(tint, peak * 0.62f) },
                new CanvasGradientStop { Position = 0.90f, Color = Ca(tint, peak * 0.10f) },
                new CanvasGradientStop { Position = 1.00f, Color = Ca(tint, 0f) },
            ]) { Center = Vector2.Zero, RadiusX = r, RadiusY = r };
            DrawSoftDisc(ds, b, w * RoseBokehX[i] + dx, h * RoseBokehY[i] + dy, r, 1f, 1f, 0f, pulse);
        }

        float unit = MathF.Max(0.5f, minDim * 0.043f);
        using var petalPb = new CanvasPathBuilder(rc);
        petalPb.BeginFigure(0f, unit);
        petalPb.AddCubicBezier(new Vector2(-unit * 0.95f, unit * 0.45f), new Vector2(-unit * 1.05f, -unit * 0.62f), new Vector2(-unit * 0.24f, -unit * 0.93f));
        petalPb.AddCubicBezier(new Vector2(-unit * 0.10f, -unit * 0.99f), new Vector2(-unit * 0.04f, -unit * 0.78f), new Vector2(0f, -unit * 0.72f));
        petalPb.AddCubicBezier(new Vector2(unit * 0.04f, -unit * 0.78f), new Vector2(unit * 0.10f, -unit * 0.99f), new Vector2(unit * 0.24f, -unit * 0.93f));
        petalPb.AddCubicBezier(new Vector2(unit * 1.05f, -unit * 0.62f), new Vector2(unit * 0.95f, unit * 0.45f), new Vector2(0f, unit));
        petalPb.EndFigure(CanvasFigureLoop.Closed);
        using var petalGeo = CanvasGeometry.CreatePath(petalPb);

        using var creasePb = new CanvasPathBuilder(rc);
        creasePb.BeginFigure(0f, unit * 0.86f);
        creasePb.AddCubicBezier(new Vector2(-unit * 0.10f, unit * 0.40f), new Vector2(unit * 0.08f, -unit * 0.10f), new Vector2(0f, -unit * 0.50f));
        creasePb.EndFigure(CanvasFigureLoop.Open);
        using var creaseGeo = CanvasGeometry.CreatePath(creasePb);
        using var creaseStyle = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };
        float creaseW = MathF.Max(0.5f, unit * 0.07f);
        var creaseCol = Argb.Lerp(c.Accent, Argb.Hex(0xFF000000), dark ? 0.24f : 0.20f);

        var nearBody = dark ? Argb.Lerp(c.Accent, c.Highlight, 0.10f) : c.Accent;
        using var nearBrush = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = C(Argb.Lerp(c.Accent, c.Highlight, dark ? 0.58f : 0.46f)) },
            new CanvasGradientStop { Position = 0.55f, Color = C(nearBody) },
            new CanvasGradientStop { Position = 1.00f, Color = C(Argb.Lerp(nearBody, Argb.Hex(0xFF000000), 0.10f)) },
        ]) { StartPoint = new Vector2(-unit * 0.5f, -unit), EndPoint = new Vector2(unit * 0.4f, unit) };

        var farBody = Argb.Lerp(c.Accent, c.SkyUpperMid, dark ? 0.36f : 0.30f);
        using var farBrush = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0f, Color = C(Argb.Lerp(farBody, c.Highlight, 0.42f)) },
            new CanvasGradientStop { Position = 1f, Color = C(farBody) },
        ]) { StartPoint = new Vector2(-unit * 0.5f, -unit), EndPoint = new Vector2(unit * 0.4f, unit) };

        float nearAlpha = MathF.Min((dark ? 0.78f : 0.90f) * k, 0.95f);
        float farAlpha = MathF.Min((dark ? 0.44f : 0.56f) * k, 0.80f);
        float countScale = MathF.Min(k, 1f);
        int farCount = Math.Clamp((int)(9 * countScale + 0.5f), 5, 9);
        int nearCount = Math.Clamp((int)(6 * countScale + 0.5f), 4, 6);

        static float RosePetalCalm(float yn)
        {
            float d = (yn - 0.45f) / 0.20f;
            return 1f - 0.40f * MathF.Max(0f, 1f - d * d);
        }

        var savedBase = ds.Transform;
        for (int i = 0; i < farCount; i++)
        {
            float prog = (SceneHash(i, 101) + t * (0.014f + 0.010f * SceneHash(i, 103))) % 1f;
            float yn = prog * 1.16f - 0.08f;
            float phase = SceneHash(i, 107) * TwoPi;
            float sway = MathF.Sin(t * (0.36f + 0.22f * SceneHash(i, 109)) + phase);
            float xn = 0.03f + 0.90f * SceneHash(i, 113) + 0.040f * sway + 0.05f * prog;
            float edge = MathF.Min(prog * 9f, 1f) * MathF.Min((1f - prog) * 9f, 1f);
            float tumble = 0.675f + 0.325f * MathF.Sin(t * (0.55f + 0.35f * SceneHash(i, 127)) + phase * 1.7f);
            float spin = (i % 2 == 0 ? 1f : -1f) * (4f + 5f * SceneHash(i, 131));
            float angle = SceneHash(i, 137) * 360f + 26f * MathF.Sin(t * 0.41f + phase) + t * spin;
            float sc = 0.34f + 0.14f * SceneHash(i, 139);
            float a = Math.Clamp(farAlpha * edge * RosePetalCalm(yn), 0f, 1f);
            if (a <= 0.004f) continue;

            ds.Transform = Matrix3x2.CreateScale(sc * tumble, sc) * Matrix3x2.CreateRotation(angle * DegToRad) * Matrix3x2.CreateTranslation(xn * w, yn * h) * savedBase;
            farBrush.Opacity = a;
            ds.FillGeometry(petalGeo, farBrush);
        }
        ds.Transform = savedBase;

        static float HedgeProfile(float a, int layer) =>
            0.55f * SoftAbs(MathF.Sin(1.5f * a + layer * 1.3f))
            + 0.30f * SoftAbs(MathF.Sin(3.5f * a + 0.8f + layer * 2.1f))
            + 0.15f * MathF.Sin(2f * a + layer * 0.9f);

        float bloomR = MathF.Max(0.6f, minDim * 0.008f);
        var bloomCol = Argb.Lerp(c.Accent, c.Highlight, dark ? 0.30f : 0.20f);
        float bloomA = MathF.Min((dark ? 0.55f : 0.80f) * k, 0.92f);

        for (int i = 0; i < 2; i++)
        {
            float depth = i;
            float baseY = h * (0.860f + 0.065f * i);
            float amp = h * (0.022f + 0.008f * i);
            float speed = 0.003f + 0.004f * depth;
            float shift = (t * speed * w) % w;

            const int steps = 72;
            using var pb = new CanvasPathBuilder(rc);
            var trace = new SmoothTrace(pb);
            for (int s = -1; s <= steps * 2 + 1; s++)
            {
                float x = w * s / steps;
                float a = TwoPi * s / steps;
                float y = baseY - amp * HedgeProfile(a, i);
                if (s == -1) trace.Start(x, y);
                else trace.To(x, y);
            }
            trace.End();
            pb.AddLine(w * 2f, h);
            pb.AddLine(0f, h);
            pb.EndFigure(CanvasFigureLoop.Closed);
            using var hedgeGeo = CanvasGeometry.CreatePath(pb);

            var body = Argb.Lerp(c.Shade, c.Ink, 0.80f * depth);
            var lit = Argb.Lerp(body, c.Accent, dark ? 0.16f : 0.10f);
            using var hedgeBrush = VerticalGrad(rc, baseY - amp, h, [
                (0f, Ca(lit, 0.52f + 0.40f * depth)),
                (1f, Ca(body, 0.78f + 0.20f * depth)),
            ]);

            ds.Transform = Matrix3x2.CreateTranslation(-shift, 0f) * savedBase;
            ds.FillGeometry(hedgeGeo, hedgeBrush);

            if (i == 1)
            {
                var bc = Ca(bloomCol, bloomA);
                for (int p = -1; p <= 2; p++)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        float fx = (j + 0.2f + 0.6f * SceneHash(j, 71)) / 6f;
                        float r = bloomR * (0.7f + 0.6f * SceneHash(j, 79));
                        float cx = (p + fx) * w;
                        float cy = baseY - amp * HedgeProfile(TwoPi * fx, i) + bloomR * (1.2f + 2.4f * SceneHash(j, 73));
                        ds.FillCircle(cx, cy, r, bc);
                    }
                }
            }
            ds.Transform = savedBase;
        }

        var moteColor = dark ? Argb.Lerp(c.Highlight, c.Accent, 0.25f) : Argb.Lerp(c.Accent, c.Highlight, 0.35f);
        float moteAlpha = (dark ? 0.34f : 0.46f) * k;
        float moteR = MathF.Min(1.2f, minDim * 0.012f);
        for (int i = 0; i < 10; i++)
        {
            float lane = SceneHash(i, 31);
            float yn = lane < 0.55f ? 0.03f + lane * 0.50f : 0.64f + (lane - 0.55f) * 0.62f;
            float xn = ((SceneHash(i, 17) + t * (0.004f + 0.007f * SceneHash(i, 43))) % 1f) * 1.06f - 0.03f;
            float bob = MathF.Sin(t * 0.33f + i * 2.1f) * h * 0.010f;
            float shimmer = 0.25f + 0.75f * (0.5f + 0.5f * MathF.Sin(t * 0.8f + i * 1.9f));
            ds.FillCircle(xn * w, yn * h + bob, moteR * (0.7f + 0.8f * SceneHash(i, 59)), Ca(moteColor, Math.Clamp(moteAlpha * shimmer, 0f, 0.8f)));
        }

        for (int i = 0; i < nearCount; i++)
        {
            int n = i + 40;
            float prog = (SceneHash(n, 101) + t * (0.026f + 0.014f * SceneHash(n, 103))) % 1f;
            float yn = prog * 1.16f - 0.08f;
            float phase = SceneHash(n, 107) * TwoPi;
            float sway = MathF.Sin(t * (0.48f + 0.26f * SceneHash(n, 109)) + phase);
            float xn = 0.02f + 0.88f * SceneHash(n, 113) + 0.060f * sway + 0.08f * prog;
            float edge = MathF.Min(prog * 9f, 1f) * MathF.Min((1f - prog) * 9f, 1f);
            float tumble = 0.675f + 0.325f * MathF.Sin(t * (0.70f + 0.40f * SceneHash(n, 127)) + phase * 1.7f);
            float spin = (i % 2 == 0 ? -1f : 1f) * (6f + 6f * SceneHash(n, 131));
            float angle = SceneHash(n, 137) * 360f + 32f * MathF.Sin(t * 0.52f + phase) + t * spin;
            float sc = 0.70f + 0.30f * SceneHash(n, 139);
            float a = Math.Clamp(nearAlpha * edge * RosePetalCalm(yn), 0f, 1f);
            if (a <= 0.004f) continue;

            ds.Transform = Matrix3x2.CreateScale(sc * tumble, sc) * Matrix3x2.CreateRotation(angle * DegToRad) * Matrix3x2.CreateTranslation(xn * w, yn * h) * savedBase;
            nearBrush.Opacity = a;
            ds.FillGeometry(petalGeo, nearBrush);
            ds.DrawGeometry(creaseGeo, Ca(creaseCol, a * 0.55f), creaseW, creaseStyle);
        }
        ds.Transform = savedBase;
    }

    private static void DrawSnow(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        var frostWhite = Argb.Hex(0xFFF6FAFF);
        var iceTint = Argb.Lerp(frostWhite, c.Accent, 0.18f);
        var coolMist = Argb.Lerp(c.SkyHorizon, iceTint, 0.45f);

        float skyWashR = minDim * 0.62f;
        using (var skyWash = SoftRadial(rc, iceTint, (isDark ? 0.11f : 0.18f) * k, skyWashR, c.SkyUpperMid))
            DrawSoftDisc(ds, skyWash, w * (0.46f + 0.03f * MathF.Sin(t * 0.06f)), h * 0.14f, skyWashR, 1.25f, 1.25f, 0f, 0.90f + 0.10f * MathF.Cos(t * 0.10f));

        float farBaseA = (isDark ? 0.42f : 0.56f) * k;
        for (int i = 0; i < 28; i++)
        {
            float xn0 = 0.03f + 0.94f * SceneHash(i, 17);
            float ph = SceneHash(i, 31);
            float sp = 0.011f + 0.007f * SceneHash(i, 47);
            float r = minDim * (0.0014f + 0.0014f * SceneHash(i, 61));
            float swayPh = SceneHash(i, 73) * TwoPi;
            float yn = -0.04f + (ph + t * sp) % 1.08f;
            float xn = PosMod(xn0 + 0.012f * MathF.Sin(t * 0.25f + swayPh) + t * 0.0018f, 1.0f);
            float calm = SceneCalmMask(yn, 0.48f, 0.25f, 0.65f);
            float edge = yn < 0.04f ? Math.Clamp((yn + 0.04f) / 0.08f, 0f, 1f)
                       : yn > 0.92f ? Math.Clamp((1.04f - yn) / 0.12f, 0f, 1f) : 1f;
            float a = farBaseA * calm * edge;
            if (a > 0.01f) ds.FillCircle(w * xn, h * yn, r, Ca(frostWhite, a));
        }

        BuildRidgeAndRibbon(rc, w, h, 80, h * 0.015f, 0.10f, 0.18f,
            xn => h * 0.81f
                - h * 0.044f * MathF.Exp(-((xn - 0.22f) * (xn - 0.22f)) / 0.032f)
                - h * 0.052f * MathF.Exp(-((xn - 0.78f) * (xn - 0.78f)) / 0.036f)
                + h * 0.014f * MathF.Sin(xn * 3.8f + 0.6f),
            out var farRidge, out var farRib);
        BuildRidgeAndRibbon(rc, w, h, 80, h * 0.016f, 0.10f, 0.18f,
            xn => h * 0.885f + h * 0.022f * MathF.Sin(xn * 2.9f + 2.0f) + h * 0.009f * MathF.Cos(xn * 5.6f),
            out var nearBank, out var nearRib);

        var snowRim = isDark ? iceTint : frostWhite;
        using (farRidge)
        using (farRib)
        using (nearBank)
        using (nearRib)
        using (var farBrush = VerticalGrad(rc, h * 0.74f, h, [
            (0f, Ca(Argb.Lerp(c.Shade, coolMist, isDark ? 0.26f : 0.40f), (isDark ? 0.72f : 0.42f) * k)),
            (1f, Ca(Argb.Lerp(c.Ink, c.Shade, 0.35f), (isDark ? 0.88f : 0.56f) * k)),
        ]))
        using (var farRibBrush = VerticalGrad(rc, h * 0.74f, h * 0.85f, [
            (0f, Ca(snowRim, (isDark ? 0.22f : 0.36f) * k)),
            (1f, Colors.Transparent),
        ]))
        using (var mistBrush = VerticalGrad(rc, h * 0.70f, h * 0.92f, [
            (0.00f, Colors.Transparent),
            (0.50f, Ca(coolMist, (isDark ? 0.11f : 0.20f) * k)),
            (1.00f, Colors.Transparent),
        ]))
        using (var nearBrush = VerticalGrad(rc, h * 0.83f, h, [
            (0f, Ca(Argb.Lerp(c.Ink, coolMist, isDark ? 0.22f : 0.34f), (isDark ? 0.88f : 0.54f) * k)),
            (1f, Ca(c.Ink, (isDark ? 0.96f : 0.70f) * k)),
        ]))
        using (var nearRibBrush = VerticalGrad(rc, h * 0.83f, h * 0.93f, [
            (0f, Ca(snowRim, (isDark ? 0.18f : 0.30f) * k)),
            (1f, Colors.Transparent),
        ]))
        {
            ds.FillGeometry(farRidge, farBrush);
            ds.FillGeometry(farRib, farRibBrush);
            ds.FillRectangle(0, h * 0.70f, w, h * 0.22f, mistBrush);
            ds.FillGeometry(nearBank, nearBrush);
            ds.FillGeometry(nearRib, nearRibBrush);
        }

        float midR = MathF.Max(1f, minDim * 0.011f);
        using (var midBrush = SoftRadial(rc, frostWhite, (isDark ? 0.42f : 0.56f) * k, midR, iceTint))
        {
            for (int i = 0; i < 20; i++)
            {
                float xn0 = 0.04f + 0.92f * SceneHash(i, 89);
                float ph = SceneHash(i, 103);
                float sp = 0.017f + 0.009f * SceneHash(i, 117);
                float sc = 0.65f + 0.60f * SceneHash(i, 131);
                float swayA = 0.014f + 0.016f * SceneHash(i, 149);
                float swayF = 0.28f + 0.24f * SceneHash(i, 163);
                float swayP = SceneHash(i, 179) * TwoPi;

                float yn = -0.05f + (ph + t * sp) % 1.10f;
                float xn = PosMod(xn0 + swayA * MathF.Sin(t * swayF + swayP) + t * 0.0028f, 1.06f) - 0.03f;
                float calm = SceneCalmMask(yn, 0.48f, 0.25f, 0.72f);
                float edge = yn < 0.04f ? Math.Clamp((yn + 0.05f) / 0.09f, 0f, 1f)
                           : yn > 0.90f ? Math.Clamp((1.05f - yn) / 0.15f, 0f, 1f) : 1f;
                float a = Math.Clamp(calm * edge, 0f, 1f);
                if (a > 0.02f) DrawSoftDisc(ds, midBrush, w * xn, h * yn, midR, sc, sc, 0f, a);
            }
        }

        float nearR = MathF.Max(1f, minDim * 0.032f);
        using (var nearBokeh = SoftRadial(rc, frostWhite, (isDark ? 0.18f : 0.28f) * k, nearR, iceTint))
        {
            for (int i = 0; i < 10; i++)
            {
                float xn0 = 0.06f + 0.88f * SceneHash(i, 193);
                float ph = SceneHash(i, 211);
                float sp = 0.025f + 0.013f * SceneHash(i, 227);
                float sc = 0.72f + 0.50f * SceneHash(i, 241);
                float swayA = 0.020f + 0.018f * SceneHash(i, 257);
                float swayP = SceneHash(i, 271) * TwoPi;

                float yn = -0.07f + (ph + t * sp) % 1.14f;
                float xn = PosMod(xn0 + swayA * MathF.Sin(t * 0.24f + swayP) + t * 0.0036f, 1.08f) - 0.04f;
                float calm = SceneCalmMask(yn, 0.48f, 0.25f, 0.78f);
                float edge = yn < 0.06f ? Math.Clamp((yn + 0.07f) / 0.13f, 0f, 1f)
                           : yn > 0.88f ? Math.Clamp((1.07f - yn) / 0.19f, 0f, 1f) : 1f;
                float a = Math.Clamp(calm * edge, 0f, 1f);
                if (a > 0.02f) DrawSoftDisc(ds, nearBokeh, w * xn, h * yn, nearR, sc, sc, 0f, a);
            }
        }
    }

    private readonly record struct ButterflyFlight(
        float Cx, float Cy, float Ax, float Ay, float Speed,
        float Size, float Phase, float FlapRate, bool Second);

    private static readonly float[] ButterflyBokehX = [0.68f, 0.34f, 0.90f, 0.14f, 0.62f];
    private static readonly float[] ButterflyBokehY = [0.10f, 0.22f, 0.27f, 0.72f, 0.78f];
    private static readonly ButterflyFlight[] ButterflyFlights =
    [
        new(0.28f, 0.74f, 0.20f, 0.060f, -0.24f, 1.00f, 4.8f, 6.4f, false),
        new(0.30f, 0.17f, 0.22f, 0.070f, 0.21f, 0.62f, 0.0f, 7.1f, false),
        new(0.72f, 0.70f, 0.18f, 0.050f, 0.19f, 0.78f, 2.6f, 6.8f, true),
        new(0.70f, 0.12f, 0.20f, 0.055f, -0.16f, 0.44f, 1.9f, 7.6f, true),
        new(0.50f, 0.79f, 0.34f, 0.035f, 0.11f, 0.52f, 5.7f, 7.3f, false),
        new(0.52f, 0.25f, 0.30f, 0.050f, 0.13f, 0.36f, 3.4f, 8.0f, false),
    ];

    private static void DrawButterflies(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        if (w < 2f || h < 2f) return;
        float k = Math.Clamp(detail, 0.35f, 1.40f);
        float ka = MathF.Min(k, 1.15f);
        bool dark = c.IsDark;
        float minDim = MathF.Min(w, h);

        var sun = new Vector2(w * 0.16f, h * 0.07f);
        float hazeR = MathF.Max(w, h * 0.5f) * 0.78f;
        var hazeTint = Argb.Lerp(c.Secondary, c.Highlight, dark ? 0.30f : 0.70f);
        using (var haze = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = Ca(hazeTint, (dark ? 0.22f : 0.50f) * ka) },
            new CanvasGradientStop { Position = 0.40f, Color = Ca(hazeTint, (dark ? 0.08f : 0.16f) * ka) },
            new CanvasGradientStop { Position = 1.00f, Color = Ca(hazeTint, 0f) },
        ]) { Center = sun, RadiusX = hazeR, RadiusY = hazeR, Opacity = 0.93f + 0.07f * MathF.Sin(t * 0.19f) })
            ds.FillCircle(sun, hazeR, haze);

        for (int i = 0; i < 5; i++)
        {
            var tint = Argb.Lerp(i % 2 == 0 ? c.Accent : c.Secondary, c.Highlight, dark ? 0.12f : 0.40f);
            float r = MathF.Max(0.5f, minDim * (0.13f + 0.11f * SceneHash(i, 9)));
            float peak = (dark ? 0.15f : 0.24f) * ka * (ButterflyBokehY[i] > 0.5f ? 0.70f : 1f);
            float pulse = 0.72f + 0.28f * MathF.Sin(t * 0.15f + i * 1.61f);
            float dx = MathF.Sin(t * 0.06f + i * 2.3f) * w * 0.024f;
            float dy = MathF.Sin(t * 0.05f + i * 1.1f + 0.7f) * h * 0.012f;
            using var b = new CanvasRadialGradientBrush(rc, [
                new CanvasGradientStop { Position = 0.00f, Color = Ca(tint, peak) },
                new CanvasGradientStop { Position = 0.62f, Color = Ca(tint, peak * 0.62f) },
                new CanvasGradientStop { Position = 0.90f, Color = Ca(tint, peak * 0.10f) },
                new CanvasGradientStop { Position = 1.00f, Color = Ca(tint, 0f) },
            ]) { Center = Vector2.Zero, RadiusX = r, RadiusY = r };
            DrawSoftDisc(ds, b, w * ButterflyBokehX[i] + dx, h * ButterflyBokehY[i] + dy, r, 1f, 1f, 0f, pulse);
        }

        static float GroundY(float xn, int layer, float hh)
        {
            float bs = 0.855f + 0.085f * layer;
            float amp = 0.012f + 0.004f * layer;
            return hh * (bs - amp * (0.6f * MathF.Sin(xn * 4.2f + layer * 1.7f) + 0.4f * MathF.Sin(xn * 9.1f + layer * 2.9f + 0.6f)));
        }

        static CanvasGeometry GroundGeo(ICanvasResourceCreator creator, int layer, float ww, float hh)
        {
            using var pb = new CanvasPathBuilder(creator);
            var trace = new SmoothTrace(pb);
            for (int s = 0; s <= 40; s++)
            {
                float xn = -0.02f + 1.04f * s / 40f;
                float y = GroundY(xn, layer, hh);
                if (s == 0) trace.Start(xn * ww, y);
                else trace.To(xn * ww, y);
            }
            trace.End();
            pb.AddLine(ww * 1.02f, hh);
            pb.AddLine(-ww * 0.02f, hh);
            pb.EndFigure(CanvasFigureLoop.Closed);
            return CanvasGeometry.CreatePath(pb);
        }

        static void AddBlade(CanvasPathBuilder pb, float x, float baseY, float bh, float hw, float lean)
        {
            pb.BeginFigure(x - hw, baseY);
            pb.AddCubicBezier(new Vector2(x - hw * 0.7f, baseY - bh * 0.50f), new Vector2(x + lean * 0.35f - hw * 0.2f, baseY - bh * 0.86f), new Vector2(x + lean, baseY - bh));
            pb.AddCubicBezier(new Vector2(x + lean * 0.45f + hw * 0.3f, baseY - bh * 0.80f), new Vector2(x + hw * 0.9f, baseY - bh * 0.45f), new Vector2(x + hw, baseY));
            pb.EndFigure(CanvasFigureLoop.Closed);
        }

        var farBody = Argb.Lerp(c.SkyHorizon, Argb.Lerp(c.Shade, c.Ink, 0.15f), 0.86f);
        var farLit = Argb.Lerp(farBody, c.Accent, dark ? 0.16f : 0.07f);
        var nearBody = Argb.Lerp(c.Shade, c.Ink, 0.88f);
        var nearLit = Argb.Lerp(nearBody, c.Accent, dark ? 0.10f : 0.06f);
        var flowerCol = Argb.Lerp(c.Secondary, c.Highlight, dark ? 0.30f : 0.15f);

        float grassScale = MathF.Min(h, w * 2.4f);
        float sink = MathF.Max(h * 0.008f, w * 0.006f);
        float flowerR = MathF.Max(0.8f, minDim * 0.0085f);
        var savedBase = ds.Transform;

        for (int layer = 0; layer < 2; layer++)
        {
            using (var ground = GroundGeo(rc, layer, w, h))
            using (var fill = VerticalGrad(rc, h * (layer == 0 ? 0.876f : 0.965f), h, [
                (0f, C(layer == 0 ? farLit : nearLit)),
                (1f, C(layer == 0 ? farBody : nearBody)),
            ]))
            {
                ds.FillGeometry(ground, fill);
            }

            int count = layer == 0 ? 6 : 8;
            float span = 1f / count;
            float bladeH = grassScale * (layer == 0 ? 0.032f : 0.056f);
            float bladeHalf = w * (layer == 0 ? 0.0065f : 0.0095f);
            var tuftCol = C(layer == 0 ? farLit : nearLit);
            float flAlpha = MathF.Min((layer == 0 ? 0.62f : 0.88f) * k, 0.95f);
            float swayDeg = layer == 0 ? 1.6f : 2.6f;

            for (int j = 0; j < count; j++)
            {
                int index = layer == 0 ? j : 6 + j;
                float x0 = j * span;
                float center = x0 + span * 0.5f;
                float pivotX = center * w;
                float pivotY = GroundY(center, layer, h) + sink;
                float phase = j * 0.75f + layer * 1.9f;
                float swing = swayDeg * (MathF.Sin(t * 0.85f + phase) + 0.35f * MathF.Sin(t * 1.9f + phase * 2.3f));

                using var bladesPb = new CanvasPathBuilder(rc);
                for (int b = 0; b < 7; b++)
                {
                    int seed = index * 16 + b;
                    float xn = x0 + span * (b + 0.5f + 0.7f * (SceneHash(seed, 211) - 0.5f)) / 7f;
                    float bh = bladeH * (0.45f + 0.55f * SceneHash(seed, 223));
                    AddBlade(bladesPb, xn * w, GroundY(xn, layer, h) + sink, bh + sink,
                        bladeHalf * (0.7f + 0.6f * SceneHash(seed, 227)),
                        bh * 0.8f * (SceneHash(seed, 229) - 0.5f));
                }

                bool flowering = layer == 0 ? j % 3 == 1 : j % 2 == 0;
                List<(float Cx, float Cy, float R)>? heads = null;
                if (flowering)
                {
                    heads = [];
                    int stems = layer == 1 && j % 4 == 0 ? 2 : 1;
                    for (int q = 0; q < stems; q++)
                    {
                        int seed = index * 16 + 9 + q;
                        float xn = x0 + span * (0.2f + 0.6f * (q + SceneHash(seed, 233)) / stems);
                        float bh = bladeH * (1.00f + 0.25f * SceneHash(seed, 239));
                        float lean = bh * 0.25f * (SceneHash(seed, 241) - 0.5f);
                        float by = GroundY(xn, layer, h) + sink;
                        AddBlade(bladesPb, xn * w, by, bh + sink, bladeHalf * 0.45f, lean);
                        float r = flowerR * (layer == 0 ? 0.65f : 1f) * (0.8f + 0.4f * SceneHash(seed, 251));
                        heads.Add((xn * w + lean, by - bh - sink, r));
                    }
                }

                using var bladesGeo = CanvasGeometry.CreatePath(bladesPb);
                ds.Transform = Matrix3x2.CreateTranslation(-pivotX, -pivotY)
                    * Matrix3x2.CreateRotation(swing * DegToRad)
                    * Matrix3x2.CreateTranslation(pivotX, pivotY) * savedBase;
                ds.FillGeometry(bladesGeo, tuftCol);
                if (heads != null)
                {
                    var fc = Ca(flowerCol, flAlpha);
                    foreach (var (cx, cy, r) in heads) ds.FillCircle(cx, cy, r, fc);
                }
            }
        }
        ds.Transform = savedBase;

        var pollenCol = dark ? Argb.Lerp(c.Highlight, c.Secondary, 0.25f) : Argb.Lerp(c.Secondary, c.Highlight, 0.25f);
        float pollenAlpha = (dark ? 0.40f : 0.52f) * k;
        float pollenR = MathF.Min(1.2f, minDim * 0.012f);
        for (int i = 0; i < 14; i++)
        {
            float lane = SceneHash(i, 37);
            float yn = lane < 0.5f ? 0.04f + lane * 0.56f : 0.62f + (lane - 0.5f) * 0.52f;
            float xn = ((SceneHash(i, 19) + t * (0.003f + 0.006f * SceneHash(i, 47))) % 1f) * 1.06f - 0.03f;
            float bob = MathF.Sin(t * 0.31f + i * 2.3f) * h * 0.012f;
            float shimmer = 0.25f + 0.75f * (0.5f + 0.5f * MathF.Sin(t * 0.7f + i * 1.7f));
            ds.FillCircle(xn * w, yn * h + bob, pollenR * (0.7f + 0.8f * SceneHash(i, 61)), Ca(pollenCol, Math.Clamp(pollenAlpha * shimmer, 0f, 0.8f)));
        }

        float unit = MathF.Max(0.5f, minDim * 0.050f);
        using var wingPb = new CanvasPathBuilder(rc);
        wingPb.BeginFigure(0f, -unit * 0.20f);
        wingPb.AddCubicBezier(new Vector2(unit * 0.28f, -unit * 1.06f), new Vector2(unit * 1.12f, -unit * 1.02f), new Vector2(unit * 1.04f, -unit * 0.44f));
        wingPb.AddCubicBezier(new Vector2(unit * 1.00f, -unit * 0.12f), new Vector2(unit * 0.46f, unit * 0.06f), new Vector2(0f, unit * 0.04f));
        wingPb.EndFigure(CanvasFigureLoop.Closed);
        wingPb.BeginFigure(0f, -unit * 0.02f);
        wingPb.AddCubicBezier(new Vector2(unit * 0.56f, 0f), new Vector2(unit * 0.94f, unit * 0.28f), new Vector2(unit * 0.74f, unit * 0.64f));
        wingPb.AddCubicBezier(new Vector2(unit * 0.58f, unit * 0.92f), new Vector2(unit * 0.16f, unit * 0.72f), new Vector2(0f, unit * 0.34f));
        wingPb.EndFigure(CanvasFigureLoop.Closed);
        using var wingGeo = CanvasGeometry.CreatePath(wingPb);

        using var antPb = new CanvasPathBuilder(rc);
        antPb.BeginFigure(0f, -unit * 0.30f);
        antPb.AddCubicBezier(new Vector2(unit * 0.04f, -unit * 0.48f), new Vector2(unit * 0.14f, -unit * 0.60f), new Vector2(unit * 0.26f, -unit * 0.66f));
        antPb.EndFigure(CanvasFigureLoop.Open);
        antPb.BeginFigure(0f, -unit * 0.30f);
        antPb.AddCubicBezier(new Vector2(-unit * 0.04f, -unit * 0.48f), new Vector2(-unit * 0.14f, -unit * 0.60f), new Vector2(-unit * 0.26f, -unit * 0.66f));
        antPb.EndFigure(CanvasFigureLoop.Open);
        using var antGeo = CanvasGeometry.CreatePath(antPb);

        Argb EdgeCol(Argb tint) => dark
            ? Argb.Lerp(tint, c.Ink, 0.60f)
            : Argb.Lerp(Argb.Lerp(tint, c.Ink, 0.15f), Argb.Hex(0xFF000000), 0.22f);

        CanvasRadialGradientBrush WingBrush(Argb tint) => new(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = C(Argb.Lerp(tint, c.Highlight, dark ? 0.70f : 0.62f)) },
            new CanvasGradientStop { Position = 0.34f, Color = C(Argb.Lerp(tint, c.Highlight, 0.22f)) },
            new CanvasGradientStop { Position = 0.78f, Color = C(dark ? Argb.Lerp(tint, c.Highlight, 0.10f) : tint) },
            new CanvasGradientStop { Position = 1.00f, Color = C(EdgeCol(tint)) },
        ]) { Center = new Vector2(unit * 0.06f, -unit * 0.06f), RadiusX = unit * 1.16f, RadiusY = unit * 1.16f };

        using var wingMain = WingBrush(c.Accent);
        using var wingSecond = WingBrush(c.Secondary);
        var edgeMain = EdgeCol(c.Accent);
        var edgeSecond = EdgeCol(c.Secondary);
        using var edgeStyle = new CanvasStrokeStyle { LineJoin = CanvasLineJoin.Round };
        using var roundCap = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };
        float edgeW = MathF.Max(0.4f, unit * 0.055f);
        float antW = MathF.Max(0.4f, unit * 0.045f);
        float bodyW = MathF.Max(0.6f, unit * 0.13f);
        int flights = k < 0.8f ? ButterflyFlights.Length - 1 : ButterflyFlights.Length;

        for (int i = 0; i < flights; i++)
        {
            var f = ButterflyFlights[i];
            float theta = t * f.Speed + f.Phase;
            float beat = t * f.FlapRate + f.Phase * 3f;
            float s = f.Size;
            float bx = (f.Cx + f.Ax * MathF.Sin(theta)) * w;
            float by = (f.Cy + f.Ay * MathF.Sin(2f * theta)) * h - MathF.Sin(beat) * unit * s * 0.10f;
            float vx = f.Ax * MathF.Cos(theta) * f.Speed * w;
            float vy = 2f * f.Ay * MathF.Cos(2f * theta) * f.Speed * h;
            float headingRad = MathF.Atan2(vy, vx) + MathF.PI * 0.5f;

            float open = 0.5f + 0.5f * MathF.Sin(beat);
            float depth = 0.50f + 0.35f * (0.5f + 0.5f * MathF.Sin(t * 0.47f + f.Phase * 1.7f));
            float flap = 1f - depth * (1f - open);
            float a = Math.Clamp((0.50f + 0.45f * s) * (dark ? 0.92f : 1f) * ka, 0f, 0.96f);

            var wb = f.Second ? wingSecond : wingMain;
            var ec = f.Second ? edgeSecond : edgeMain;
            bool outlined = s > 0.5f;
            wb.Opacity = a;

            var bodyTf = Matrix3x2.CreateScale(s, s) * Matrix3x2.CreateRotation(headingRad) * Matrix3x2.CreateTranslation(bx, by) * savedBase;

            ds.Transform = Matrix3x2.CreateScale(flap, 1f) * bodyTf;
            ds.FillGeometry(wingGeo, wb);
            if (outlined) ds.DrawGeometry(wingGeo, Ca(ec, a * 0.75f), edgeW, edgeStyle);

            ds.Transform = Matrix3x2.CreateScale(-flap, 1f) * bodyTf;
            ds.FillGeometry(wingGeo, wb);
            if (outlined) ds.DrawGeometry(wingGeo, Ca(ec, a * 0.75f), edgeW, edgeStyle);

            ds.Transform = bodyTf;
            ds.DrawLine(0f, -unit * 0.30f, 0f, unit * 0.40f, Ca(ec, a), bodyW, roundCap);
            if (outlined) ds.DrawGeometry(antGeo, Ca(ec, a * 0.8f), antW, roundCap);
        }
        ds.Transform = savedBase;
    }

    private static void DrawRings(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        ReadOnlySpan<float> exs = [0.88f, 0.11f];
        ReadOnlySpan<float> eys = [0.09f, 0.89f];
        ReadOnlySpan<float> maxRs = [minDim * 0.84f, minDim * 0.62f];
        ReadOnlySpan<float> cycles = [9.0f, 11.0f];
        ReadOnlySpan<float> gains = [1.0f, 0.56f];

        var primaryRing = Argb.Lerp(c.Accent, c.Highlight, isDark ? 0.34f : 0.14f);
        var secondaryRing = Argb.Lerp(c.Secondary, c.Highlight, isDark ? 0.28f : 0.12f);
        var hairlinePrimary = isDark ? Argb.Lerp(primaryRing, c.Highlight, 0.42f) : Argb.Lerp(primaryRing, c.Accent, 0.25f);
        var hairlineSecondary = isDark ? Argb.Lerp(secondaryRing, c.Highlight, 0.38f) : Argb.Lerp(secondaryRing, c.Secondary, 0.25f);

        float glowW = MathF.Max(1.8f, minDim * 0.0068f);
        float coreW = MathF.Max(1.1f, minDim * 0.0020f);
        float hubR = MathF.Max(3.5f, minDim * 0.020f);
        float orbR = MathF.Max(1f, minDim * 0.09f);

        using var orb0 = SoftRadial(rc, primaryRing, (isDark ? 0.11f : 0.09f) * k, orbR, c.SkyUpperMid);
        using var orb1 = SoftRadial(rc, secondaryRing, (isDark ? 0.08f : 0.07f) * k, orbR, c.SkyLowerMid);

        for (int e = 0; e < 2; e++)
        {
            float ex = w * (exs[e] + 0.012f * MathF.Sin(t * 0.35f + e * 2.1f));
            float ey = h * (eys[e] + 0.010f * MathF.Cos(t * 0.28f + e * 1.7f));
            var baseTint = e == 1 ? secondaryRing : primaryRing;
            var fineTint = e == 1 ? hairlineSecondary : hairlinePrimary;
            float gain = gains[e];

            float hubPulse = 0.76f + 0.24f * MathF.Sin(t * 1.25f + e * 1.9f);
            DrawSoftDisc(ds, e == 1 ? orb1 : orb0, ex, ey, orbR, 1f, 1f, 0f, hubPulse * gain);
            ds.DrawCircle(ex, ey, hubR * (0.94f + 0.06f * hubPulse), Ca(fineTint, (isDark ? 0.28f : 0.24f) * hubPulse * gain * k), coreW);

            float period = cycles[e];
            float maxR = maxRs[e];
            for (int rIdx = 0; rIdx < 6; rIdx++)
            {
                float age = (t / period + rIdx / 6f + e * 0.14f) % 1f;
                float radius = hubR * 1.35f + maxR * age;
                float birthEase = MathF.Pow(MathF.Sin(MathF.Min(age * 2.2f, 1f) * MathF.PI * 0.5f), 0.75f);
                float outwardFade = MathF.Pow(1f - age, 1.25f);
                float midSin = MathF.Sin(age * MathF.PI);
                float midCalm = 1f - 0.38f * midSin * midSin;
                float waveA = Math.Clamp(birthEase * outwardFade * midCalm * gain * k, 0f, 1f);
                if (waveA > 0.008f)
                {
                    ds.DrawCircle(ex, ey, radius, Ca(baseTint, waveA * (isDark ? 0.055f : 0.045f)), glowW);
                    ds.DrawCircle(ex, ey, radius, Ca(fineTint, waveA * (isDark ? 0.23f : 0.19f)), coreW);
                }
            }
        }
    }

    private readonly record struct MovingCircleSpec(
        float XBase, float YBase, float Vx, float Vy, float SizeRatio,
        float SwayAmp, float SwayFreq, float Phase, int Layer, int ColorSlot);

    private static readonly MovingCircleSpec[] MovingCircles =
    [
        new(0.14f, 0.08f, 0.013f, -0.017f, 0.235f, 0.030f, 0.42f, 0.3f, 1, 0),
        new(0.56f, 0.04f, -0.012f, -0.015f, 0.255f, 0.032f, 0.38f, 1.4f, 0, 1),
        new(0.84f, 0.14f, -0.016f, -0.022f, 0.205f, 0.028f, 0.48f, 2.6f, 1, 2),
        new(0.32f, 0.19f, 0.018f, -0.026f, 0.190f, 0.038f, 0.54f, 3.8f, 2, 0),
        new(0.68f, 0.23f, -0.014f, -0.021f, 0.215f, 0.034f, 0.46f, 4.9f, 2, 1),
        new(0.10f, 0.29f, 0.015f, -0.020f, 0.155f, 0.030f, 0.58f, 0.9f, 2, 2),
        new(0.46f, 0.12f, -0.011f, -0.018f, 0.142f, 0.028f, 0.60f, 2.0f, 2, 0),
        new(0.90f, 0.31f, -0.015f, -0.019f, 0.168f, 0.032f, 0.52f, 5.4f, 1, 0),
        new(0.24f, 0.36f, 0.012f, -0.017f, 0.132f, 0.026f, 0.64f, 1.7f, 2, 1),
        new(0.58f, 0.35f, 0.011f, -0.016f, 0.122f, 0.026f, 0.58f, 4.1f, 1, 2),
        new(0.20f, 0.62f, 0.014f, -0.018f, 0.195f, 0.034f, 0.40f, 3.1f, 0, 2),
        new(0.74f, 0.68f, -0.013f, -0.017f, 0.220f, 0.032f, 0.44f, 0.7f, 0, 1),
        new(0.44f, 0.80f, 0.016f, -0.022f, 0.175f, 0.034f, 0.48f, 2.3f, 1, 0),
        new(0.86f, 0.85f, -0.012f, -0.020f, 0.148f, 0.028f, 0.52f, 5.1f, 1, 2),
    ];

    private static void DrawCircles(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        const float unitR = 120f;
        var warmGold = Argb.Lerp(c.Highlight, Argb.Hex(0xFFE5A45B), isDark ? 0.30f : 0.18f);
        Argb[] rims =
        [
            Argb.Lerp(warmGold, c.Accent, 0.24f),
            Argb.Lerp(c.Accent, c.Highlight, isDark ? 0.28f : 0.14f),
            Argb.Lerp(c.Secondary, c.Highlight, isDark ? 0.26f : 0.14f),
        ];
        Argb[] inners =
        [
            Argb.Lerp(rims[0], c.SkyUpperMid, 0.22f),
            Argb.Lerp(rims[1], c.SkyLowerMid, 0.22f),
            Argb.Lerp(rims[2], c.SkyUpperMid, 0.24f),
        ];

        float haloPeak = (isDark ? 0.085f : 0.075f) * k;
        float bokehPeak = (isDark ? 0.135f : 0.115f) * k;
        using var h0 = SoftRadial(rc, rims[0], haloPeak, unitR, c.SkyUpperMid);
        using var h1 = SoftRadial(rc, rims[1], haloPeak, unitR, c.SkyLowerMid);
        using var h2 = SoftRadial(rc, rims[2], haloPeak, unitR, c.SkyUpperMid);
        CanvasRadialGradientBrush[] halos = [h0, h1, h2];

        CanvasRadialGradientBrush MakeBokeh(Argb inner, Argb rim) => new(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = Ca(inner, bokehPeak * 0.56f) },
            new CanvasGradientStop { Position = 0.55f, Color = Ca(inner, bokehPeak * 0.68f) },
            new CanvasGradientStop { Position = 0.84f, Color = Ca(rim, bokehPeak * 0.92f) },
            new CanvasGradientStop { Position = 0.97f, Color = Ca(rim, bokehPeak * 0.65f) },
            new CanvasGradientStop { Position = 1.00f, Color = Colors.Transparent },
        ]) { Center = Vector2.Zero, RadiusX = unitR, RadiusY = unitR };

        using var b0 = MakeBokeh(inners[0], rims[0]);
        using var b1 = MakeBokeh(inners[1], rims[1]);
        using var b2 = MakeBokeh(inners[2], rims[2]);
        CanvasRadialGradientBrush[] bokehs = [b0, b1, b2];

        float rimW = MathF.Max(1.1f, minDim * 0.0020f);
        float outerRimW = MathF.Max(1.8f, minDim * 0.0052f);
        const float wrapMargin = 0.28f;
        const float wrapSpan = 1.56f;
        const float edgeFadeDist = 0.14f;

        foreach (var sp in MovingCircles)
        {
            float xd = sp.XBase + t * sp.Vx + sp.SwayAmp * MathF.Sin(t * sp.SwayFreq + sp.Phase);
            float yd = sp.YBase + t * sp.Vy + sp.SwayAmp * 0.45f * MathF.Cos(t * sp.SwayFreq * 0.85f + sp.Phase);
            float xn = PosMod(xd + wrapMargin, wrapSpan) - wrapMargin;
            float yn = PosMod(yd + wrapMargin, wrapSpan) - wrapMargin;

            float fadeX = Math.Clamp(MathF.Min((xn + wrapMargin) / edgeFadeDist, (1f + wrapMargin - xn) / edgeFadeDist), 0f, 1f);
            float fadeY = Math.Clamp(MathF.Min((yn + wrapMargin) / edgeFadeDist, (1f + wrapMargin - yn) / edgeFadeDist), 0f, 1f);
            float layerGain = sp.Layer switch { 0 => 0.56f, 1 => 0.82f, _ => 1.00f };
            float breath = 0.91f + 0.09f * MathF.Sin(t * 0.65f + sp.Phase);
            float rPx = minDim * sp.SizeRatio * breath;
            float calm = SceneCalmMask(yn, 0.50f, 0.20f, 0.40f);
            float alpha = Math.Clamp(layerGain * fadeX * fadeY * calm, 0f, 1f);
            if (alpha <= 0.01f) continue;

            float cx = w * xn, cy = h * yn;
            float hSc = rPx * 1.18f / unitR;
            DrawSoftDisc(ds, halos[sp.ColorSlot], cx, cy, unitR, hSc, hSc, 0f, alpha * 0.72f);

            float dSc = rPx / unitR;
            DrawSoftDisc(ds, bokehs[sp.ColorSlot], cx, cy, unitR, dSc, dSc, 0f, alpha);

            float rimA = Math.Clamp((isDark ? 0.24f : 0.20f) * alpha * k, 0f, 1f);
            if (rimA > 0.01f)
            {
                ds.DrawCircle(cx, cy, rPx, Ca(rims[sp.ColorSlot], rimA * 0.28f), outerRimW);
                ds.DrawCircle(cx, cy, rPx, Ca(rims[sp.ColorSlot], rimA), rimW);
            }
        }
    }

    private static void DrawParticles(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        float poolR = MathF.Max(1f, minDim * 0.48f);
        var tealTint = Argb.Lerp(c.Accent, Argb.Hex(0xFF287D8C), isDark ? 0.28f : 0.14f);
        var violetTint = Argb.Lerp(c.Secondary, c.SkyUpperMid, 0.20f);
        using (var poolA = SoftRadial(rc, tealTint, (isDark ? 0.13f : 0.09f) * k, poolR, c.SkyTop))
        using (var poolB = SoftRadial(rc, violetTint, (isDark ? 0.11f : 0.08f) * k, poolR, c.SkyUpperMid))
        {
            DrawSoftDisc(ds, poolA, w * (0.28f + 0.045f * MathF.Sin(t * 0.25f)), h * 0.16f, poolR, 1.35f, 0.85f, 0f, 0.85f + 0.15f * MathF.Cos(t * 0.32f));
            DrawSoftDisc(ds, poolB, w * (0.76f - 0.045f * MathF.Cos(t * 0.21f)), h * 0.26f, poolR, 1.25f, 0.80f, 0f, 0.85f + 0.15f * MathF.Sin(t * 0.28f));
        }

        var nodeCol = isDark ? Argb.Lerp(c.Highlight, Argb.Hex(0xFFFFFFFF), 0.30f) : Argb.Lerp(c.Accent, c.Secondary, 0.25f);
        var lineCol = isDark ? Argb.Lerp(c.Accent, c.Highlight, 0.48f) : Argb.Lerp(c.Accent, c.Secondary, 0.30f);

        float bgPeak = (isDark ? 0.42f : 0.30f) * k;
        for (int i = 0; i < 42; i++)
        {
            float xn = 0.03f + 0.94f * SceneHash(i, 11);
            float hY = SceneHash(i, 29);
            float yn = i < 32 ? 0.02f + 0.46f * hY : 0.46f + 0.36f * hY;
            float freq = 0.85f + 0.90f * SceneHash(i, 43);
            float phase = SceneHash(i, 61) * TwoPi;
            float r = MathF.Max(0.75f, minDim * (0.0010f + 0.0009f * SceneHash(i, 79)));
            float vf = Math.Clamp((0.64f - yn) / 0.28f, 0.06f, 1f);
            float tw = 0.46f + 0.54f * MathF.Sin(t * freq + phase);
            float a = Math.Clamp(bgPeak * vf * tw, 0f, 1f);
            if (a > 0.01f) ds.FillCircle(w * xn, h * yn, r, Ca(nodeCol, a));
        }

        const int nodes = 20;
        Span<float> px = stackalloc float[nodes];
        Span<float> py = stackalloc float[nodes];
        Span<float> curYn = stackalloc float[nodes];
        Span<float> curFade = stackalloc float[nodes];
        Span<float> nPhase = stackalloc float[nodes];
        Span<float> nRad = stackalloc float[nodes];

        for (int i = 0; i < nodes; i++)
        {
            float xn0 = 0.06f + 0.88f * SceneHash(i, 19);
            float rawY = SceneHash(i, 37);
            float yn0 = i < 16 ? 0.04f + 0.39f * rawY : 0.36f + 0.16f * rawY;
            float ax = 0.024f + 0.022f * SceneHash(i, 53);
            float ay = 0.018f + 0.018f * SceneHash(i, 71);
            float fx = 0.26f + 0.24f * SceneHash(i, 89);
            float fy = 0.22f + 0.22f * SceneHash(i, 103);
            float ph = SceneHash(i, 127) * TwoPi;
            bool anchor = i is 1 or 5 or 10 or 14 or 18;
            float xn = xn0 + ax * MathF.Sin(t * fx + ph);
            float yn = yn0 + ay * MathF.Cos(t * fy + ph * 1.15f);
            px[i] = w * xn;
            py[i] = h * yn;
            curYn[i] = yn;
            curFade[i] = Math.Clamp((0.56f - yn) / 0.24f, 0.06f, 1f);
            nPhase[i] = ph;
            nRad[i] = MathF.Max(anchor ? 1.6f : 1.0f, minDim * (anchor ? 0.0036f : 0.0018f + 0.0010f * SceneHash(i, 149)));
        }

        float maxDist = minDim * 0.25f;
        float linePeak = (isDark ? 0.19f : 0.16f) * k;
        float lineW = MathF.Max(0.85f, minDim * 0.0014f);
        using var roundCap = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };

        for (int i = 0; i < nodes; i++)
        {
            for (int j = i + 1; j < nodes; j++)
            {
                float d = Hypot(px[i] - px[j], py[i] - py[j]);
                if (d >= maxDist) continue;
                float u = 1f - d / maxDist;
                float midYn = (curYn[i] + curYn[j]) * 0.5f;
                float fadeMid = Math.Clamp((0.54f - midYn) / 0.24f, 0f, 1f);
                float a = Math.Clamp(linePeak * u * u * u * fadeMid, 0f, 1f);
                if (a > 0.006f) ds.DrawLine(px[i], py[i], px[j], py[j], Ca(lineCol, a), lineW, roundCap);
            }
        }

        float anchorHaloR = MathF.Max(6.0f, minDim * 0.030f);
        float glintHalf = MathF.Max(2.2f, minDim * 0.011f);
        float glintW = MathF.Max(0.75f, minDim * 0.0012f);
        float dotPeak = (isDark ? 0.72f : 0.52f) * k;
        using var anchorHalo = SoftRadial(rc, nodeCol, (isDark ? 0.38f : 0.24f) * k, anchorHaloR, tealTint);

        for (int i = 0; i < nodes; i++)
        {
            float pulse = 0.62f + 0.38f * MathF.Sin(t * 1.15f + nPhase[i]);
            float fade = curFade[i];
            if (i is 1 or 5 or 10 or 14 or 18)
            {
                float sc = 0.88f + 0.16f * pulse;
                DrawSoftDisc(ds, anchorHalo, px[i], py[i], anchorHaloR, sc, sc, 0f, pulse * fade);
                float ga = Math.Clamp((isDark ? 0.28f : 0.20f) * pulse * fade * k, 0f, 1f);
                if (ga > 0.01f)
                {
                    float sp = glintHalf * sc;
                    var gc = Ca(nodeCol, ga);
                    ds.DrawLine(px[i] - sp, py[i], px[i] + sp, py[i], gc, glintW, roundCap);
                    ds.DrawLine(px[i], py[i] - sp, px[i], py[i] + sp, gc, glintW, roundCap);
                }
            }
            float a = Math.Clamp(dotPeak * pulse * fade, 0f, 1f);
            if (a > 0.01f) ds.FillCircle(px[i], py[i], nRad[i], Ca(nodeCol, a));
        }
    }

    private readonly record struct LavaBlobSpec(
        float XBase, float CycleSpeed, float Phase, float BaseRadiusRatio,
        float SwayAmp, float SwayFreq, int Slot, bool IsSatellite);

    private static readonly LavaBlobSpec[] LavaBlobs =
    [
        new(0.26f, 0.095f, 0.45f, 0.155f, 0.030f, 0.18f, 0, false),
        new(0.70f, 0.085f, 2.05f, 0.165f, 0.028f, 0.16f, 1, false),
        new(0.48f, 0.112f, 3.65f, 0.140f, 0.034f, 0.21f, 2, false),
        new(0.80f, 0.092f, 5.10f, 0.125f, 0.030f, 0.19f, 0, false),
        new(0.36f, 0.125f, 1.35f, 0.068f, 0.032f, 0.24f, 2, true),
    ];

    private static void DrawLavaLamp(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        var skyBlend = Argb.Lerp(c.SkyUpperMid, c.SkyLowerMid, 0.50f);
        var waxPrimary = Argb.Lerp(c.Accent, skyBlend, isDark ? 0.42f : 0.48f);
        var waxSecondary = Argb.Lerp(c.Secondary, skyBlend, isDark ? 0.45f : 0.50f);
        var waxWarm = Argb.Lerp(waxPrimary, waxSecondary, 0.38f);
        var waxSoft = Argb.Lerp(waxPrimary, c.Highlight, isDark ? 0.24f : 0.30f);

        const float unitR = 100f;
        float bodyScale = Math.Clamp(k, 0.65f, 1.25f);

        float baseGlowR = minDim * 0.62f;
        using (var baseGlow = SoftRadial(rc, waxWarm, (isDark ? 0.11f : 0.09f) * k, baseGlowR, waxSecondary))
            DrawSoftDisc(ds, baseGlow, w * (0.50f + 0.024f * MathF.Sin(t * 0.22f)), h * 0.95f, baseGlowR, 1.60f, 0.44f, 0f, 0.88f + 0.12f * MathF.Cos(t * 0.34f));

        const int poolSteps = 36;
        using (var topPb = new CanvasPathBuilder(rc))
        {
            var trace = new SmoothTrace(topPb);
            for (int s = 0; s <= poolSteps; s++)
            {
                float xn = s / (float)poolSteps;
                float y = h * (0.042f + 0.011f * MathF.Sin(xn * TwoPi * 1.4f + t * 0.42f + 0.7f) + 0.006f * MathF.Cos(xn * TwoPi * 2.6f - t * 0.34f));
                if (s == 0)
                    trace.Start(w * xn, y);
                else
                    trace.To(w * xn, y);
            }
            trace.End();
            topPb.AddLine(w, 0f);
            topPb.AddLine(0f, 0f);
            topPb.EndFigure(CanvasFigureLoop.Closed);
            using var topGeo = CanvasGeometry.CreatePath(topPb);
            using var topBrush = VerticalGrad(rc, 0f, h * 0.085f, [
                (0.00f, Ca(waxSecondary, Math.Clamp(0.20f * bodyScale, 0f, 0.45f))),
                (0.65f, Ca(waxPrimary, Math.Clamp(0.11f * bodyScale, 0f, 0.35f))),
                (1.00f, Ca(waxWarm, Math.Clamp(0.03f * bodyScale, 0f, 0.20f))),
            ]);
            ds.FillGeometry(topGeo, topBrush);
        }

        float haloR = unitR * 1.95f;
        float haloPeak = (isDark ? 0.11f : 0.09f) * k;
        using var halo0 = SoftRadial(rc, waxWarm, haloPeak, haloR, waxPrimary);
        using var halo1 = SoftRadial(rc, waxSecondary, haloPeak, haloR, waxPrimary);
        using var halo2 = SoftRadial(rc, waxSoft, haloPeak, haloR, waxWarm);
        CanvasRadialGradientBrush[] halos = [halo0, halo1, halo2];

        float crownA = Math.Clamp((isDark ? 0.18f : 0.14f) * bodyScale, 0f, 0.45f);
        float midA = Math.Clamp((isDark ? 0.25f : 0.20f) * bodyScale, 0f, 0.55f);
        float bellyA = Math.Clamp((isDark ? 0.31f : 0.24f) * bodyScale, 0f, 0.65f);
        using var body0 = VerticalGrad(rc, -unitR * 1.25f, unitR * 1.25f, [
            (0.0f, Ca(Argb.Lerp(waxSecondary, waxPrimary, 0.40f), crownA)),
            (0.5f, Ca(waxWarm, midA)),
            (1.0f, Ca(Argb.Lerp(waxWarm, waxSoft, 0.45f), bellyA)),
        ]);
        using var body1 = VerticalGrad(rc, -unitR * 1.25f, unitR * 1.25f, [
            (0.0f, Ca(waxSecondary, crownA)),
            (0.5f, Ca(waxPrimary, midA)),
            (1.0f, Ca(waxWarm, bellyA)),
        ]);
        using var body2 = VerticalGrad(rc, -unitR * 1.25f, unitR * 1.25f, [
            (0.0f, Ca(waxPrimary, crownA)),
            (0.5f, Ca(Argb.Lerp(waxWarm, waxSoft, 0.25f), midA)),
            (1.0f, Ca(waxSoft, bellyA)),
        ]);
        CanvasLinearGradientBrush[] bodies = [body0, body1, body2];

        var hotCenter = new Vector2(0f, unitR * 0.22f);
        float hotR = unitR * 0.85f;
        float hotPeak = (isDark ? 0.16f : 0.13f) * k;
        using var hot0 = SoftRadial(rc, waxSoft, hotPeak, hotR, waxWarm, hotCenter);
        using var hot1 = SoftRadial(rc, Argb.Lerp(waxSoft, waxPrimary, 0.28f), hotPeak * 0.92f, hotR, waxSecondary, hotCenter);
        using var hot2 = SoftRadial(rc, Argb.Lerp(waxSoft, c.Highlight, 0.18f), hotPeak, hotR, waxSoft, hotCenter);
        CanvasRadialGradientBrush[] hots = [hot0, hot1, hot2];

        var sheenCenter = new Vector2(-unitR * 0.24f, -unitR * 0.26f);
        float sheenR = unitR * 0.52f;
        using var sheen = SoftRadial(rc, Argb.Lerp(waxSoft, c.Highlight, 0.30f), (isDark ? 0.09f : 0.11f) * k, sheenR, waxWarm, sheenCenter);

        const int samples = 32;
        Span<float> bx = stackalloc float[samples];
        Span<float> by = stackalloc float[samples];
        var savedBase = ds.Transform;

        foreach (var spec in LavaBlobs)
        {
            float phaseT = t * spec.CycleSpeed + spec.Phase;
            float rawSin = MathF.Sin(phaseT);
            float vy = MathF.Cos(phaseT);
            float dwell = rawSin * (0.82f + 0.18f * rawSin * rawSin);
            float yn = 0.50f - 0.37f * dwell;
            float xn = spec.XBase + spec.SwayAmp * MathF.Sin(t * spec.SwayFreq + spec.Phase);
            float stretchFactor = 0.11f * (1f - rawSin * rawSin);
            float wobA = spec.IsSatellite ? 0.72f : 0.62f;
            float wobB = spec.IsSatellite ? 0.54f : 0.46f;

            for (int s = 0; s < samples; s++)
            {
                float theta = TwoPi * s / samples;
                float localR = unitR * (1f
                    + stretchFactor * (-MathF.Cos(2f * theta))
                    + 0.08f * vy * MathF.Sin(theta)
                    + 0.045f * MathF.Sin(3f * theta + t * wobA + spec.Phase)
                    + 0.024f * MathF.Cos(4f * theta - t * wobB));
                bx[s] = localR * MathF.Cos(theta);
                by[s] = localR * MathF.Sin(theta);
            }

            using var pb = new CanvasPathBuilder(rc);
            pb.BeginFigure((bx[samples - 1] + bx[0]) * 0.5f, (by[samples - 1] + by[0]) * 0.5f);
            for (int s = 0; s < samples; s++)
            {
                int next = (s + 1) % samples;
                pb.AddQuadraticBezier(new Vector2(bx[s], by[s]), new Vector2((bx[s] + bx[next]) * 0.5f, (by[s] + by[next]) * 0.5f));
            }
            pb.EndFigure(CanvasFigureLoop.Closed);
            using var blobGeo = CanvasGeometry.CreatePath(pb);

            float calm = SceneCalmMask(yn, 0.48f, 0.24f, 0.48f);
            float rPx = minDim * spec.BaseRadiusRatio * (0.94f + 0.06f * MathF.Sin(t * 0.42f + spec.Phase));
            float sc = rPx / unitR;
            var blobTf = Matrix3x2.CreateScale(sc, sc) * Matrix3x2.CreateTranslation(w * xn, h * yn) * savedBase;

            ds.Transform = blobTf;
            halos[spec.Slot].Opacity = calm;
            ds.FillCircle(0f, 0f, haloR, halos[spec.Slot]);

            ds.Transform = Matrix3x2.CreateScale(1.08f, 1.08f) * blobTf;
            bodies[spec.Slot].Opacity = Math.Clamp(0.10f * calm, 0f, 1f);
            ds.FillGeometry(blobGeo, bodies[spec.Slot]);

            ds.Transform = Matrix3x2.CreateScale(1.035f, 1.035f) * blobTf;
            bodies[spec.Slot].Opacity = Math.Clamp(0.18f * calm, 0f, 1f);
            ds.FillGeometry(blobGeo, bodies[spec.Slot]);

            ds.Transform = blobTf;
            bodies[spec.Slot].Opacity = calm;
            ds.FillGeometry(blobGeo, bodies[spec.Slot]);

            using (ds.CreateLayer(1f, blobGeo))
            {
                hots[spec.Slot].Opacity = calm;
                ds.FillCircle(hotCenter, hotR, hots[spec.Slot]);
                sheen.Opacity = calm;
                ds.FillCircle(sheenCenter, sheenR, sheen);
            }
        }
        ds.Transform = savedBase;

        using (var botPb = new CanvasPathBuilder(rc))
        {
            var trace = new SmoothTrace(botPb);
            for (int s = 0; s <= poolSteps; s++)
            {
                float xn = s / (float)poolSteps;
                float y = h * (0.915f + 0.015f * MathF.Sin(xn * TwoPi * 1.3f - t * 0.42f) + 0.008f * MathF.Cos(xn * TwoPi * 2.7f + t * 0.34f));
                if (s == 0)
                    trace.Start(w * xn, y);
                else
                    trace.To(w * xn, y);
            }
            trace.End();
            botPb.AddLine(w, h);
            botPb.AddLine(0f, h);
            botPb.EndFigure(CanvasFigureLoop.Closed);
            using var botGeo = CanvasGeometry.CreatePath(botPb);
            using var rimBrush = VerticalGrad(rc, h * 0.85f, h * 0.96f, [
                (0.0f, Colors.Transparent),
                (0.5f, Ca(waxSoft, Math.Clamp(0.08f * k, 0f, 0.25f))),
                (1.0f, Colors.Transparent),
            ]);
            using var botBrush = VerticalGrad(rc, h * 0.87f, h, [
                (0.00f, Ca(waxSoft, Math.Clamp(0.16f * bodyScale, 0f, 0.45f))),
                (0.42f, Ca(waxWarm, Math.Clamp(0.24f * bodyScale, 0f, 0.55f))),
                (1.00f, Ca(Argb.Lerp(waxPrimary, waxSecondary, 0.35f), Math.Clamp(0.32f * bodyScale, 0f, 0.65f))),
            ]);

            ds.Transform = Matrix3x2.CreateTranslation(0f, -h * 0.012f) * savedBase;
            rimBrush.Opacity = 0.85f;
            ds.FillGeometry(botGeo, rimBrush);
            ds.Transform = savedBase;
            ds.FillGeometry(botGeo, botBrush);
        }
    }

    private static void DrawSpace(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        var starCore = Argb.Lerp(c.Highlight, Argb.Hex(0xFFFFFFFF), 0.45f);
        var starBlue = Argb.Lerp(c.Highlight, Argb.Hex(0xFFAAD4FF), 0.35f);
        var starGold = Argb.Lerp(c.Highlight, Argb.Hex(0xFFFFE2B8), 0.32f);

        float nebR = MathF.Max(1f, minDim * 0.42f);
        float nebBreathA = 0.90f + 0.10f * MathF.Sin(t * 0.08f);
        float nebBreathB = 0.90f + 0.10f * MathF.Cos(t * 0.07f + 1.2f);
        using (var nebA = SoftRadial(rc, Argb.Lerp(c.Accent, c.Secondary, 0.35f), (isDark ? 0.15f : 0.11f) * k, nebR, c.SkyUpperMid))
        using (var nebB = SoftRadial(rc, Argb.Lerp(c.Secondary, starBlue, 0.25f), (isDark ? 0.13f : 0.09f) * k, nebR, c.Accent))
        using (var nebC = SoftRadial(rc, Argb.Lerp(c.Accent, starGold, 0.28f), (isDark ? 0.12f : 0.08f) * k, nebR, c.SkyLowerMid))
        {
            DrawSoftDisc(ds, nebA, w * (0.24f + 0.015f * MathF.Sin(t * 0.04f)), h * 0.22f, nebR, 1.42f, 0.72f, -18f, nebBreathA);
            DrawSoftDisc(ds, nebB, w * (0.64f - 0.014f * MathF.Cos(t * 0.05f)), h * 0.17f, nebR, 1.28f, 0.66f, 14f, nebBreathB);
            DrawSoftDisc(ds, nebC, w * (0.76f + 0.012f * MathF.Sin(t * 0.04f)), h * 0.78f, nebR, 1.48f, 0.68f, -12f, nebBreathA);
        }

        float baseA = isDark ? 0.60f : 0.18f;
        for (int i = 0; i < 48; i++)
        {
            float xn = (SceneHash(i, 11) + t * 0.0008f) % 1f;
            float yn = 0.03f + 0.92f * SceneHash(i, 23);
            float r = minDim * (0.0010f + 0.0012f * SceneHash(i, 41));
            float phase = SceneHash(i, 59) * TwoPi;
            float speed = 0.28f + 0.32f * SceneHash(i, 73);
            float calm = SceneCalmMask(yn, 0.48f, 0.24f, 0.72f);
            float tw = 0.54f + 0.46f * MathF.Sin(t * speed + phase);
            float a = baseA * tw * calm * k;
            if (a > 0.01f) ds.FillCircle(w * xn, h * yn, r, Ca(starCore, a));
        }

        for (int i = 0; i < 22; i++)
        {
            float xn = (SceneHash(i, 97) + t * 0.0014f) % 1f;
            float yn = 0.04f + 0.90f * SceneHash(i, 113);
            float r = minDim * (0.0017f + 0.0014f * SceneHash(i, 131));
            float phase = SceneHash(i, 149) * TwoPi;
            float warmth = SceneHash(i, 167);
            float calm = SceneCalmMask(yn, 0.48f, 0.24f, 0.74f);
            float tw = 0.48f + 0.52f * MathF.Sin(t * 0.42f + phase);
            float a = baseA * 1.15f * tw * calm * k;
            if (a > 0.01f) ds.FillCircle(w * xn, h * yn, r, Ca(Argb.Lerp(starBlue, starGold, warmth), a));
        }

        ReadOnlySpan<float> gXn = [0.18f, 0.42f, 0.67f, 0.84f];
        ReadOnlySpan<float> gYn = [0.13f, 0.24f, 0.11f, 0.26f];
        ReadOnlySpan<float> gPh = [0.5f, 2.2f, 4.1f, 5.4f];
        float gHaloR = MathF.Max(1f, minDim * 0.026f);
        float armLen = MathF.Max(2f, minDim * 0.022f);
        float armW = MathF.Max(0.7f, minDim * 0.0014f);

        using (var gHalo = SoftRadial(rc, starBlue, (isDark ? 0.42f : 0.26f) * k, gHaloR, c.Accent))
        using (var armBrush = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.0f, Color = Colors.Transparent },
            new CanvasGradientStop { Position = 0.5f, Color = Ca(starCore, (isDark ? 0.42f : 0.24f) * k) },
            new CanvasGradientStop { Position = 1.0f, Color = Colors.Transparent },
        ]) { StartPoint = new Vector2(-armLen, 0f), EndPoint = new Vector2(armLen, 0f) })
        {
            var saved = ds.Transform;
            for (int i = 0; i < 4; i++)
            {
                float px = w * ((gXn[i] + t * 0.0016f) % 1f);
                float py = h * gYn[i];
                float pulse = 0.62f + 0.38f * MathF.Sin(t * 0.46f + gPh[i]);
                float calm = SceneCalmMask(gYn[i], 0.48f, 0.24f, 0.65f);
                float sc = 0.85f + 0.25f * pulse;
                DrawSoftDisc(ds, gHalo, px, py, gHaloR, sc, sc, 0f, pulse * calm);

                float armA = Math.Clamp(pulse * calm, 0f, 1f);
                float armSc = 0.75f + 0.35f * pulse;
                armBrush.Opacity = armA;

                ds.Transform = Matrix3x2.CreateScale(armSc, 1f) * Matrix3x2.CreateTranslation(px, py) * saved;
                ds.DrawLine(-armLen, 0f, armLen, 0f, armBrush, armW);

                ds.Transform = Matrix3x2.CreateScale(armSc, 1f) * Matrix3x2.CreateRotation(MathF.PI * 0.5f) * Matrix3x2.CreateTranslation(px, py) * saved;
                ds.DrawLine(-armLen, 0f, armLen, 0f, armBrush, armW);

                ds.Transform = saved;
                ds.FillCircle(px, py, minDim * 0.0025f, Ca(starCore, (isDark ? 0.86f : 0.44f) * pulse * k));
            }
        }

        DrawMeteor(ds, rc, w, h, minDim, starBlue, starCore, k, isDark, t, 20f, 1.1f, 0.4f, 0.16f, 0.20f, 0.09f, 0.32f, 307, 331, 0.0020f, 0.56f);
    }

    private static void DrawSilk(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        var silkWarm = Argb.Lerp(c.Accent, Argb.Hex(0xFFE07A86), 0.35f);
        var silkCool = Argb.Lerp(c.Secondary, Argb.Hex(0xFF4AC2D4), 0.35f);
        var silkMid = Argb.Lerp(silkWarm, silkCool, 0.50f);

        float orbR = MathF.Max(1f, minDim * 0.22f);
        using (var orb = SoftRadial(rc, Argb.Lerp(silkWarm, c.Highlight, 0.25f), (isDark ? 0.16f : 0.14f) * k, orbR, c.SkyUpperMid))
            DrawSoftDisc(ds, orb, w * (0.84f + 0.025f * MathF.Sin(t * 0.38f)), h * (0.065f + 0.018f * MathF.Cos(t * 0.32f)), orbR, 1f, 1f, 0f, 0.85f + 0.15f * MathF.Sin(t * 0.75f));

        using var upperBrush = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = Ca(silkWarm, 0f) },
            new CanvasGradientStop { Position = 0.18f, Color = Ca(silkWarm, (isDark ? 0.28f : 0.22f) * k) },
            new CanvasGradientStop { Position = 0.50f, Color = Ca(silkMid, (isDark ? 0.32f : 0.24f) * k) },
            new CanvasGradientStop { Position = 0.82f, Color = Ca(silkCool, (isDark ? 0.28f : 0.22f) * k) },
            new CanvasGradientStop { Position = 1.00f, Color = Ca(silkCool, 0f) },
        ]) { StartPoint = Vector2.Zero, EndPoint = new Vector2(w, 0f) };

        using var lowerBrush = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = Ca(silkCool, 0f) },
            new CanvasGradientStop { Position = 0.22f, Color = Ca(silkCool, (isDark ? 0.24f : 0.19f) * k) },
            new CanvasGradientStop { Position = 0.52f, Color = Ca(silkMid, (isDark ? 0.26f : 0.20f) * k) },
            new CanvasGradientStop { Position = 0.82f, Color = Ca(silkWarm, (isDark ? 0.24f : 0.19f) * k) },
            new CanvasGradientStop { Position = 1.00f, Color = Ca(silkWarm, 0f) },
        ]) { StartPoint = Vector2.Zero, EndPoint = new Vector2(w, 0f) };

        using var style = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round, LineJoin = CanvasLineJoin.Round };
        float strokeW = MathF.Max(0.75f, minDim * 0.0014f);

        void DrawRibbon(float baseY, float spineAmp, float twistAmp, float spinePhase, float twistPhase, float speed, CanvasLinearGradientBrush brush)
        {
            const int strands = 26;
            const int steps = 36;
            for (int strand = 0; strand < strands; strand++)
            {
                float u = strand / (float)(strands - 1) - 0.5f;
                float weight = Math.Clamp(1f - MathF.Abs(u) * 1.15f, 0.32f, 1f);
                using var pb = new CanvasPathBuilder(rc);
                var trace = new SmoothTrace(pb);
                for (int s = 0; s <= steps; s++)
                {
                    float x = -w * 0.04f + w * 1.08f * (s / (float)steps);
                    float xn = x / w;
                    float spine = h * baseY + h * spineAmp * MathF.Sin(xn * TwoPi * 1.15f + t * speed + spinePhase);
                    float halfSpan = h * twistAmp * MathF.Sin(xn * TwoPi * 0.92f - t * (speed * 0.72f) + u * 0.42f + twistPhase);
                    float shimmer = h * 0.018f * MathF.Cos(xn * TwoPi * 2.1f + u * 2.4f + t * speed * 1.15f);
                    float y = spine + halfSpan * (0.45f + 1.10f * u) + shimmer;
                    if (s == 0)
                        trace.Start(x, y);
                    else
                        trace.To(x, y);
                }
                trace.End();
                pb.EndFigure(CanvasFigureLoop.Open);
                using var geo = CanvasGeometry.CreatePath(pb);
                brush.Opacity = weight;
                ds.DrawGeometry(geo, brush, strokeW, style);
            }
        }

        DrawRibbon(0.215f, 0.056f, 0.130f, 0.4f, 0.9f, 0.68f, upperBrush);
        DrawRibbon(0.785f, 0.052f, 0.120f, 2.6f, 3.1f, -0.56f, lowerBrush);
    }

    private static void DrawContours(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        float peakX = w * (0.78f + 0.020f * MathF.Sin(t * 0.38f));
        float peakY = h * (0.16f + 0.015f * MathF.Cos(t * 0.31f));
        var contourTint = Argb.Lerp(c.Accent, c.Secondary, 0.35f);
        var summitAmber = Argb.Lerp(c.Secondary, Argb.Hex(0xFFE0B066), 0.55f);

        float haloR = MathF.Max(1f, minDim * 0.042f);
        using (var halo = SoftRadial(rc, summitAmber, (isDark ? 0.45f : 0.38f) * k, haloR, contourTint))
            DrawSoftDisc(ds, halo, peakX, peakY, haloR, 1f, 1f, 0f, 0.80f + 0.20f * MathF.Sin(t * 1.35f));
        ds.FillCircle(peakX, peakY, minDim * 0.0045f, Ca(summitAmber, 0.88f * k));

        using var fadeBrush = VerticalGrad(rc, 0f, h * 0.65f, [
            (0.00f, Ca(contourTint, (isDark ? 0.42f : 0.34f) * k)),
            (0.26f, Ca(contourTint, (isDark ? 0.30f : 0.24f) * k)),
            (0.48f, Ca(contourTint, (isDark ? 0.14f : 0.10f) * k)),
            (0.62f, Colors.Transparent),
            (1.00f, Colors.Transparent),
        ]);
        using var style = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round, LineJoin = CanvasLineJoin.Round };
        float thinW = MathF.Max(0.75f, minDim * 0.0014f);
        float idxW = MathF.Max(1.25f, minDim * 0.0024f);

        const int rings = 16;
        const int samples = 40;
        float tiltRad = -28f * DegToRad;
        Span<float> px = stackalloc float[samples];
        Span<float> py = stackalloc float[samples];

        float cycleProg = t * 0.28f;
        int cycleFloor = (int)MathF.Floor(cycleProg);
        float flow = cycleProg - cycleFloor;

        for (int rIdx = 0; rIdx < rings; rIdx++)
        {
            float frac = (rIdx + flow) / rings;
            float baseR = minDim * (0.032f + frac * (rings * 0.042f));

            for (int s = 0; s < samples; s++)
            {
                float theta = TwoPi * s / samples;
                float tl = theta - tiltRad;
                float harmonics = 0.24f * MathF.Cos(2f * tl + t * 0.36f)
                    + 0.12f * MathF.Sin(3f * tl - t * 0.48f + 0.8f + 0.15f * frac)
                    + 0.06f * MathF.Cos(5f * tl + t * 0.62f - 0.5f);
                float breathe = 0.036f * MathF.Sin(t * 0.75f + frac * 5.0f + tl);
                float r = baseR * (1f + harmonics + breathe);
                px[s] = peakX + r * (1.08f * MathF.Cos(theta) - 0.06f * MathF.Sin(tl));
                py[s] = peakY + r * (0.92f * MathF.Sin(theta) + 0.05f * MathF.Cos(tl));
            }

            bool isIndex = ((rIdx - cycleFloor) % 4 + 4) % 4 == 0;
            float birth = Math.Clamp(frac / 0.10f, 0f, 1f);
            float outer = Math.Clamp((1f - frac) / 0.22f, 0f, 1f);
            float pulse = 0.82f + 0.18f * MathF.Sin(frac * TwoPi * 2f - t * 1.4f);
            float alpha = Math.Clamp(birth * outer * pulse * (isIndex ? 1.0f : 0.74f), 0f, 1f);
            if (alpha <= 0.006f) continue;

            using var pb = new CanvasPathBuilder(rc);
            pb.BeginFigure((px[samples - 1] + px[0]) * 0.5f, (py[samples - 1] + py[0]) * 0.5f);
            for (int s = 0; s < samples; s++)
            {
                int next = (s + 1) % samples;
                pb.AddQuadraticBezier(new Vector2(px[s], py[s]), new Vector2((px[s] + px[next]) * 0.5f, (py[s] + py[next]) * 0.5f));
            }
            pb.EndFigure(CanvasFigureLoop.Closed);
            using var geo = CanvasGeometry.CreatePath(pb);
            fadeBrush.Opacity = alpha;
            ds.DrawGeometry(geo, fadeBrush, isIndex ? idxW : thinW, style);
        }
    }

    private static void DrawMosaic(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        const int cols = 7;
        const int rows = 6;
        const int vCount = cols * rows;
        Span<float> lx = stackalloc float[vCount];
        Span<float> ly = stackalloc float[vCount];

        for (int r = 0; r < rows; r++)
        {
            float yGrid = -0.04f + (r / (float)(rows - 1)) * 0.60f;
            for (int col = 0; col < cols; col++)
            {
                int idx = r * cols + col;
                float xGrid = -0.06f + (col / (float)(cols - 1)) * 1.12f;
                bool pinX = col == 0 || col == cols - 1;
                bool pinY = r == 0 || r == rows - 1;
                float bx = xGrid + (pinX ? 0f : (SceneHash(idx, 13) - 0.5f) * 0.11f);
                float by = yGrid + (r == 0 ? 0f : (SceneHash(idx, 29) - 0.5f) * 0.065f);
                if (pinX || pinY)
                {
                    lx[idx] = w * bx;
                    ly[idx] = h * by;
                }
                else
                {
                    float ph = SceneHash(idx, 47) * TwoPi;
                    float sp = 0.58f + 0.46f * SceneHash(idx, 61);
                    lx[idx] = w * (bx + 0.032f * MathF.Cos(t * sp + ph));
                    ly[idx] = h * (by + 0.026f * MathF.Sin(t * sp * 0.85f + ph * 1.2f));
                }
            }
        }

        var baseShade = Argb.Lerp(c.SkyUpperMid, c.Shade, 0.35f);
        var edgeTint = Argb.Lerp(c.Highlight, c.Accent, 0.30f);
        float edgeW = MathF.Max(0.75f, minDim * 0.0012f);
        using var bevel = new CanvasStrokeStyle { LineJoin = CanvasLineJoin.Bevel };

        void DrawTri(float ax, float ay, float bx, float by, float cx, float cy, int triIdx)
        {
            float cxn = ((ax + bx + cx) / 3f) / w;
            float cyn = ((ay + by + cy) / 3f) / h;
            float fade = Math.Clamp((0.56f - cyn) / 0.34f, 0f, 1f);
            float vAtten = fade * fade;
            float wave01 = 0.5f + 0.5f * MathF.Sin(t * 1.25f - cxn * 3.0f - cyn * 2.2f + triIdx * 0.37f);
            float baseA = (isDark ? 0.24f : 0.18f) + 0.14f * SceneHash(triIdx, 131);
            float fillA = Math.Clamp(baseA * vAtten * (0.60f + 0.50f * wave01) * k, 0f, 0.72f);
            if (fillA <= 0.005f) return;

            var jewel = Argb.Lerp(c.Accent, c.Secondary, SceneHash(triIdx, 83));
            var col = Argb.Lerp(baseShade, jewel, 0.28f + 0.42f * SceneHash(triIdx, 107));

            using var pb = new CanvasPathBuilder(rc);
            pb.BeginFigure(ax, ay);
            pb.AddLine(bx, by);
            pb.AddLine(cx, cy);
            pb.EndFigure(CanvasFigureLoop.Closed);
            using var geo = CanvasGeometry.CreatePath(pb);
            ds.FillGeometry(geo, Ca(col, fillA));

            float edgeA = Math.Clamp(fillA * (0.22f + 0.24f * wave01), 0f, 0.38f);
            if (edgeA > 0.004f) ds.DrawGeometry(geo, Ca(edgeTint, edgeA), edgeW, bevel);
        }

        int ti = 0;
        for (int r = 0; r < rows - 1; r++)
        {
            for (int col = 0; col < cols - 1; col++)
            {
                int i00 = r * cols + col, i10 = i00 + 1, i01 = (r + 1) * cols + col, i11 = i01 + 1;
                if ((r + col) % 2 == 0)
                {
                    DrawTri(lx[i00], ly[i00], lx[i10], ly[i10], lx[i11], ly[i11], ti++);
                    DrawTri(lx[i00], ly[i00], lx[i11], ly[i11], lx[i01], ly[i01], ti++);
                }
                else
                {
                    DrawTri(lx[i00], ly[i00], lx[i10], ly[i10], lx[i01], ly[i01], ti++);
                    DrawTri(lx[i10], ly[i10], lx[i11], ly[i11], lx[i01], ly[i01], ti++);
                }
            }
        }
    }

    private static void DrawWaves(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        var wavePrimary = Argb.Lerp(c.Accent, c.Highlight, isDark ? 0.18f : 0.12f);
        var waveSecondary = Argb.Lerp(c.Secondary, c.Shade, isDark ? 0.22f : 0.16f);

        ReadOnlySpan<float> bases = [0.645f, 0.705f, 0.768f, 0.832f, 0.896f];
        ReadOnlySpan<float> amps = [0.032f, 0.035f, 0.033f, 0.029f, 0.024f];
        ReadOnlySpan<float> speeds = [-0.62f, 0.72f, -0.56f, 0.48f, -0.38f];
        ReadOnlySpan<float> startYs = [0.60f, 0.66f, 0.73f, 0.80f, 0.87f];
        ReadOnlySpan<float> topAlphas = isDark ? [0.18f, 0.26f, 0.35f, 0.45f, 0.58f] : [0.14f, 0.19f, 0.25f, 0.32f, 0.40f];
        ReadOnlySpan<float> botAlphas = isDark ? [0.36f, 0.46f, 0.58f, 0.70f, 0.84f] : [0.24f, 0.31f, 0.39f, 0.48f, 0.58f];
        Argb[] topCols =
        [
            Argb.Lerp(c.Shade, waveSecondary, 0.28f),
            Argb.Lerp(c.Shade, wavePrimary, 0.32f),
            Argb.Lerp(c.Shade, wavePrimary, 0.24f),
            Argb.Lerp(c.Ink, wavePrimary, 0.24f),
            Argb.Lerp(c.Ink, waveSecondary, 0.18f),
        ];

        const int steps = 56;
        float x0 = -w * 0.03f, xLast = w * 1.03f;
        for (int b = 0; b < 5; b++)
        {
            float baseY = h * bases[b];
            float amp = h * amps[b];
            float speed = speeds[b];
            using var pb = new CanvasPathBuilder(rc);
            var trace = new SmoothTrace(pb);
            for (int s = 0; s <= steps; s++)
            {
                float x = x0 + w * 1.06f * (s / (float)steps);
                float xn = x / w;
                float y = baseY + amp * (
                    0.68f * MathF.Sin(xn * TwoPi * (1.18f + b * 0.20f) + t * speed + b * 1.40f)
                    + 0.32f * MathF.Cos(xn * TwoPi * (2.30f - b * 0.16f) - t * speed * 0.62f));
                if (s == 0)
                    trace.Start(x, y);
                else
                    trace.To(x, y);
            }
            trace.End();
            pb.AddLine(xLast, h);
            pb.AddLine(x0, h);
            pb.EndFigure(CanvasFigureLoop.Closed);
            using var geo = CanvasGeometry.CreatePath(pb);
            using var brush = VerticalGrad(rc, h * startYs[b], h, [
                (0f, Ca(topCols[b], topAlphas[b] * k)),
                (1f, Ca(c.Ink, botAlphas[b] * k)),
            ]);
            ds.FillGeometry(geo, brush);
        }
    }

    private readonly record struct HarvestLeafSpec(
        float XBase, float Phase, float FallSpeed, float SwayAmp,
        float SwayFreq, float RotAmpDeg, float Scale, int PaletteIdx, bool IsMaple);

    private static readonly HarvestLeafSpec[] HarvestLeaves =
    [
        new(0.14f, 0.08f, 0.036f, 0.042f, 1.08f, 30f, 0.92f, 0, true),
        new(0.32f, 0.22f, 0.045f, 0.048f, 0.98f, 36f, 0.78f, 1, false),
        new(0.54f, 0.14f, 0.039f, 0.038f, 1.24f, 28f, 0.86f, 2, true),
        new(0.76f, 0.30f, 0.048f, 0.054f, 1.04f, 34f, 0.96f, 0, true),
        new(0.88f, 0.04f, 0.032f, 0.036f, 0.95f, 26f, 0.72f, 1, false),
        new(0.22f, 0.48f, 0.042f, 0.046f, 1.15f, 32f, 0.82f, 2, false),
        new(0.66f, 0.56f, 0.037f, 0.044f, 1.32f, 29f, 0.90f, 0, true),
        new(0.42f, 0.72f, 0.046f, 0.050f, 1.00f, 38f, 0.76f, 1, true),
        new(0.12f, 0.86f, 0.040f, 0.040f, 1.40f, 28f, 0.84f, 0, false),
        new(0.82f, 0.92f, 0.043f, 0.045f, 1.12f, 32f, 0.74f, 2, true),
        new(0.30f, 0.98f, 0.034f, 0.037f, 1.18f, 27f, 0.68f, 1, false),
        new(0.68f, 0.80f, 0.039f, 0.041f, 1.06f, 31f, 0.80f, 2, true),
    ];

    private static void DrawHarvest(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float detail, float t)
    {
        float minDim = MathF.Min(w, h);
        float k = Math.Clamp(detail, 0.40f, 1.35f);
        bool isDark = c.IsDark;

        var autumnGold = Argb.Lerp(c.Accent, Argb.Hex(0xFFD49A4A), 0.52f);
        var autumnCopper = Argb.Lerp(c.Accent, Argb.Hex(0xFFC86D44), 0.52f);
        var autumnRusset = Argb.Lerp(c.Secondary, Argb.Hex(0xFFA84C38), 0.50f);
        Argb[] leafCols = [autumnGold, autumnCopper, autumnRusset];
        float leafBaseA = Math.Clamp((isDark ? 0.68f : 0.56f) * k, 0f, 0.88f);
        var veinCol = Argb.Lerp(c.Highlight, autumnGold, 0.35f);
        float veinA = (isDark ? 0.28f : 0.24f) * k;

        float glowR = MathF.Max(1f, minDim * 0.34f);
        using (var gLeft = SoftRadial(rc, autumnGold, (isDark ? 0.20f : 0.16f) * k, glowR, autumnCopper))
        using (var gRight = SoftRadial(rc, autumnCopper, (isDark ? 0.18f : 0.15f) * k, glowR, autumnRusset))
        {
            DrawSoftDisc(ds, gLeft, w * (0.20f + 0.015f * MathF.Sin(t * 0.45f)), h * 0.08f, glowR, 1.45f, 0.82f, 0f, 0.85f + 0.15f * MathF.Sin(t * 0.55f));
            DrawSoftDisc(ds, gRight, w * (0.82f - 0.015f * MathF.Cos(t * 0.42f)), h * 0.11f, glowR, 1.40f, 0.85f, 0f, 0.85f + 0.15f * MathF.Cos(t * 0.52f));
        }

        var savedBase = ds.Transform;
        using (var clPb = new CanvasPathBuilder(rc))
        using (var crPb = new CanvasPathBuilder(rc))
        {
            clPb.BeginFigure(0f, 0f);
            clPb.AddLine(w * 0.52f, 0f);
            clPb.AddCubicBezier(new Vector2(w * 0.44f, h * 0.08f), new Vector2(w * 0.30f, h * 0.11f), new Vector2(w * 0.22f, h * 0.16f));
            clPb.AddCubicBezier(new Vector2(w * 0.14f, h * 0.21f), new Vector2(w * 0.06f, h * 0.22f), new Vector2(0f, h * 0.28f));
            clPb.EndFigure(CanvasFigureLoop.Closed);

            crPb.BeginFigure(w, 0f);
            crPb.AddLine(w * 0.44f, 0f);
            crPb.AddCubicBezier(new Vector2(w * 0.56f, h * 0.07f), new Vector2(w * 0.72f, h * 0.12f), new Vector2(w * 0.80f, h * 0.17f));
            crPb.AddCubicBezier(new Vector2(w * 0.89f, h * 0.22f), new Vector2(w * 0.95f, h * 0.22f), new Vector2(w, h * 0.29f));
            crPb.EndFigure(CanvasFigureLoop.Closed);

            using var clGeo = CanvasGeometry.CreatePath(clPb);
            using var crGeo = CanvasGeometry.CreatePath(crPb);
            using var cbL = VerticalGrad(rc, 0f, h * 0.30f, [(0f, Ca(autumnRusset, (isDark ? 0.26f : 0.16f) * k)), (1f, Colors.Transparent)]);
            using var cbR = VerticalGrad(rc, 0f, h * 0.30f, [(0f, Ca(autumnCopper, (isDark ? 0.24f : 0.15f) * k)), (1f, Colors.Transparent)]);

            ds.Transform = Matrix3x2.CreateRotation(2.2f * MathF.Sin(t * 0.65f) * DegToRad) * savedBase;
            ds.FillGeometry(clGeo, cbL);

            ds.Transform = Matrix3x2.CreateTranslation(-w, 0f)
                * Matrix3x2.CreateRotation(-2.2f * MathF.Sin(t * 0.65f + 1.1f) * DegToRad)
                * Matrix3x2.CreateTranslation(w, 0f) * savedBase;
            ds.FillGeometry(crGeo, cbR);
            ds.Transform = savedBase;
        }

        const float leafR = 24f;
        using var mapleGeo = BuildMapleLeaf(rc, leafR);
        using var oakGeo = BuildOakLeaf(rc, leafR);
        using var veinPb = new CanvasPathBuilder(rc);
        veinPb.BeginFigure(0f, leafR * 1.15f);
        veinPb.AddLine(0f, -leafR * 0.78f);
        veinPb.EndFigure(CanvasFigureLoop.Open);
        veinPb.BeginFigure(0f, leafR * 0.15f);
        veinPb.AddLine(-leafR * 0.42f, -leafR * 0.22f);
        veinPb.EndFigure(CanvasFigureLoop.Open);
        veinPb.BeginFigure(0f, leafR * 0.15f);
        veinPb.AddLine(leafR * 0.42f, -leafR * 0.22f);
        veinPb.EndFigure(CanvasFigureLoop.Open);
        using var veinGeo = CanvasGeometry.CreatePath(veinPb);
        using var roundCap = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };

        float scaleUnit = (minDim * 0.024f) / leafR;
        for (int idx = 0; idx < HarvestLeaves.Length; idx++)
        {
            var sp = HarvestLeaves[idx];
            float yn = -0.04f + (sp.Phase + t * sp.FallSpeed) % 1.06f;
            float xn = sp.XBase + sp.SwayAmp * MathF.Sin(t * sp.SwayFreq + sp.Phase * TwoPi);
            float topFade = Math.Clamp((yn + 0.04f) / 0.08f, 0f, 1f);
            float midFade = yn < 0.35f ? 1f
                : yn < 0.65f ? (1f - (yn - 0.35f) / 0.30f) * 0.88f + 0.12f
                : yn < 0.88f ? 0.14f + 0.22f * ((yn - 0.65f) / 0.23f)
                : Math.Clamp((1.02f - yn) / 0.14f, 0f, 1f) * 0.36f;
            float calm = SceneCalmMask(yn, 0.48f, 0.18f, 0.55f);
            float alpha = Math.Clamp(topFade * midFade * calm, 0f, 1f);
            if (alpha <= 0.02f) continue;

            float rotDeg = sp.RotAmpDeg * MathF.Sin(t * sp.SwayFreq * 1.15f + sp.Phase * 5f) + (idx * 37f) % 50f - 25f;
            float sc = scaleUnit * sp.Scale;
            float flutterX = sc * (0.72f + 0.28f * MathF.Cos(t * sp.SwayFreq * 1.35f + idx));

            ds.Transform = Matrix3x2.CreateScale(flutterX, sc) * Matrix3x2.CreateRotation(rotDeg * DegToRad) * Matrix3x2.CreateTranslation(w * xn, h * yn) * savedBase;
            ds.FillGeometry(sp.IsMaple ? mapleGeo : oakGeo, Ca(leafCols[sp.PaletteIdx], leafBaseA * alpha));
            ds.DrawGeometry(veinGeo, Ca(veinCol, veinA * alpha), 1.2f, roundCap);
        }
        ds.Transform = savedBase;
    }

    private static CanvasGeometry BuildMapleLeaf(ICanvasResourceCreator rc, float r)
    {
        using var pb = new CanvasPathBuilder(rc);
        pb.BeginFigure(0f, -r);
        pb.AddLine(r * 0.22f, -r * 0.48f);
        pb.AddLine(r * 0.78f, -r * 0.58f);
        pb.AddLine(r * 0.46f, -r * 0.12f);
        pb.AddLine(r * 0.92f, r * 0.16f);
        pb.AddLine(r * 0.34f, r * 0.36f);
        pb.AddLine(r * 0.42f, r * 0.68f);
        pb.AddLine(r * 0.06f, r * 0.58f);
        pb.AddLine(0f, r * 1.05f);
        pb.AddLine(-r * 0.06f, r * 0.58f);
        pb.AddLine(-r * 0.42f, r * 0.68f);
        pb.AddLine(-r * 0.34f, r * 0.36f);
        pb.AddLine(-r * 0.92f, r * 0.16f);
        pb.AddLine(-r * 0.46f, -r * 0.12f);
        pb.AddLine(-r * 0.78f, -r * 0.58f);
        pb.AddLine(-r * 0.22f, -r * 0.48f);
        pb.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(pb);
    }

    private static CanvasGeometry BuildOakLeaf(ICanvasResourceCreator rc, float r)
    {
        using var pb = new CanvasPathBuilder(rc);
        pb.BeginFigure(0f, -r * 0.98f);
        pb.AddCubicBezier(new Vector2(r * 0.22f, -r * 0.92f), new Vector2(r * 0.36f, -r * 0.74f), new Vector2(r * 0.24f, -r * 0.58f));
        pb.AddCubicBezier(new Vector2(r * 0.58f, -r * 0.52f), new Vector2(r * 0.66f, -r * 0.26f), new Vector2(r * 0.36f, -r * 0.14f));
        pb.AddCubicBezier(new Vector2(r * 0.72f, -r * 0.02f), new Vector2(r * 0.68f, r * 0.28f), new Vector2(r * 0.32f, r * 0.34f));
        pb.AddCubicBezier(new Vector2(r * 0.44f, r * 0.52f), new Vector2(r * 0.24f, r * 0.68f), new Vector2(r * 0.05f, r * 0.62f));
        pb.AddLine(0f, r * 1.02f);
        pb.AddLine(-r * 0.05f, r * 0.62f);
        pb.AddCubicBezier(new Vector2(-r * 0.24f, r * 0.68f), new Vector2(-r * 0.44f, r * 0.52f), new Vector2(-r * 0.32f, r * 0.34f));
        pb.AddCubicBezier(new Vector2(-r * 0.68f, r * 0.28f), new Vector2(-r * 0.72f, -r * 0.02f), new Vector2(-r * 0.36f, -r * 0.14f));
        pb.AddCubicBezier(new Vector2(-r * 0.66f, -r * 0.26f), new Vector2(-r * 0.58f, -r * 0.52f), new Vector2(-r * 0.24f, -r * 0.58f));
        pb.AddCubicBezier(new Vector2(-r * 0.36f, -r * 0.74f), new Vector2(-r * 0.22f, -r * 0.92f), new Vector2(0f, -r * 0.98f));
        pb.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(pb);
    }

    private static void DrawMeteor(
        CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, float minDim,
        Argb tailColor, Argb headColor, float k, bool isDark, float t,
        float every, float duration, float offset, float xStartMin, float dxFrac, float dyFrac,
        float tailFrac, int saltX, int saltY, float strokeFrac, float darkPeak)
    {
        int slot = (int)(t / every);
        float local = t % every;
        if (local < offset || local > offset + duration) return;

        float u = (local - offset) / duration;
        float fade = Math.Clamp(MathF.Sin(u * MathF.PI), 0f, 1f);
        float startX = w * (xStartMin + 0.42f * SceneHash(slot, saltX));
        float startY = h * (0.06f + 0.16f * SceneHash(slot, saltY));
        float dx = w * dxFrac, dy = h * dyFrac;
        float headX = startX + dx * u, headY = startY + dy * u;
        float tailU = MathF.Max(0f, u - tailFrac);
        float tailX = startX + dx * tailU, tailY = startY + dy * tailU;
        float segLen = Hypot(headX - tailX, headY - tailY);
        if (segLen <= 1f) return;

        float maxLen = w * 0.14f;
        float peakA = (isDark ? darkPeak : 0.28f) * k;
        using var brush = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = Colors.Transparent },
            new CanvasGradientStop { Position = 0.55f, Color = Ca(tailColor, peakA * 0.35f) },
            new CanvasGradientStop { Position = 1.00f, Color = Ca(headColor, peakA) },
        ]) { StartPoint = new Vector2(-maxLen, 0f), EndPoint = Vector2.Zero, Opacity = fade };
        using var cap = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };

        var saved = ds.Transform;
        float angleRad = MathF.Atan2(dy, dx);
        ds.Transform = Matrix3x2.CreateScale(segLen / maxLen, 1f) * Matrix3x2.CreateRotation(angleRad) * Matrix3x2.CreateTranslation(headX, headY) * saved;
        ds.DrawLine(-maxLen, 0f, 0f, 0f, brush, MathF.Max(0.8f, minDim * strokeFrac), cap);
        ds.Transform = saved;
    }

    private static void BuildRidgeAndRibbon(
        ICanvasResourceCreator rc, float w, float h, int steps, float ribbonMaxDepth,
        float topLiftFrac, float minTaper, Func<float, float> elevationAt,
        out CanvasGeometry ridgeGeo, out CanvasGeometry ribbonGeo)
    {
        float[] xs = new float[steps + 1];
        float[] ys = new float[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float xn = i / (float)steps;
            xs[i] = w * xn;
            ys[i] = elevationAt(xn);
        }

        using var ridgePb = new CanvasPathBuilder(rc);
        var rt = new SmoothTrace(ridgePb);
        rt.Start(xs[0], ys[0]);
        for (int i = 1; i <= steps; i++) rt.To(xs[i], ys[i]);
        rt.End();
        ridgePb.AddLine(w, h);
        ridgePb.AddLine(0f, h);
        ridgePb.EndFigure(CanvasFigureLoop.Closed);
        ridgeGeo = CanvasGeometry.CreatePath(ridgePb);

        using var ribPb = new CanvasPathBuilder(rc);
        var rbt = new SmoothTrace(ribPb);
        float topLift = ribbonMaxDepth * topLiftFrac;
        rbt.Start(xs[0], ys[0] - topLift);
        for (int i = 1; i <= steps; i++) rbt.To(xs[i], ys[i] - topLift);
        rbt.End();
        for (int i = steps; i >= 0; i--)
        {
            float xn = i / (float)steps;
            float taper = Math.Clamp(MathF.Sin(xn * MathF.PI), minTaper, 1f);
            float depth = ribbonMaxDepth * taper;
            if (i == steps) rbt.Start(xs[i], ys[i] + depth, join: true);
            else rbt.To(xs[i], ys[i] + depth);
        }
        rbt.End();
        ribPb.EndFigure(CanvasFigureLoop.Closed);
        ribbonGeo = CanvasGeometry.CreatePath(ribPb);
    }

    private static CanvasRadialGradientBrush SoftRadial(
        ICanvasResourceCreator rc, Argb color, float peakAlpha, float radius, Argb edgeTint, Vector2 center = default)
    {
        float peak = Math.Clamp(peakAlpha, 0f, 1f);
        float rSafe = MathF.Max(0.5f, radius);
        var stops = new CanvasGradientStop[SoftRadialStops.Length];
        bool sameTint = color == edgeTint;
        for (int i = 0; i < stops.Length; i++)
        {
            float r = SoftRadialStops[i];
            float falloff = r >= 1f ? 0f : MathF.Pow(1f - r * r, 2.4f);
            var tint = sameTint ? color : Argb.Lerp(color, edgeTint, r);
            stops[i] = new CanvasGradientStop { Position = r, Color = Ca(tint, peak * falloff) };
        }
        return new CanvasRadialGradientBrush(rc, stops) { Center = center, RadiusX = rSafe, RadiusY = rSafe };
    }

    private static CanvasLinearGradientBrush VerticalGrad(ICanvasResourceCreator rc, float startY, float endY, (float Pos, Color Col)[] stops)
    {
        var gs = new CanvasGradientStop[stops.Length];
        for (int i = 0; i < stops.Length; i++) gs[i] = new CanvasGradientStop { Position = stops[i].Pos, Color = stops[i].Col };
        return new CanvasLinearGradientBrush(rc, gs) { StartPoint = new Vector2(0f, startY), EndPoint = new Vector2(0f, endY) };
    }

    private static void DrawSoftDisc(
        CanvasDrawingSession ds, CanvasRadialGradientBrush brush,
        float cx, float cy, float radius,
        float scaleX = 1f, float scaleY = 1f, float rotationDeg = 0f, float alpha = 1f)
    {
        float a = Math.Clamp(alpha, 0f, 1f);
        if (a <= 0.002f) return;
        var saved = ds.Transform;
        var m = Matrix3x2.Identity;
        if (scaleX != 1f || scaleY != 1f) m *= Matrix3x2.CreateScale(scaleX, scaleY);
        if (rotationDeg != 0f) m *= Matrix3x2.CreateRotation(rotationDeg * DegToRad);
        m *= Matrix3x2.CreateTranslation(cx, cy);
        ds.Transform = m * saved;
        brush.Opacity = a;
        ds.FillCircle(0f, 0f, radius, brush);
        ds.Transform = saved;
    }

    private static float SceneHash(int index, int salt)
    {
        unchecked
        {
            uint x = (uint)(index * 374_761_393 + salt * 668_265_263);
            x = (x ^ (x >> 13)) * 1_274_126_177u;
            x ^= x >> 16;
            return (x & 0x7FFFFFFFu) / 2_147_483_647f;
        }
    }

    private static float SoftAbs(float v, float round = 0.12f) =>
        (MathF.Sqrt(v * v + round * round) - round) / (MathF.Sqrt(1f + round * round) - round);

    private static float SceneCalmMask(float yn, float center = 0.48f, float halfSpan = 0.22f, float dip = 0.42f)
    {
        float dist = MathF.Abs(yn - center) / MathF.Max(0.01f, halfSpan);
        if (dist >= 1f) return 1f;
        float s = 1f - dist;
        float smooth = s * s * (3f - 2f * s);
        return Math.Clamp(1f - Math.Clamp(dip, 0f, 1f) * smooth, 0f, 1f);
    }

    private static float PosMod(float x, float m) => ((x % m) + m) % m;

    private static Color C(Argb c) => ThemeService.ToColor(c);
    private static Color Ca(Argb c, float a) => ThemeService.ToColor(c, Math.Clamp(a, 0f, 1f));

    private struct SmoothTrace(CanvasPathBuilder pb)
    {
        private float _px, _py;
        private int _count;

        public void Start(float x, float y, bool join = false)
        {
            if (join) pb.AddLine(x, y);
            else pb.BeginFigure(x, y);
            _px = x;
            _py = y;
            _count = 1;
        }

        public void To(float x, float y)
        {
            float mx = (_px + x) * 0.5f;
            float my = (_py + y) * 0.5f;
            if (_count == 1) pb.AddLine(mx, my);
            else pb.AddQuadraticBezier(new Vector2(_px, _py), new Vector2(mx, my));
            _px = x;
            _py = y;
            _count++;
        }

        public void End() => pb.AddLine(_px, _py);
    }
}
