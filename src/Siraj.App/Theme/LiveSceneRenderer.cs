using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Siraj.App.Services;
using Siraj.Core.Sky;
using Siraj.Core.Theme;
using Windows.UI;

namespace Siraj.App.Theme;

internal static class LiveSceneRenderer
{
    public static void DrawBackdrop(
        CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h,
        string sceneId, LiveSceneColors colors, LiveIntensitySpec intensity,
        GlassSpec glass, bool liveEnabled, bool glassEnabled, bool timeSync, int minutesOfDay,
        float t, bool drawVeil = true)
    {
        if (w < 1 || h < 1) return;
        float k = Math.Clamp(intensity.DetailScale, 0.45f, 1.35f);

        if (liveEnabled)
        {
            DrawSkyGradient(ds, rc, w, h, colors);
            if (timeSync && sceneId is not ("NightStars" or "Aurora" or "Space"))
            {
                float starVis = LiveScenes.StarVisibility(minutesOfDay) * (colors.IsDark ? 0.55f : 0.10f);
                if (starVis > 0.02f) DrawSyncedStars(ds, w, h, colors.Highlight, starVis * k, t);
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
                case "Particles": DrawParticles(ds, w, h, colors, k, t); break;
                case "LavaLamp": DrawLavaLamp(ds, rc, w, h, colors, k, t); break;
                case "Space": DrawSpace(ds, rc, w, h, colors, k, t); break;
                case "Silk": DrawSilk(ds, rc, w, h, colors, k, t); break;
                case "Contours": DrawContours(ds, w, h, colors, k, t); break;
                case "Mosaic": DrawMosaic(ds, w, h, colors, k, t); break;
                case "Waves": DrawWaves(ds, rc, w, h, colors, k, t); break;
                case "Harvest": DrawHarvest(ds, rc, w, h, colors, k, t); break;
                default: DrawDesertDunes(ds, rc, w, h, colors, k, t); break;
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

    private static void DrawSkyGradient(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c)
    {
        using var sky = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = C(c.SkyTop) },
            new CanvasGradientStop { Position = 0.34f, Color = C(c.SkyUpperMid) },
            new CanvasGradientStop { Position = 0.68f, Color = C(c.SkyLowerMid) },
            new CanvasGradientStop { Position = 1.00f, Color = C(c.SkyHorizon) },
        ]) { StartPoint = new Vector2(0, 0), EndPoint = new Vector2(0, h) };
        ds.FillRectangle(0, 0, w, h, sky);
    }

