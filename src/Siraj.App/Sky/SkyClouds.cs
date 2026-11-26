using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Siraj.App.Services;
using Siraj.Core.Sky;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace Siraj.App.Sky;

internal readonly record struct CloudSpec(
    float XRatio,
    float YRatio,
    float WidthDp,
    float HeightDp,
    int Layer,
    int Bumps,
    int Seed);

internal sealed class PaintedCloud : IDisposable
{
    public CanvasBitmap Shape { get; }
    public CanvasBitmap LitLeft { get; }
    public CanvasBitmap LitRight { get; }
    public CanvasBitmap LitAbove { get; }
    public CanvasBitmap EdgeLeft { get; }
    public CanvasBitmap EdgeRight { get; }
    public CanvasBitmap EdgeAbove { get; }
    public CanvasBitmap Under { get; }
    public int Width { get; }
    public int Height { get; }
    public float Left { get; }
    public float Top { get; }
    public float BaseX { get; }
    public float YRatio { get; }
    public int Layer { get; }
    public float HalfWidthPx { get; }
    public float HeightPx { get; }

    public PaintedCloud(
        CanvasBitmap shape,
        CanvasBitmap litLeft,
        CanvasBitmap litRight,
        CanvasBitmap litAbove,
        CanvasBitmap edgeLeft,
        CanvasBitmap edgeRight,
        CanvasBitmap edgeAbove,
        CanvasBitmap under,
        int width,
        int height,
        float left,
        float top,
        float baseX,
        float yRatio,
        int layer,
        float halfWidthPx,
        float heightPx)
    {
        Shape = shape;
        LitLeft = litLeft;
        LitRight = litRight;
        LitAbove = litAbove;
        EdgeLeft = edgeLeft;
        EdgeRight = edgeRight;
        EdgeAbove = edgeAbove;
        Under = under;
        Width = width;
        Height = height;
        Left = left;
        Top = top;
        BaseX = baseX;
        YRatio = yRatio;
        Layer = layer;
        HalfWidthPx = halfWidthPx;
        HeightPx = heightPx;
    }

    public void Dispose()
    {
        Shape.Dispose();
        LitLeft.Dispose();
        LitRight.Dispose();
        LitAbove.Dispose();
        EdgeLeft.Dispose();
        EdgeRight.Dispose();
        EdgeAbove.Dispose();
        Under.Dispose();
    }
}

internal static class SkyClouds
{
    private static readonly Argb MoonlitCloudTop = Argb.Hex(0xFFD6E4FA);
    private static readonly Argb MoonlitCloudBase = Argb.Hex(0xFF141D32);
    private static readonly Argb MoonlitCloudLining = Argb.Hex(0xFFFFFFFF);

    public static readonly CloudSpec[] DashboardSpecs =
    [
        new(0.65f, 0.28f, 52f, 16f, 0, 4, 3),
        new(1.18f, 0.36f, 60f, 18f, 0, 4, 4),
        new(0.12f, 0.24f, 42f, 13f, 0, 3, 7),
        new(0.82f, 0.50f, 76f, 22f, 1, 5, 5),
        new(1.46f, 0.53f, 68f, 20f, 1, 5, 6),
    ];

    public const float CloudLoop = 1.7f;
    public const float FarLoopSeconds = 900f;
    public const float NearLoopSeconds = 600f;
    public const float NightCloudStrength = 0.42f;

    public static PaintedCloud Paint(ICanvasResourceCreator rc, CloudSpec spec, float unit)
    {
        float w = spec.WidthDp * unit;
        float h = spec.HeightDp * unit;
        float pad = 0.35f * h;
        float originX = w / 2f + pad;
        float originY = h * 1.15f;
        int bw = Math.Max(4, (int)MathF.Ceiling(w + 2f * pad));
        int bh = Math.Max(4, (int)MathF.Ceiling(originY + pad));
        int n = bw * bh;
        var billows = BuildBillows(w, h, spec.Bumps, spec.Seed);

        var shape = new float[n];
        for (int j = 0; j < bh; j++)
        {
            float y = j + 0.5f - originY;
            int row = j * bw;
            for (int i = 0; i < bw; i++)
            {
                float x = i + 0.5f - originX;
                float density = 0f;
                for (int bi = 0; bi < billows.Count; bi++)
                {
                    var b = billows[bi];
                    float dx = (x - b.X) / b.Rx;
                    float dy = (y - b.Y) / b.Ry;
                    float d2 = dx * dx + dy * dy;
                    if (d2 < 1f)
                    {
                        float q = 1f - d2;
                        float p = q * q * (3f - 2f * q) * b.Weight;
                        density = 1f - (1f - density) * (1f - p);
                    }
                }

                if (y > 0f)
                {
                    float fade = MathF.Exp(-(y / (0.13f * h)) * (y / (0.13f * h)));
                    density *= fade;
                }
                else if (y > -0.20f * h)
                {
                    float t = Math.Clamp(y / (-0.20f * h), 0f, 1f);
                    density *= 0.82f + 0.18f * MathF.Sqrt(t);
                }

                if (density > 0.02f)
                {
                    float wave = MathF.Sin((x * 2.8f + y * 2.0f) / h + spec.Seed * 3) * 0.05f
                               + MathF.Cos((x * 4.6f - y * 3.2f) / h + spec.Seed * 7) * 0.03f;
                    density = Math.Clamp(density + wave * density * (1f - density), 0f, 1f);
                }

                shape[row + i] = density < 0.012f ? 0f : density;
            }
        }

        Blur(shape, bw, bh, 0.045f * h);

        var lit0 = new float[n];
        var lit1 = new float[n];
        var lit2 = new float[n];
        var edge0 = new float[n];
        var edge1 = new float[n];
        var edge2 = new float[n];

        float step = MathF.Max(1f, 0.035f * h);
        const float lx0 = -0.65f, ly0 = -0.70f, lz0 = 0.30f;
        const float lx1 = 0.65f, ly1 = -0.70f, lz1 = 0.30f;
        const float lx2 = 0.0f, ly2 = -0.92f, lz2 = 0.38f;

        for (int j = 0; j < bh; j++)
        {
            int row = j * bw;
            for (int i = 0; i < bw; i++)
            {
                int at = row + i;
                float d = shape[at];
                if (d <= 0.012f) continue;

                float dLeft = Sample(shape, bw, bh, i - step, j);
                float dRight = Sample(shape, bw, bh, i + step, j);
                float dUp = Sample(shape, bw, bh, i, j - step);
                float dDown = Sample(shape, bw, bh, i, j + step);

                float gx = (dRight - dLeft) / (2f * step);
                float gy = (dDown - dUp) / (2f * step);
                const float nz = 0.40f;
                float invLen = 1f / MathF.Sqrt(gx * gx + gy * gy + nz * nz);
                float nx = -gx * invLen;
                float ny = -gy * invLen;
                float nZ = nz * invLen;

                float diff0 = MathF.Max(0f, nx * lx0 + ny * ly0 + nZ * lz0);
                float diff1 = MathF.Max(0f, nx * lx1 + ny * ly1 + nZ * lz1);
                float diff2 = MathF.Max(0f, nx * lx2 + ny * ly2 + nZ * lz2);

                lit0[at] = Math.Clamp(diff0 * 1.25f, 0f, 1f) * d;
                lit1[at] = Math.Clamp(diff1 * 1.25f, 0f, 1f) * d;
                lit2[at] = Math.Clamp(diff2 * 1.30f, 0f, 1f) * d;

                float fringe = Math.Clamp(4.0f * d * (1f - d), 0f, 1f);
                edge0[at] = Math.Clamp(fringe * diff0 * 1.35f, 0f, 1f);
                edge1[at] = Math.Clamp(fringe * diff1 * 1.35f, 0f, 1f);
                edge2[at] = Math.Clamp(fringe * diff2 * 1.35f, 0f, 1f);
            }
        }

        float lightSigma = 0.035f * h;
        Blur(lit0, bw, bh, lightSigma);
        Blur(lit1, bw, bh, lightSigma);
        Blur(lit2, bw, bh, lightSigma);
        Blur(edge0, bw, bh, lightSigma);
        Blur(edge1, bw, bh, lightSigma);
        Blur(edge2, bw, bh, lightSigma);

        var under = new float[n];
        for (int j = 0; j < bh; j++)
        {
            float y = j + 0.5f - originY;
            float underExposure = Math.Clamp((y + 0.55f * h) / (0.60f * h), 0f, 1f);
            float ramp = underExposure * underExposure;
            int row = j * bw;
            for (int i = 0; i < bw; i++)
                under[row + i] = shape[row + i] * ramp;
        }

        return new PaintedCloud(
            MaskBitmap(rc, shape, bw, bh),
            MaskBitmap(rc, lit0, bw, bh),
            MaskBitmap(rc, lit1, bw, bh),
            MaskBitmap(rc, lit2, bw, bh),
            MaskBitmap(rc, edge0, bw, bh),
            MaskBitmap(rc, edge1, bw, bh),
            MaskBitmap(rc, edge2, bw, bh),
            MaskBitmap(rc, under, bw, bh),
            bw, bh, -originX, -originY,
            spec.XRatio, spec.YRatio, spec.Layer, originX, h);
    }