    private static void DrawVeil(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, LiveIntensitySpec intensity, bool glass)
    {
        float adj = intensity.VeilAdjustment;
        float top = glass
            ? Math.Clamp((c.IsDark ? 0.18f : 0.12f) + adj, 0.04f, 0.40f)
            : Math.Clamp((c.IsDark ? 0.28f : 0.22f) + adj, 0.08f, 0.50f);
        float mid = glass
            ? Math.Clamp((c.IsDark ? 0.06f : 0.04f) + adj * 0.6f, 0f, 0.24f)
            : Math.Clamp((c.IsDark ? 0.14f : 0.12f) + adj, 0.03f, 0.34f);
        float bot = glass
            ? Math.Clamp((c.IsDark ? 0.22f : 0.14f) + adj, 0.06f, 0.44f)
            : Math.Clamp((c.IsDark ? 0.30f : 0.22f) + adj, 0.10f, 0.52f);

        using var brush = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = Ca(c.Veil, top) },
            new CanvasGradientStop { Position = 0.46f, Color = Ca(c.Veil, mid) },
            new CanvasGradientStop { Position = 1.00f, Color = Ca(c.Veil, bot) },
        ]) { StartPoint = new Vector2(0, 0), EndPoint = new Vector2(0, h) };
        ds.FillRectangle(0, 0, w, h, brush);
    }

    private static void DrawAmbientGlassPools(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, GlassSpec g, float scale, float t)
    {
        float dx = MathF.Sin(t * 0.14f) * w * 0.04f;
        float dy = MathF.Cos(t * 0.11f) * h * 0.03f;
        Glow(ds, rc, new Vector2(w * 0.18f + dx, h * 0.16f + dy), MathF.Max(w, h) * 0.45f, Ca(g.AmbientPrimary, (g.AmbientPrimary.A / 255f) * scale));
        Glow(ds, rc, new Vector2(w * 0.84f - dx, h * 0.78f - dy), MathF.Max(w, h) * 0.42f, Ca(g.AmbientSecondary, (g.AmbientSecondary.A / 255f) * scale));
        Glow(ds, rc, new Vector2(w * 0.55f + dy, h * 0.42f - dx * 0.5f), MathF.Max(w, h) * 0.34f, Ca(g.AmbientPrimary, (g.AmbientPrimary.A / 255f) * 0.55f * scale));
    }

    // ---- 1. Desert Dunes (evening dunes, crest highlights, glowing rising lanterns, stars) ----
    private static void DrawDesertDunes(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        float minDim = MathF.Min(w, h);
        var warm = Argb.Lerp(Argb.Hex(0xFFFFB24D), c.Accent, 0.28f);
        var core = Argb.Hex(0xFFFFEECB);

        // Soft twilight stars in upper sky
        float starA = (c.IsDark ? 0.50f : 0.15f) * k;
        for (int i = 0; i < 18; i++)
        {
            float sx = (0.05f + 0.90f * Hash(i, 11)) * w;
            float sy = (0.04f + 0.36f * Hash(i, 23)) * h;
            float r = MathF.Max(0.8f, minDim * (0.0014f + 0.0016f * Hash(i, 37)));
            float tw = 0.45f + 0.55f * (0.5f + 0.5f * MathF.Sin(t * 0.75f + Hash(i, 53) * 6.28f));
            ds.FillCircle(sx, sy, r, Ca(c.Highlight, starA * tw));
        }

        // Horizon atmospheric glow
        Glow(ds, rc, new Vector2(w * 0.52f, h * 0.72f), w * 0.60f, Ca(warm, (c.IsDark ? 0.16f : 0.20f) * k));

        // Floating glowing sky lanterns rising peacefully on left & right flanks
        (float x, float phase, float speed, float sc)[] lanterns =
        [
            (0.14f, 0.18f, 0.0068f, 0.85f), (0.84f, 0.34f, 0.0074f, 0.80f),
            (0.24f, 0.66f, 0.0060f, 0.68f), (0.76f, 0.10f, 0.0085f, 1.05f),
            (0.56f, 0.86f, 0.0048f, 0.45f), (0.90f, 0.72f, 0.0052f, 0.50f),
        ];
        foreach (var (xb, ph, sp, sc) in lanterns)
        {
            float prog = (ph + t * sp) % 1f;
            float lx = (xb + MathF.Sin(t * 0.30f + ph * 12f) * 0.018f) * w;
            float ly = h * (0.86f - prog * 0.92f);
            float lw = MathF.Max(5f, minDim * 0.020f * sc);
            float lh = lw * 1.42f;
            float flicker = 0.85f + 0.15f * MathF.Sin(t * 3.1f + ph * 20f);
            float fade = Math.Clamp(MathF.Min(prog * 6f, (1f - prog) * 5f), 0f, 1f);
            Glow(ds, rc, new Vector2(lx, ly), lw * 3.6f, Ca(warm, (c.IsDark ? 0.24f : 0.18f) * k * flicker * fade));
            ds.FillRoundedRectangle(lx - lw * 0.48f, ly - lh * 0.5f, lw * 0.96f, lh, lw * 0.38f, lw * 0.38f, Ca(warm, 0.78f * fade * k));
            ds.FillCircle(lx, ly + lh * 0.18f, lw * 0.28f, Ca(core, 0.92f * flicker * fade));
        }

        // Far and near layered smooth desert dunes
        DrawHill(ds, rc, w, h, 0.76f, 0.038f, 3.1f, 0.45f,
            Ca(Argb.Lerp(c.Shade, warm, 0.18f), (c.IsDark ? 0.76f : 0.38f) * k),
            Ca(Argb.Lerp(c.Ink, c.Shade, 0.35f), (c.IsDark ? 0.90f : 0.54f) * k));
        DrawHill(ds, rc, w, h, 0.85f, 0.032f, 2.5f, 2.35f,
            Ca(Argb.Lerp(c.Ink, c.Shade, 0.40f), (c.IsDark ? 0.92f : 0.55f) * k),
            Ca(c.Ink, (c.IsDark ? 0.96f : 0.70f) * k));
    }

    // ---- 2. Sea Waves ----
    private static void DrawSeaWaves(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        Glow(ds, rc, new Vector2(w * 0.72f, h * 0.24f), MathF.Min(w, h) * 0.36f, Ca(c.Accent, (c.IsDark ? 0.20f : 0.16f) * k));
        for (int layer = 0; layer < 4; layer++)
        {
            float yBase = 0.62f + layer * 0.09f;
            float amp = 0.022f + layer * 0.004f;
            float phase = t * (0.45f + layer * 0.18f) + layer * 1.4f;
            var colTop = Argb.Lerp(c.Accent, c.SkyHorizon, 0.35f + layer * 0.12f);
            var colBot = Argb.Lerp(c.Ink, c.Secondary, 0.30f);
            DrawHill(ds, rc, w, h, yBase, amp, 3.4f - layer * 0.35f, phase,
                Ca(colTop, (0.24f + layer * 0.12f) * k),
                Ca(colBot, (0.45f + layer * 0.12f) * k));
        }
    }

    // ---- 3. Aurora ----
    private static void DrawAurora(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        DrawSyncedStars(ds, w, h, c.Highlight, (c.IsDark ? 0.55f : 0.15f) * k, t);
        for (int b = 0; b < 3; b++)
        {
            float yMid = h * (0.24f + b * 0.13f);
            float bandH = h * (0.20f + b * 0.04f);
            var col = b == 1 ? c.Secondary : c.Accent;
            using var pb = new CanvasPathBuilder(rc);
            const int steps = 40;
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps;
                float y = yMid + MathF.Sin(u * 5.2f + t * (0.28f + b * 0.08f) + b * 2.1f) * h * 0.07f
                               + MathF.Cos(u * 9.0f - t * 0.18f + b) * h * 0.025f;
                if (i == 0) pb.BeginFigure(u * w, y - bandH * 0.5f);
                else pb.AddLine(u * w, y - bandH * 0.5f);
            }
            for (int i = steps; i >= 0; i--)
            {
                float u = i / (float)steps;
                float y = yMid + MathF.Sin(u * 5.2f + t * (0.28f + b * 0.08f) + b * 2.1f) * h * 0.07f
                               + MathF.Cos(u * 9.0f - t * 0.18f + b) * h * 0.025f;
                pb.AddLine(u * w, y + bandH * 0.5f);
            }
            pb.EndFigure(CanvasFigureLoop.Closed);
            using var geo = CanvasGeometry.CreatePath(pb);
            using var brush = new CanvasLinearGradientBrush(rc, [
                new CanvasGradientStop { Position = 0f, Color = Ca(col, 0f) },
                new CanvasGradientStop { Position = 0.45f, Color = Ca(col, (c.IsDark ? 0.24f : 0.16f) * k) },
                new CanvasGradientStop { Position = 1f, Color = Ca(col, 0f) },
            ]) { StartPoint = new Vector2(0, yMid - bandH), EndPoint = new Vector2(0, yMid + bandH) };
            ds.FillGeometry(geo, brush);
        }
    }

    // ---- 4. Night Stars ----
    private static void DrawNightStars(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        Glow(ds, rc, new Vector2(w * 0.76f, h * 0.22f), MathF.Min(w, h) * 0.42f, Ca(c.Accent, (c.IsDark ? 0.18f : 0.14f) * k));
        Glow(ds, rc, new Vector2(w * 0.25f, h * 0.65f), MathF.Min(w, h) * 0.36f, Ca(c.Secondary, (c.IsDark ? 0.14f : 0.10f) * k));
        for (int i = 0; i < 64; i++)
        {
            float sx = Hash(i, 13) * w;
            float sy = Hash(i, 29) * h * 0.88f;
            float r = i % 9 == 0 ? 2.1f : i % 3 == 0 ? 1.4f : 0.9f;
            float tw = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(t * (0.7f + (i % 5) * 0.2f) + i * 1.3f));
            var col = i % 7 == 0 ? c.Accent : c.Highlight;
            ds.FillCircle(sx, sy, r, Ca(col, (c.IsDark ? 0.78f : 0.34f) * tw * k));
        }
        // Periodic shooting star
        float cycle = t % 12f;
        if (cycle < 1.1f)
        {
            float p = cycle / 1.1f;
            float x = w * (0.78f - p * 0.32f);
            float y = h * (0.12f + p * 0.18f);
            float g = MathF.Sin(p * MathF.PI);
            ds.DrawLine(x, y, x + 58f, y - 32f, Ca(c.Highlight, 0.55f * g * k), 1.4f);
            ds.FillCircle(x, y, 2f, Ca(c.Highlight, 0.85f * g * k));
        }
    }

    // ---- 5. Rose Petals ----
    private static void DrawRosePetals(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        Glow(ds, rc, new Vector2(w * 0.25f, h * 0.25f), MathF.Min(w, h) * 0.45f, Ca(c.Accent, 0.14f * k));
        for (int i = 0; i < 20; i++)
        {
            float speed = 0.022f + 0.018f * Hash(i, 7);
            float py = ((Hash(i, 19) + t * speed) % 1.15f - 0.08f) * h;
            float px = ((Hash(i, 31) + MathF.Sin(t * 0.6f + i) * 0.045f + t * 0.008f) % 1.1f - 0.05f) * w;
            float sz = 5f + 7f * Hash(i, 43);
            float ang = t * (0.5f + Hash(i, 59)) + i;
            var saved = ds.Transform;
            ds.Transform = Matrix3x2.CreateRotation(ang) * Matrix3x2.CreateTranslation(px, py) * saved;
            var col = i % 2 == 0 ? c.Accent : Argb.Lerp(c.Accent, c.Highlight, 0.45f);
            ds.FillEllipse(0, 0, sz, sz * 0.55f, Ca(col, (c.IsDark ? 0.34f : 0.30f) * k));
            ds.Transform = saved;
        }
    }

    // ---- 6. Snow ----
    private static void DrawSnow(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        Glow(ds, rc, new Vector2(w * 0.5f, h * 0.15f), w * 0.5f, Ca(c.Highlight, (c.IsDark ? 0.10f : 0.22f) * k));
        for (int i = 0; i < 42; i++)
        {
            float sp = 0.018f + 0.026f * Hash(i, 3);
            float py = ((Hash(i, 17) + t * sp) % 1.08f - 0.04f) * h;
            float px = ((Hash(i, 29) + MathF.Sin(t * 0.7f + i * 0.9f) * 0.025f) % 1f) * w;
            float r = 1.6f + 3.4f * Hash(i, 41);
            float alpha = (0.20f + 0.35f * Hash(i, 53)) * k;
            ds.FillCircle(px, py, r, Ca(c.Highlight, alpha));
        }
    }

    // ---- 7. Butterflies ----
    private static void DrawButterflies(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        Glow(ds, rc, new Vector2(w * 0.30f, h * 0.30f), MathF.Min(w, h) * 0.42f, Ca(c.Accent, 0.16f * k));
        Glow(ds, rc, new Vector2(w * 0.75f, h * 0.65f), MathF.Min(w, h) * 0.38f, Ca(c.Secondary, 0.14f * k));
        for (int i = 0; i < 9; i++)
        {
            float bx = ((Hash(i, 11) + t * (0.012f + 0.008f * Hash(i, 13))) % 1.15f - 0.07f) * w;
            float by = (0.15f + 0.68f * Hash(i, 23) + MathF.Sin(t * 0.9f + i * 2f) * 0.045f) * h;
            float wing = 5f + 5f * Hash(i, 37);
            float flap = 0.35f + 0.65f * MathF.Abs(MathF.Sin(t * (3.6f + i * 0.3f) + i));
            var col = i % 2 == 0 ? c.Accent : c.Secondary;
            Glow(ds, rc, new Vector2(bx, by), wing * 2.5f, Ca(col, 0.16f * k));
            ds.FillEllipse(bx - wing * 0.45f * flap, by - wing * 0.2f, wing * 0.65f * flap, wing * 0.42f, Ca(col, 0.46f * k));
            ds.FillEllipse(bx + wing * 0.45f * flap, by - wing * 0.2f, wing * 0.65f * flap, wing * 0.42f, Ca(col, 0.46f * k));
        }
    }

    // ---- 8. Rings ----
    private static void DrawRings(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        var center = new Vector2(w * 0.52f, h * 0.46f);
        float maxR = MathF.Max(w, h) * 0.62f;
        for (int i = 0; i < 7; i++)
        {
            float p = (i / 7f + t * 0.045f) % 1f;
            float r = p * maxR;
            float a = MathF.Sin(p * MathF.PI) * (c.IsDark ? 0.22f : 0.18f) * k;
            ds.DrawCircle(center, r, Ca(i % 2 == 0 ? c.Accent : c.Secondary, a), 1.6f);
        }
    }

    // ---- 9. Circles (soft bokeh) ----
    private static void DrawCircles(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        for (int i = 0; i < 10; i++)
        {
            float cx = (0.10f + 0.80f * Hash(i, 7) + MathF.Sin(t * 0.22f + i * 1.4f) * 0.06f) * w;
            float cy = (0.10f + 0.80f * Hash(i, 19) + MathF.Cos(t * 0.19f + i * 1.7f) * 0.06f) * h;
            float r = MathF.Min(w, h) * (0.08f + 0.14f * Hash(i, 31));
            var col = i % 3 == 0 ? c.Accent : i % 3 == 1 ? c.Secondary : c.Highlight;
            Glow(ds, rc, new Vector2(cx, cy), r, Ca(col, (c.IsDark ? 0.20f : 0.16f) * k));
        }
    }

    // ---- 10. Particles ----
    private static void DrawParticles(CanvasDrawingSession ds, float w, float h, LiveSceneColors c, float k, float t)
    {
        Span<Vector2> pts = stackalloc Vector2[28];
        for (int i = 0; i < pts.Length; i++)
        {
            float x = ((Hash(i, 5) + MathF.Sin(t * 0.25f + i) * 0.03f) % 1f) * w;
            float y = ((Hash(i, 17) - t * (0.010f + 0.008f * Hash(i, 29)) + 100f) % 1f) * h;
            pts[i] = new Vector2(x, y);
            ds.FillCircle(x, y, 1.5f + 2.2f * Hash(i, 41), Ca(i % 2 == 0 ? c.Accent : c.Highlight, (c.IsDark ? 0.45f : 0.28f) * k));
        }
        float thresh = MathF.Min(w, h) * 0.18f;
        for (int i = 0; i < pts.Length; i++)
        {
            for (int j = i + 1; j < pts.Length; j++)
            {
                float d = Vector2.Distance(pts[i], pts[j]);
                if (d < thresh) ds.DrawLine(pts[i], pts[j], Ca(c.Accent, (1f - d / thresh) * 0.14f * k), 0.9f);
            }
        }
    }

    // ---- 11. Lava Lamp ----
    private static void DrawLavaLamp(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        for (int i = 0; i < 6; i++)
        {
            float x = (0.16f + 0.68f * Hash(i, 11) + MathF.Sin(t * (0.18f + i * 0.03f) + i * 2f) * 0.10f) * w;
            float y = (0.18f + 0.64f * Hash(i, 23) + MathF.Cos(t * (0.15f + i * 0.025f) + i) * 0.14f) * h;
            float r = MathF.Min(w, h) * (0.18f + 0.14f * Hash(i, 37));
            Glow(ds, rc, new Vector2(x, y), r, Ca(i % 2 == 0 ? c.Accent : c.Secondary, (c.IsDark ? 0.28f : 0.22f) * k));
        }
    }

    // ---- 12. Space ----
    private static void DrawSpace(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        Glow(ds, rc, new Vector2(w * 0.28f, h * 0.34f), MathF.Max(w, h) * 0.38f, Ca(c.Accent, (c.IsDark ? 0.22f : 0.16f) * k));
        Glow(ds, rc, new Vector2(w * 0.74f, h * 0.62f), MathF.Max(w, h) * 0.36f, Ca(c.Secondary, (c.IsDark ? 0.20f : 0.14f) * k));
        DrawNightStars(ds, rc, w, h, c, k * 0.85f, t);
    }

    // ---- 13. Silk ----
    private static void DrawSilk(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        for (int s = 0; s < 5; s++)
        {
            float y0 = h * (0.20f + s * 0.16f);
            using var pb = new CanvasPathBuilder(rc);
            pb.BeginFigure(0, y0);
            for (int i = 1; i <= 32; i++)
            {
                float u = i / 32f;
                float y = y0 + MathF.Sin(u * 4.5f + t * 0.32f + s * 1.1f) * h * 0.07f;
                pb.AddLine(u * w, y);
            }
            pb.EndFigure(CanvasFigureLoop.Open);
            using var geo = CanvasGeometry.CreatePath(pb);
            ds.DrawGeometry(geo, Ca(s % 2 == 0 ? c.Accent : c.Secondary, (c.IsDark ? 0.18f : 0.14f) * k), h * 0.055f);
        }
    }

    // ---- 14. Contours ----
    private static void DrawContours(CanvasDrawingSession ds, float w, float h, LiveSceneColors c, float k, float t)
    {
        for (int line = 0; line < 10; line++)
        {
            float y0 = h * (0.08f + line * 0.095f);
            float prevX = 0, prevY = y0;
            var col = Ca(line % 2 == 0 ? c.Accent : c.Secondary, (c.IsDark ? 0.18f : 0.15f) * k);
            for (int i = 1; i <= 36; i++)
            {
                float u = i / 36f;
                float x = u * w;
                float y = y0 + MathF.Sin(u * 6f + t * 0.22f + line * 0.55f) * h * 0.032f
                             + MathF.Cos(u * 3f - t * 0.15f + line) * h * 0.018f;
                ds.DrawLine(prevX, prevY, x, y, col, 1.25f);
                prevX = x; prevY = y;
            }
        }
    }

    // ---- 15. Mosaic (Islamic geometric lattice) ----
    private static void DrawMosaic(CanvasDrawingSession ds, float w, float h, LiveSceneColors c, float k, float t)
    {
        float step = Math.Clamp(MathF.Min(w, h) * 0.11f, 44f, 84f);
        int cols = (int)(w / step) + 2, rows = (int)(h / step) + 2;
        for (int r = 0; r < rows; r++)
        {
            for (int col = 0; col < cols; col++)
            {
                float cx = col * step + (r % 2 == 0 ? 0 : step * 0.5f);
                float cy = r * step;
                float pulse = 0.55f + 0.45f * MathF.Sin(t * 0.55f + (col + r) * 0.7f);
                float rad = step * 0.34f;
                var stroke = Ca(c.Accent, (c.IsDark ? 0.13f : 0.10f) * pulse * k);
                // Two overlapped rotated squares = Rub-el-Hizb 8-point star
                var saved = ds.Transform;
                ds.Transform = Matrix3x2.CreateTranslation(cx, cy) * saved;
                ds.DrawRectangle(-rad * 0.65f, -rad * 0.65f, rad * 1.3f, rad * 1.3f, stroke, 1f);
                ds.Transform = Matrix3x2.CreateRotation(MathF.PI / 4f) * Matrix3x2.CreateTranslation(cx, cy) * saved;
                ds.DrawRectangle(-rad * 0.65f, -rad * 0.65f, rad * 1.3f, rad * 1.3f, stroke, 1f);
                ds.Transform = saved;
            }
        }
    }

    // ---- 16. Waves ----
    private static void DrawWaves(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        for (int i = 0; i < 4; i++)
        {
            DrawHill(ds, rc, w, h, 0.50f + i * 0.12f, 0.045f, 3.8f - i * 0.5f, t * (0.35f + i * 0.10f) + i * 1.7f,
                Ca(i % 2 == 0 ? c.Accent : c.Secondary, (0.14f + i * 0.04f) * k),
                Ca(c.SkyHorizon, 0.04f * k));
        }
    }

    // ---- 17. Harvest ----
    private static void DrawHarvest(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h, LiveSceneColors c, float k, float t)
    {
        var gold = Argb.Lerp(Argb.Hex(0xFFEAB308), c.Accent, 0.35f);
        Glow(ds, rc, new Vector2(w * 0.68f, h * 0.30f), MathF.Min(w, h) * 0.45f, Ca(gold, (c.IsDark ? 0.18f : 0.22f) * k));
        DrawHill(ds, rc, w, h, 0.80f, 0.030f, 2.8f, 0.9f, Ca(gold, 0.22f * k), Ca(c.Shade, 0.48f * k));
        // Gentle golden fireflies / harvest motes
        for (int i = 0; i < 22; i++)
        {
            float fx = (0.06f + 0.88f * Hash(i, 13) + MathF.Sin(t * 0.4f + i) * 0.03f) * w;
            float fy = (0.35f + 0.55f * Hash(i, 29) + MathF.Cos(t * 0.5f + i * 1.3f) * 0.03f) * h;
            float gl = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(t * 1.4f + i * 2.1f));
            ds.FillCircle(fx, fy, 2.2f, Ca(gold, 0.55f * gl * k));
        }
    }

    // ---- Shared drawing primitives ----

    private static void DrawSyncedStars(CanvasDrawingSession ds, float w, float h, Argb tint, float alpha, float t)
    {
        for (int i = 0; i < 24; i++)
        {
            float sx = Hash(i, 11) * w;
            float sy = Hash(i, 23) * h * 0.55f;
            float tw = 0.4f + 0.6f * (0.5f + 0.5f * MathF.Sin(t * 0.85f + i * 1.6f));
            ds.FillCircle(sx, sy, i % 5 == 0 ? 1.8f : 1.1f, Ca(tint, Math.Clamp(alpha * tw, 0f, 0.75f)));
        }
    }

    private static void DrawHill(CanvasDrawingSession ds, ICanvasResourceCreator rc, float w, float h,
        float baseYNorm, float ampNorm, float freq, float phase, Color topColor, Color botColor)
    {
        using var pb = new CanvasPathBuilder(rc);
        const int steps = 48;
        pb.BeginFigure(0, h);
        for (int i = 0; i <= steps; i++)
        {
            float u = i / (float)steps;
            float y = h * baseYNorm + h * ampNorm * MathF.Sin(u * freq + phase) + h * (ampNorm * 0.42f) * MathF.Cos(u * freq * 2.1f + phase * 0.7f);
            pb.AddLine(u * w, y);
        }
        pb.AddLine(w, h);
        pb.EndFigure(CanvasFigureLoop.Closed);
        using var geo = CanvasGeometry.CreatePath(pb);
        using var brush = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0f, Color = topColor },
            new CanvasGradientStop { Position = 1f, Color = botColor },
        ]) { StartPoint = new Vector2(0, h * (baseYNorm - ampNorm)), EndPoint = new Vector2(0, h) };
        ds.FillGeometry(geo, brush);
    }

    private static void Glow(CanvasDrawingSession ds, ICanvasResourceCreator rc, Vector2 center, float r, Color color)
    {
        if (color.A < 2 || r < 1) return;
        using var brush = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0f, Color = color },
            new CanvasGradientStop { Position = 0.50f, Color = Color.FromArgb((byte)(color.A * 0.35f), color.R, color.G, color.B) },
            new CanvasGradientStop { Position = 1f, Color = Color.FromArgb(0, color.R, color.G, color.B) },
        ]) { Center = center, RadiusX = r, RadiusY = r };
        ds.FillCircle(center, r, brush);
    }

    private static float Hash(int i, int salt)
    {
        double x = Math.Sin((i + 1) * 12.9898 + salt * 78.233) * 43758.5453;
        return (float)(x - Math.Floor(x));
    }

    private static Color C(Argb c) => ThemeService.ToColor(c);
    private static Color Ca(Argb c, float a) => ThemeService.ToColor(c, Math.Clamp(a, 0f, 1f));
}