    public static void DrawDeck(
        CanvasDrawingSession ds,
        IReadOnlyList<PaintedCloud> clouds,
        SkyColors sky,
        SkyTwilight twilight,
        int layer,
        float drift,
        float orbX,
        float orbY,
        float nightness,
        float deckAlpha,
        float w,
        float h)
    {
        if (deckAlpha <= 0.01f || clouds.Count == 0) return;

        float moonlit = Math.Clamp(nightness, 0f, 1f);
        var light = Argb.Lerp(sky.CloudLight, MoonlitCloudTop, moonlit);
        var dark = Argb.Lerp(sky.CloudDark, MoonlitCloudBase, moonlit);
        var lining = Argb.Lerp(sky.OrbCore, MoonlitCloudLining, moonlit);
        float lowSun = Math.Clamp(MathF.Max(twilight.SunriseGlow, MathF.Max(twilight.SunsetGlow, twilight.Shafaq * 0.8f)), 0f, 1f);
        var horizon = sky.Sky.Count > 0 ? sky.Sky[^1] : sky.OrbGlow;

        var shadowCol = ThemeService.ToColor(Argb.Lerp(light, dark, 0.72f));
        var lightCol = ThemeService.ToColor(Argb.Lerp(light, lining, 0.18f));
        var liningCol = ThemeService.ToColor(lining);
        var underCol = ThemeService.ToColor(Argb.Lerp(horizon, sky.OrbCore, 0.45f));

        // Composite the full deck offscreen so cloud shadow/light layers never double-blend with the sky.
        using (ds.CreateLayer(Math.Clamp(deckAlpha, 0f, 1f)))
        {
            for (int i = 0; i < clouds.Count; i++)
            {
                var cloud = clouds[i];
                if (cloud.Layer != layer) continue;
                float x = (((cloud.BaseX + drift) % CloudLoop) - (CloudLoop - 1f) * 0.5f) * w;
                if (x + cloud.HalfWidthPx < 0f || x - cloud.HalfWidthPx > w) continue;

                float baseY = cloud.YRatio * h;
                float reach = MathF.Max(1f, w * 0.34f);
                float side = Math.Clamp((orbX - x) / MathF.Max(1f, w * 0.42f), -1f, 1f);
                float near = Math.Clamp(1f - MathF.Sqrt((orbX - x) * (orbX - x) + (orbY - (baseY - cloud.HeightPx * 0.5f)) * (orbY - (baseY - cloud.HeightPx * 0.5f))) / reach, 0f, 1f);
                float edge = Math.Clamp(0.35f * moonlit + 0.8f * near * near * (1f - 0.5f * moonlit), 0f, 1f);
                float aside = Math.Clamp(MathF.Abs(side) * 2.5f, 0f, 1f);
                float overhead = 1f - aside;

                var pos = new Vector2(x + cloud.Left, baseY + cloud.Top);
                DrawTinted(ds, cloud.Shape, pos, shadowCol, 1f);
                if (overhead > 0.01f) DrawTinted(ds, cloud.LitAbove, pos, lightCol, overhead);
                if (aside > 0.01f) DrawTinted(ds, side < 0f ? cloud.LitLeft : cloud.LitRight, pos, lightCol, aside);
                if (lowSun > 0.01f) DrawTinted(ds, cloud.Under, pos, underCol, 0.5f * lowSun);
                if (edge > 0.01f)
                {
                    if (overhead > 0.01f) DrawTinted(ds, cloud.EdgeAbove, pos, liningCol, edge * overhead);
                    if (aside > 0.01f) DrawTinted(ds, side < 0f ? cloud.EdgeLeft : cloud.EdgeRight, pos, liningCol, edge * aside);
                }
            }
        }
    }

    private static void DrawTinted(CanvasDrawingSession ds, CanvasBitmap mask, Vector2 pos, Color color, float alpha)
    {
        if (alpha <= 0.008f) return;
        using var tint = new TintEffect
        {
            Source = mask,
            Color = Color.FromArgb((byte)Math.Clamp(alpha * 255f, 0, 255), color.R, color.G, color.B),
        };
        ds.DrawImage(tint, pos);
    }

    private static CanvasBitmap MaskBitmap(ICanvasResourceCreator rc, float[] mask, int bw, int bh)
    {
        var bytes = new byte[bw * bh * 4];
        for (int i = 0; i < mask.Length; i++)
        {
            byte a = (byte)Math.Clamp((int)(mask[i] * 255f + 0.5f), 0, 255);
            if (a == 0) continue;
            int idx = i * 4;
            bytes[idx] = a;
            bytes[idx + 1] = a;
            bytes[idx + 2] = a;
            bytes[idx + 3] = a;
        }
        return CanvasBitmap.CreateFromBytes(rc, bytes, bw, bh, DirectXPixelFormat.B8G8R8A8UIntNormalized, 96f, CanvasAlphaMode.Premultiplied);
    }

    private readonly record struct Billow(float X, float Y, float Rx, float Ry, float Weight);

    private static List<Billow> BuildBillows(float w, float h, int bumps, int seed)
    {
        var list = new List<Billow>();
        float summit = (Seeded(seed * 7 + 1) - 0.5f) * 0.18f * w;

        int baseCount = Math.Max(bumps, 4);
        for (int i = 0; i < baseCount; i++)
        {
            float f = (i + 0.5f) / baseCount;
            float x = -0.38f * w + 0.76f * w * f + (Seeded(seed * 17 + i) - 0.5f) * 0.04f * w;
            float dist = MathF.Abs(x - summit) / (0.45f * w);
            float swell = MathF.Max(0f, 1f - dist * dist);
            float ry = h * (0.28f + 0.12f * swell) * (0.95f + 0.10f * Seeded(seed * 31 + i));
            float rx = ry * (1.50f + 0.20f * Seeded(seed * 19 + i));
            float cy = -ry * 0.65f - 0.06f * h * swell;
            list.Add(new Billow(x, cy, rx, ry, 0.95f + 0.05f * swell));
        }

        float mainRy = h * (0.36f + 0.06f * Seeded(seed * 29));
        float mainRx = mainRy * (1.45f + 0.10f * Seeded(seed * 37));
        list.Add(new Billow(summit, -h * 0.56f, mainRx, mainRy, 1.0f));

        float leftOff = -0.18f * w + (Seeded(seed * 43) - 0.5f) * 0.04f * w;
        float leftRy = mainRy * (0.80f + 0.10f * Seeded(seed * 47));
        list.Add(new Billow(summit + leftOff, -h * 0.46f, leftRy * 1.40f, leftRy, 0.90f));

        float rightOff = 0.20f * w + (Seeded(seed * 53) - 0.5f) * 0.04f * w;
        float rightRy = mainRy * (0.75f + 0.12f * Seeded(seed * 59));
        list.Add(new Billow(summit + rightOff, -h * 0.44f, rightRy * 1.40f, rightRy, 0.88f));

        if (bumps >= 5)
        {
            float subOff = (Seeded(seed * 61) > 0.5f ? 0.09f : -0.09f) * w;
            float subRy = mainRy * 0.88f;
            list.Add(new Billow(summit + subOff, -h * 0.52f, subRy * 1.35f, subRy, 0.92f));
        }

        return list;
    }

    private static float Sample(float[] data, int bw, int bh, float x, float y)
    {
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        float fx = x - x0;
        float fy = y - y0;
        float At(int i, int j) => (uint)i < (uint)bw && (uint)j < (uint)bh ? data[j * bw + i] : 0f;
        float top = At(x0, y0) * (1f - fx) + At(x0 + 1, y0) * fx;
        float bot = At(x0, y0 + 1) * (1f - fx) + At(x0 + 1, y0 + 1) * fx;
        return top * (1f - fy) + bot * fy;
    }

    private static void Blur(float[] data, int w, int h, float sigma)
    {
        if (sigma < 0.3f) return;
        int radius = (int)MathF.Ceiling(sigma * 3f);
        var kernel = new float[radius * 2 + 1];
        float total = 0f;
        for (int i = 0; i < kernel.Length; i++)
        {
            float d = i - radius;
            kernel[i] = MathF.Exp(-(d * d) / (2f * sigma * sigma));
            total += kernel[i];
        }
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= total;

        var tmp = new float[data.Length];
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                float acc = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    int xx = x + k;
                    if ((uint)xx < (uint)w) acc += data[row + xx] * kernel[k + radius];
                }
                tmp[row + x] = acc;
            }
        }
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                float acc = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    int yy = y + k;
                    if ((uint)yy < (uint)h) acc += tmp[yy * w + x] * kernel[k + radius];
                }
                data[y * w + x] = acc;
            }
        }
    }

    private static float Seeded(int i)
    {
        double x = Math.Sin(i * 12.9898) * 43758.5453;
        return (float)(x - Math.Floor(x));
    }
}
