using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Siraj.App.Services;
using Siraj.Core.Hijri;
using Siraj.Core.Sky;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace Siraj.App.Sky;

internal sealed record SkyFrame(SkyMoment Moment, SolarArc Arc, MoonPhase Moon, double Seconds, bool Motion);

internal sealed class SkyGeometry : IDisposable
{
    public readonly record struct StarSpec(float XRatio, float YRatio, float RadiusDp, float Phase);
    public readonly record struct StarInstance(
        float RadiusPx, float HaloRadiusPx, float Phase, Vector2 Center,
        bool HasGlint, float Emerge, bool Twinkles);
    public readonly record struct WaterGlint(float Depth, float Y, float Lateral, float Phase, float Speed, float Reach);

    private static readonly StarSpec[] StarSpecs =
    [
        new(0.06f, 0.11f, 0.85f, 0.02f), new(0.12f, 0.17f, 0.70f, 0.47f),
        new(0.17f, 0.07f, 1.05f, 0.71f), new(0.24f, 0.13f, 0.75f, 0.19f),
        new(0.31f, 0.08f, 0.90f, 0.58f), new(0.41f, 0.06f, 1.15f, 0.86f),
        new(0.47f, 0.15f, 0.65f, 0.33f), new(0.55f, 0.07f, 0.80f, 0.09f),
        new(0.61f, 0.19f, 0.70f, 0.64f), new(0.67f, 0.09f, 1.00f, 0.28f),
        new(0.73f, 0.14f, 0.70f, 0.91f), new(0.81f, 0.08f, 1.10f, 0.42f),
        new(0.88f, 0.13f, 0.80f, 0.77f), new(0.94f, 0.09f, 0.90f, 0.15f),
        new(0.08f, 0.25f, 0.70f, 0.53f), new(0.15f, 0.30f, 0.60f, 0.81f),
        new(0.21f, 0.22f, 0.55f, 0.37f), new(0.27f, 0.28f, 0.65f, 0.68f),
        new(0.35f, 0.21f, 0.60f, 0.05f), new(0.44f, 0.27f, 0.55f, 0.44f),
        new(0.52f, 0.23f, 0.60f, 0.95f), new(0.58f, 0.31f, 0.55f, 0.22f),
        new(0.66f, 0.25f, 0.65f, 0.61f), new(0.70f, 0.21f, 0.75f, 0.13f),
        new(0.78f, 0.29f, 0.60f, 0.74f), new(0.86f, 0.23f, 0.70f, 0.31f),
        new(0.93f, 0.27f, 0.60f, 0.88f), new(0.04f, 0.36f, 0.55f, 0.50f),
        new(0.19f, 0.40f, 0.50f, 0.17f), new(0.33f, 0.37f, 0.55f, 0.79f),
        new(0.49f, 0.41f, 0.50f, 0.26f), new(0.63f, 0.38f, 0.55f, 0.66f),
        new(0.76f, 0.42f, 0.50f, 0.11f), new(0.90f, 0.36f, 0.60f, 0.56f),
        new(0.11f, 0.47f, 0.45f, 0.84f), new(0.29f, 0.50f, 0.50f, 0.39f),
        new(0.57f, 0.48f, 0.45f, 0.72f), new(0.83f, 0.46f, 0.50f, 0.24f),
    ];

    private static readonly (float Dx, float Dy)[] FrondDirs =
    [
        (-1.00f, -0.30f), (-0.92f, 0.26f),
        (-0.50f, -0.78f), (0.50f, -0.78f),
        (0.92f, 0.26f), (1.00f, -0.30f),
        (0.06f, -1.02f),
    ];

    private const float ReferenceCardHeight = 370f;
    private const float ReflectionSquash = 0.88f;
    private const int WaterGlintCount = 44;

    public float Width { get; private set; }
    public float Height { get; private set; }
    public CanvasDevice? Device { get; private set; }
    public float HorizonY { get; private set; }
    public float BaseY { get; private set; }
    public float PoolTop { get; private set; }
    public float Unit { get; private set; }
    public float ComplexCenterX { get; private set; }
    public float LeftMinaretX { get; private set; }
    public float RightMinaretX { get; private set; }
    public float LeftDomeX { get; private set; }
    public float RightDomeX { get; private set; }

    public Vector2 P0 { get; private set; }
    public Vector2 P1 { get; private set; }
    public Vector2 P2 { get; private set; }
    public Vector2 P3 { get; private set; }

    public CanvasGeometry? ArcPath { get; private set; }
    public CanvasGeometry? FarRidgePath { get; private set; }
    public CanvasGeometry? MidDunePath { get; private set; }
    public CanvasGeometry? MosquePath { get; private set; }
    public CanvasGeometry? PalmsPath { get; private set; }
    public CanvasGeometry? GatePath { get; private set; }
    public CanvasGeometry? FinialPath { get; private set; }
    public CanvasGeometry? MinaretCrescentsPath { get; private set; }
    public CanvasBitmap? ReflectionMask { get; private set; }
    public int ReflectionWidth { get; private set; }
    public int ReflectionHeight { get; private set; }

    public StarInstance[] Stars { get; private set; } = [];
    public Vector2[] StarDustFaint { get; private set; } = [];
    public Vector2[] StarDustBright { get; private set; } = [];
    public Vector2[] MilkyWayFaint { get; private set; } = [];
    public Vector2[] MilkyWayBright { get; private set; } = [];
    public float[] ArcadeWindowXs { get; private set; } = [];
    public float[] ReflectedLightXs { get; private set; } = [];
    public Vector2[] Lamps { get; private set; } = [];
    public Vector2[] CrescentGlints { get; private set; } = [];
    public WaterGlint[] WaterGlints { get; private set; } = [];
    public List<PaintedCloud> Clouds { get; } = [];

    public void Update(ICanvasResourceCreator rc, float w, float h)
    {
        if (MathF.Abs(w - Width) < 0.5f && MathF.Abs(h - Height) < 0.5f && ReferenceEquals(Device, rc.Device))
            return;

        DisposeResources();
        Width = w;
        Height = h;
        Device = rc.Device;

        float scale = Math.Clamp(h / ReferenceCardHeight, 0.70f, 1.12f);
        float u = scale;
        Unit = u;
        HorizonY = h * 0.855f;
        BaseY = HorizonY + 6f * u;
        PoolTop = BaseY + 2.5f * u;

        ComplexCenterX = w * 0.50f;
        LeftMinaretX = ComplexCenterX - 105f * u;
        RightMinaretX = ComplexCenterX + 105f * u;
        LeftDomeX = ComplexCenterX - 64f * u;
        RightDomeX = ComplexCenterX + 64f * u;

        float[] arcadeOffsets = [-97f, -90.5f, -84f, -45.5f, -40f, 37.5f, 43.5f, 80.5f, 85.5f];
        ArcadeWindowXs = arcadeOffsets.Select(off => ComplexCenterX + off * u).ToArray();
        ReflectedLightXs =
        [
            ComplexCenterX, LeftMinaretX, RightMinaretX,
            ComplexCenterX - 5.8f * u, ComplexCenterX + 5.8f * u, LeftDomeX, RightDomeX,
        ];
        Lamps =
        [
            new(ComplexCenterX, BaseY - 13f * u),
            new(LeftMinaretX, BaseY - 43.6f * u),
            new(RightMinaretX, BaseY - 34.9f * u),
            new(ComplexCenterX - 5.8f * u, BaseY - 34.2f * u),
            new(ComplexCenterX + 5.8f * u, BaseY - 34.2f * u),
            new(LeftDomeX, BaseY - 25.8f * u),
            new(RightDomeX, BaseY - 25.8f * u),
        ];
        CrescentGlints =
        [
            new(ComplexCenterX - 1.4f * u, BaseY - 74.4f * u),
            new(LeftMinaretX - 0.8f * u, BaseY - 74.5f * u),
            new(RightMinaretX - 0.8f * u, BaseY - 59.5f * u),
        ];

        // Control points sit above the canvas to flatten the crown clear of hero copy and dive steeply to horizon.
        P0 = new Vector2(w * 0.045f, HorizonY);
        P1 = new Vector2(w * 0.060f, -h * 0.145f);
        P2 = new Vector2(w * 0.940f, -h * 0.145f);
        P3 = new Vector2(w * 0.955f, HorizonY);

        using (var pb = new CanvasPathBuilder(rc))
        {
            pb.BeginFigure(P0);
            pb.AddCubicBezier(P1, P2, P3);
            pb.EndFigure(CanvasFigureLoop.Open);
            ArcPath = CanvasGeometry.CreatePath(pb);
        }

        BuildStars(w, h, u);
        BuildMilkyWay(w, h);
        BuildFarRidge(rc, w, h, u);
        BuildMidDune(rc, w, h, u);
        BuildMosque(rc, w, h, u);
        BuildPalms(rc, u);
        BuildGateAndFinial(rc, u);
        BuildMinaretCrescents(rc, u);
        BuildWaterGlints(h, u);
        BuildReflectionMask(rc, w, h, u);

        foreach (var spec in SkyClouds.DashboardSpecs)
            Clouds.Add(SkyClouds.Paint(rc, spec, u));
    }

    public float BezierX(float t)
    {
        float tt = Math.Clamp(t, 0f, 1f);
        float inv = 1f - tt;
        return inv * inv * inv * P0.X + 3f * inv * inv * tt * P1.X + 3f * inv * tt * tt * P2.X + tt * tt * tt * P3.X;
    }

    public float BezierY(float t)
    {
        float tt = Math.Clamp(t, 0f, 1f);
        float inv = 1f - tt;
        return inv * inv * inv * P0.Y + 3f * inv * inv * tt * P1.Y + 3f * inv * tt * tt * P2.Y + tt * tt * tt * P3.Y;
    }

    public Vector2 Bezier(float t) => new(BezierX(t), BezierY(t));

    private void BuildStars(float w, float h, float u)
    {
        Stars = new StarInstance[StarSpecs.Length];
        for (int i = 0; i < StarSpecs.Length; i++)
        {
            var spec = StarSpecs[i];
            float rPx = spec.RadiusDp * u;
            Stars[i] = new StarInstance(
                rPx,
                rPx * 2.4f,
                spec.Phase,
                new Vector2(w * spec.XRatio, h * spec.YRatio),
                spec.RadiusDp > 1.02f,
                Math.Clamp((1.15f - spec.RadiusDp) / 0.7f, 0f, 1f) * 0.55f,
                spec.RadiusDp >= 0.8f);
        }

        var faint = new List<Vector2>(80);
        var bright = new List<Vector2>(30);
        for (int i = 0; i < 110; i++)
        {
            float x = Seeded(i * 7 + 401) * w;
            float y = h * 0.66f * MathF.Pow(Seeded(i * 7 + 402), 1.35f);
            if (Seeded(i * 7 + 403) > 0.74f) bright.Add(new Vector2(x, y));
            else faint.Add(new Vector2(x, y));
        }
        StarDustFaint = faint.ToArray();
        StarDustBright = bright.ToArray();
    }

    private void BuildMilkyWay(float w, float h)
    {
        float cx = w * 0.52f, cy = h * 0.36f;
        float rad = -24f * MathF.PI / 180f;
        float cosA = MathF.Cos(rad), sinA = MathF.Sin(rad);
        var faint = new List<Vector2>(60);
        var bright = new List<Vector2>(30);
        for (int i = 0; i < 90; i++)
        {
            float lx = (Seeded(i * 5 + 11) - 0.5f) * w * 1.4f;
            float ly = (Seeded(i * 5 + 12) - 0.5f) * h * 0.22f * (0.4f + Seeded(i * 5 + 13));
            var pt = new Vector2(cx + lx * cosA - ly * sinA, cy + lx * sinA + ly * cosA);
            if (Seeded(i * 5 + 14) > 0.66f) bright.Add(pt);
            else faint.Add(pt);
        }
        MilkyWayFaint = faint.ToArray();
        MilkyWayBright = bright.ToArray();
    }

    private void BuildFarRidge(ICanvasResourceCreator rc, float w, float h, float u)
    {
        using var pb = new CanvasPathBuilder(rc);
        pb.BeginFigure(0f, h);
        pb.AddLine(0f, HorizonY - 13f * u);
        pb.AddCubicBezier(new(w * 0.14f, HorizonY - 26f * u), new(w * 0.26f, HorizonY - 9f * u), new(w * 0.40f, HorizonY - 18f * u));
        pb.AddCubicBezier(new(w * 0.56f, HorizonY - 29f * u), new(w * 0.72f, HorizonY - 8f * u), new(w * 0.86f, HorizonY - 16f * u));
        pb.AddCubicBezier(new(w * 0.93f, HorizonY - 20f * u), new(w * 0.97f, HorizonY - 10f * u), new(w, HorizonY - 13f * u));
        pb.AddLine(w, h);
        pb.EndFigure(CanvasFigureLoop.Closed);
        FarRidgePath = CanvasGeometry.CreatePath(pb);
    }

    private void BuildMidDune(ICanvasResourceCreator rc, float w, float h, float u)
    {
        using var pb = new CanvasPathBuilder(rc);
        pb.BeginFigure(0f, h);
        pb.AddLine(0f, HorizonY + 1f * u);
        pb.AddCubicBezier(new(w * 0.20f, HorizonY - 11f * u), new(w * 0.38f, HorizonY + 3f * u), new(w * 0.56f, HorizonY - 7f * u));
        pb.AddCubicBezier(new(w * 0.76f, HorizonY - 18f * u), new(w * 0.90f, HorizonY), new(w, HorizonY - 4f * u));
        pb.AddLine(w, h);
        pb.EndFigure(CanvasFigureLoop.Closed);
        MidDunePath = CanvasGeometry.CreatePath(pb);
    }

    private void BuildMosque(ICanvasResourceCreator rc, float w, float h, float u)
    {
        float y0 = BaseY;
        float cx = ComplexCenterX;
        float leftWallEnd = cx - 117.7f * u;
        float rightWallStart = cx + 106.9f * u;
        float rightWallEnd = cx + 140.7f * u;
        float merlon = 5.9f * u;

        using var pb = new CanvasPathBuilder(rc);
        pb.BeginFigure(0f, h);
        pb.AddLine(0f, y0 - 9f * u);

        AddCrenellations(pb, 0f, leftWallEnd, y0, 9f * u, 5f * u, merlon);
        AddMinaret(pb, LeftMinaretX, y0, 72f * u, 5.2f * u);

        pb.AddLine(cx - 102.2f * u, y0 - 9f * u);
        pb.AddLine(cx - 102.2f * u, y0 - 19f * u);
        pb.AddLine(cx - 77.5f * u, y0 - 19f * u);
        AddSmallDome(pb, LeftDomeX, 13.7f * u, y0 - 19f * u, 18f * u);
        pb.AddLine(cx - 35.5f * u, y0 - 19f * u);

        pb.AddLine(cx - 31.0f * u, y0 - 19f * u);
        pb.AddLine(cx - 31.0f * u, y0 - 24f * u);
        pb.AddLine(cx - 26.4f * u, y0 - 24f * u);
        AddOnionDome(pb, cx, 26.4f * u, y0 - 24f * u, 37f * u);
        pb.AddLine(cx + 31.1f * u, y0 - 24f * u);
        pb.AddLine(cx + 31.1f * u, y0 - 19f * u);

        pb.AddLine(cx + 50.3f * u, y0 - 19f * u);
        AddSmallDome(pb, RightDomeX, 13.7f * u, y0 - 19f * u, 18f * u);
        pb.AddLine(cx + 88.6f * u, y0 - 19f * u);
        pb.AddLine(cx + 88.6f * u, y0 - 9f * u);

        AddMinaret(pb, RightMinaretX, y0, 57f * u, 4.6f * u);
        AddCrenellations(pb, rightWallStart, rightWallEnd, y0, 9f * u, 5f * u, merlon);

        pb.AddLine(w, y0 - 6f * u);
        pb.AddLine(w, h);
        pb.EndFigure(CanvasFigureLoop.Closed);
        MosquePath = CanvasGeometry.CreatePath(pb);
    }

    private static void AddCrenellations(CanvasPathBuilder pb, float x0, float x1, float baseY, float high, float low, float step)
    {
        float x = x0;
        bool up = true;
        float merlon = step * 0.55f;
        pb.AddLine(x0, baseY - high);
        while (x < x1 - step)
        {
            pb.AddLine(x + merlon, baseY - (up ? high : low));
            pb.AddLine(x + merlon, baseY - (up ? low : high));
            x += merlon;
            up = !up;
        }
        pb.AddLine(x1, baseY - high);
    }

    private static void AddMinaret(CanvasPathBuilder pb, float cx, float baseY, float height, float shaftW)
    {
        float foot = 9f * (shaftW / 5.2f);
        pb.AddLine(cx - shaftW * 0.46f, baseY - foot);
        pb.AddLine(cx - shaftW * 0.38f, baseY - height * 0.500f);
        pb.AddLine(cx - shaftW * 0.86f, baseY - height * 0.525f);
        pb.AddLine(cx - shaftW * 0.92f, baseY - height * 0.585f);
        pb.AddLine(cx - shaftW * 0.34f, baseY - height * 0.615f);
        pb.AddLine(cx - shaftW * 0.28f, baseY - height * 0.840f);
        pb.AddLine(cx - shaftW * 0.46f, baseY - height * 0.855f);
        pb.AddLine(cx - shaftW * 0.05f, baseY - height * 0.975f);
        pb.AddLine(cx - shaftW * 0.05f, baseY - height);
        pb.AddLine(cx + shaftW * 0.05f, baseY - height);
        pb.AddLine(cx + shaftW * 0.05f, baseY - height * 0.975f);
        pb.AddLine(cx + shaftW * 0.46f, baseY - height * 0.855f);
        pb.AddLine(cx + shaftW * 0.28f, baseY - height * 0.840f);
        pb.AddLine(cx + shaftW * 0.34f, baseY - height * 0.615f);
        pb.AddLine(cx + shaftW * 0.92f, baseY - height * 0.585f);
        pb.AddLine(cx + shaftW * 0.86f, baseY - height * 0.525f);
        pb.AddLine(cx + shaftW * 0.38f, baseY - height * 0.500f);
        pb.AddLine(cx + shaftW * 0.46f, baseY - foot);
    }

    private static void AddOnionDome(CanvasPathBuilder pb, float cx, float halfW, float yTop, float height)
    {
        pb.AddCubicBezier(
            new(cx - halfW * 1.24f, yTop - height * 0.30f),
            new(cx - halfW * 1.00f, yTop - height * 0.63f),
            new(cx - halfW * 0.34f, yTop - height * 0.90f));
        pb.AddCubicBezier(
            new(cx - halfW * 0.20f, yTop - height * 0.955f),
            new(cx - halfW * 0.11f, yTop - height * 0.985f),
            new(cx - halfW * 0.06f, yTop - height));
        pb.AddLine(cx - halfW * 0.042f, yTop - height - height * 0.26f);
        pb.AddLine(cx + halfW * 0.042f, yTop - height - height * 0.26f);
        pb.AddLine(cx + halfW * 0.06f, yTop - height);
        pb.AddCubicBezier(
            new(cx + halfW * 0.11f, yTop - height * 0.985f),
            new(cx + halfW * 0.20f, yTop - height * 0.955f),
            new(cx + halfW * 0.34f, yTop - height * 0.90f));
        pb.AddCubicBezier(
            new(cx + halfW * 1.00f, yTop - height * 0.63f),
            new(cx + halfW * 1.24f, yTop - height * 0.30f),
            new(cx + halfW, yTop));
    }

    private static void AddSmallDome(CanvasPathBuilder pb, float cx, float halfW, float yTop, float height)
    {
        pb.AddCubicBezier(
            new(cx - halfW * 1.14f, yTop - height * 0.52f),
            new(cx - halfW * 0.60f, yTop - height * 1.00f),
            new(cx - halfW * 0.05f, yTop - height * 1.04f));
        pb.AddLine(cx - halfW * 0.04f, yTop - height * 1.22f);
        pb.AddLine(cx + halfW * 0.04f, yTop - height * 1.22f);
        pb.AddLine(cx + halfW * 0.05f, yTop - height * 1.04f);
        pb.AddCubicBezier(
            new(cx + halfW * 0.60f, yTop - height * 1.00f),
            new(cx + halfW * 1.14f, yTop - height * 0.52f),
            new(cx + halfW, yTop));
    }

    private void BuildPalms(ICanvasResourceCreator rc, float u)
    {
        float cx = ComplexCenterX;
        using var pb = new CanvasPathBuilder(rc);
        AddPalm(pb, cx + 145f * u, BaseY + 1f * u, 34f * u, 1f, u);
        AddPalm(pb, cx + 178f * u, BaseY + 1f * u, 44f * u, -1f, u);
        AddPalm(pb, cx + 208f * u, BaseY + 1f * u, 28f * u, 1f, u);
        AddPalm(pb, cx - 172f * u, BaseY + 1f * u, 30f * u, 1f, u);
        AddPalm(pb, cx - 198f * u, BaseY + 1f * u, 38f * u, -1f, u);
        PalmsPath = CanvasGeometry.CreatePath(pb);
    }

    private static void AddPalm(CanvasPathBuilder pb, float x, float footY, float height, float lean, float u)
    {
        float topX = x + lean * height * 0.15f;
        float topY = footY - height;

        pb.BeginFigure(x - 1.7f * u, footY);
        pb.AddQuadraticBezier(new(x + lean * height * 0.045f, footY - height * 0.55f), new(topX - 1.0f * u, topY));
        pb.AddLine(topX + 1.0f * u, topY);
        pb.AddQuadraticBezier(new(x + lean * height * 0.045f + 2.6f * u, footY - height * 0.55f), new(x + 1.7f * u, footY));
        pb.EndFigure(CanvasFigureLoop.Closed);

        float l = height * 0.46f;
        foreach (var (dx, dy) in FrondDirs)
        {
            float ex = topX + dx * l;
            float ey = topY + dy * l * 0.78f;
            float mx = (topX + ex) * 0.5f;
            float my = (topY + ey) * 0.5f - l * 0.26f;
            pb.BeginFigure(topX, topY);
            pb.AddQuadraticBezier(new(mx, my), new(ex, ey));
            pb.AddQuadraticBezier(new(mx, my + 3.4f * u), new(topX, topY + 2.4f * u));
            pb.EndFigure(CanvasFigureLoop.Closed);
        }
    }

    private void BuildGateAndFinial(ICanvasResourceCreator rc, float u)
    {
        float cx = ComplexCenterX;
        float gateHalf = 6.4f * u;
        using (var pb = new CanvasPathBuilder(rc))
        {
            pb.BeginFigure(cx - gateHalf, BaseY - 9f * u);
            pb.AddLine(cx - gateHalf, BaseY - 13f * u);
            pb.AddQuadraticBezier(new(cx, BaseY - 21f * u), new(cx + gateHalf, BaseY - 13f * u));
            pb.AddLine(cx + gateHalf, BaseY - 9f * u);
            pb.EndFigure(CanvasFigureLoop.Closed);
            GatePath = CanvasGeometry.CreatePath(pb);
        }

        FinialPath = CreateCrescent(rc, cx, BaseY - 73f * u, 3.0f * u);
    }

    private void BuildMinaretCrescents(ICanvasResourceCreator rc, float u)
    {
        float r = 1.7f * u;
        using var left = CreateCrescent(rc, LeftMinaretX, BaseY - 72f * u - r * 1.1f, r);
        using var right = CreateCrescent(rc, RightMinaretX, BaseY - 57f * u - r * 1.1f, r);
        MinaretCrescentsPath = left.CombineWith(right, Matrix3x2.Identity, CanvasGeometryCombine.Union);
    }

    private static CanvasGeometry CreateCrescent(ICanvasResourceCreator rc, float cx, float cy, float r)
    {
        using var disc = CanvasGeometry.CreateCircle(rc, cx, cy, r);
        using var cut = CanvasGeometry.CreateCircle(rc, cx + r * 0.42f, cy - r * 0.16f, r * 0.82f);
        return disc.CombineWith(cut, Matrix3x2.Identity, CanvasGeometryCombine.Exclude);
    }

    private void BuildWaterGlints(float h, float u)
    {
        float depth = h - PoolTop;
        WaterGlints = new WaterGlint[WaterGlintCount];
        for (int i = 0; i < WaterGlintCount; i++)
        {
            float d = 0.03f + 0.94f * MathF.Pow(Seeded(i * 29 + 3), 1.4f);
            float rawLat = (Seeded(i * 29 + 5) - 0.5f) * 2f;
            float lateral = MathF.CopySign(MathF.Pow(MathF.Abs(rawLat), 1.4f), rawLat);
            WaterGlints[i] = new WaterGlint(
                d,
                PoolTop + 2.5f * u + d * (depth - 5f * u),
                lateral,
                Seeded(i * 29 + 9) * MathF.Tau,
                1.4f + 2.0f * Seeded(i * 29 + 17),
                0.6f + 2.4f * d);
        }
    }

    private void BuildReflectionMask(ICanvasResourceCreator rc, float w, float h, float u)
    {
        int rw = Math.Max(4, (int)MathF.Ceiling(w));
        int rh = Math.Max(0, (int)MathF.Ceiling(h - PoolTop));
        if (rh < 4 || MosquePath == null || PalmsPath == null) return;

        using var rt = new CanvasRenderTarget(rc, rw, rh, 96f);
        using (var ds = rt.CreateDrawingSession())
        {
            ds.Clear(Color.FromArgb(0, 0, 0, 0));
            // Mirror mosque and palms about the pool edge (poolTop -> y=0, squashed 0.88x vertically).
            ds.Transform = Matrix3x2.CreateTranslation(0f, -PoolTop) * Matrix3x2.CreateScale(1f, -ReflectionSquash);
            ds.FillGeometry(MosquePath, Color.FromArgb(255, 255, 255, 255));
            ds.FillGeometry(PalmsPath, Color.FromArgb(255, 255, 255, 255));
        }

        var raw = rt.GetPixelBytes();
        int n = rw * rh;
        var alpha = new float[n];
        for (int i = 0; i < n; i++) alpha[i] = raw[i * 4 + 3] / 255f;

        // Anisotropic Gaussian blur: still water smears an image wider horizontally than vertically.
        AnisotropicBlur(alpha, rw, rh, sigmaX: 1.3f * u, sigmaY: 0.6f * u);

        var outBytes = new byte[n * 4];
        for (int j = 0; j < rh; j++)
        {
            float fade = 0.72f - 0.60f * (j / (float)rh);
            int row = j * rw;
            for (int i = 0; i < rw; i++)
            {
                byte a = (byte)Math.Clamp((int)(alpha[row + i] * fade * 255f + 0.5f), 0, 255);
                if (a == 0) continue;
                int idx = (row + i) * 4;
                outBytes[idx] = a;
                outBytes[idx + 1] = a;
                outBytes[idx + 2] = a;
                outBytes[idx + 3] = a;
            }
        }

        ReflectionMask = CanvasBitmap.CreateFromBytes(rc, outBytes, rw, rh, DirectXPixelFormat.B8G8R8A8UIntNormalized, 96f, CanvasAlphaMode.Premultiplied);
        ReflectionWidth = rw;
        ReflectionHeight = rh;
    }

    private static void AnisotropicBlur(float[] data, int w, int h, float sigmaX, float sigmaY)
    {
        var tmp = new float[data.Length];
        BlurPass(data, tmp, w, h, sigmaX, horizontal: true);
        BlurPass(tmp, data, w, h, sigmaY, horizontal: false);
    }

    private static void BlurPass(float[] from, float[] to, int w, int h, float sigma, bool horizontal)
    {
        if (sigma < 0.3f)
        {
            Array.Copy(from, to, from.Length);
            return;
        }
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

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float acc = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    int cx = Math.Clamp(horizontal ? x + k : x, 0, w - 1);
                    int cy = Math.Clamp(horizontal ? y : y + k, 0, h - 1);
                    acc += from[cy * w + cx] * kernel[k + radius];
                }
                to[y * w + x] = acc;
            }
        }
    }

    internal static float Seeded(int i)
    {
        double x = Math.Sin(i * 12.9898) * 43758.5453;
        return (float)(x - Math.Floor(x));
    }

    private void DisposeResources()
    {
        ArcPath?.Dispose(); ArcPath = null;
        FarRidgePath?.Dispose(); FarRidgePath = null;
        MidDunePath?.Dispose(); MidDunePath = null;
        MosquePath?.Dispose(); MosquePath = null;
        PalmsPath?.Dispose(); PalmsPath = null;
        GatePath?.Dispose(); GatePath = null;
        FinialPath?.Dispose(); FinialPath = null;
        MinaretCrescentsPath?.Dispose(); MinaretCrescentsPath = null;
        ReflectionMask?.Dispose(); ReflectionMask = null;
        foreach (var c in Clouds) c.Dispose();
        Clouds.Clear();
    }

    public void Dispose() => DisposeResources();
}

internal static class SkyRenderer
{
    private const float WaterPace = 0.5f;
    private const float RoadHead = 4.5f;
    private const float RoadSpread = 26f;
    private const int ReflectionBands = 12;
    private const float FlightCycle = 45f;
    private const float FlightSeconds = 19f;
    private const float FirstFlight = 1.2f;
    private const float RestingFlight = 0.62f;
    private const float EasedWings = 0.3f;
    private const float MeteorCycle = 22f;
    private const float MeteorSeconds = 1.1f;
    private const float FirstMeteor = 3.2f;
    private const float GlintCycle = 13f;
    private const float GlintDelay = 5f;
    private const float GlintSeconds = 1.3f;
    private const float RayTurnSeconds = 600f;
    private const float MistLoop = 1.8f;

    private static readonly Color[] StarTints =
    [
        Color.FromArgb(255, 255, 255, 255),
        Color.FromArgb(255, 220, 230, 255),
        Color.FromArgb(255, 255, 241, 220),
        Color.FromArgb(255, 255, 255, 255),
        Color.FromArgb(255, 232, 238, 255),
    ];

    private static readonly float[] LampRanks = [0f, 0.35f, 0.45f, 0.15f, 0.25f, 0.55f, 0.62f];

    private static readonly (float Start, float Reach, float Pace, float HeightDp, float Squash, float Strength)[] MistBanks =
    [
        (0.10f, 0.42f, 1f / 260f, -3f, 0.15f, 0.80f),
        (0.70f, 0.52f, 1f / 340f, 4f, 0.13f, 0.72f),
        (1.15f, 0.36f, 1f / 220f, 9f, 0.12f, 0.62f),
        (1.50f, 0.46f, 1f / 300f, -7f, 0.14f, 0.60f),
    ];

    // ---- Layer 1: Static sky, twilight bands, Milky Way, dust, non-twinkling stars & cubic arc ----

    public static void DrawBack(CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo, float w, float h, SkyFrame f, bool showArc)
    {
        var c = f.Moment.Colors;
        var tw = f.Moment.Twilight;
        float horizonY = geo.HorizonY;
        bool isDay = f.Arc.IsDaytime;
        float orbX = geo.BezierX(f.Arc.Progress);
        float orbY = geo.BezierY(f.Arc.Progress);

        // Sky gradient down to the horizon line (clamped below behind the pool).
        using (var sky = new CanvasLinearGradientBrush(rc, Stops(c.Sky))
        { StartPoint = Vector2.Zero, EndPoint = new Vector2(0f, horizonY) })
        {
            ds.FillRectangle(0f, 0f, w, h, sky);
        }

        // Horizon bloom anchored under the sun or moon.
        var bloomCol = isDay ? ThemeService.ToColor(c.OrbGlow) : Color.FromArgb(255, 203, 213, 225);
        float bloomR = w * 0.62f;
        using (var bloom = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0f, Color = WithA(bloomCol, isDay ? 0.30f : 0.13f) },
            new CanvasGradientStop { Position = 0.5f, Color = WithA(bloomCol, isDay ? 0.10f : 0.05f) },
            new CanvasGradientStop { Position = 1f, Color = WithA(bloomCol, 0f) },
        ]) { Center = new Vector2(orbX, horizonY), RadiusX = bloomR, RadiusY = bloomR })
        {
            ds.FillCircle(orbX, horizonY, bloomR, bloom);
        }

        // Low sun atmospheric scatter around the orb.
        if (isDay)
        {
            float low = Math.Clamp(1f - (1f - MathF.Abs(f.Arc.Progress - 0.5f) * 2f) / 0.45f, 0f, 1f);
            if (low > 0.01f)
            {
                float glowR = w * (0.45f + 0.25f * low);
                using var lowBrush = new CanvasRadialGradientBrush(rc, [
                    new CanvasGradientStop { Position = 0f, Color = ThemeService.ToColor(c.OrbGlow, 0.30f * low) },
                    new CanvasGradientStop { Position = 0.45f, Color = ThemeService.ToColor(c.OrbGlow, 0.10f * low) },
                    new CanvasGradientStop { Position = 1f, Color = ThemeService.ToColor(c.OrbGlow, 0f) },
                ]) { Center = new Vector2(orbX, orbY), RadiusX = glowR, RadiusY = glowR };
                ds.FillCircle(orbX, orbY, glowR, lowBrush);
            }
        }

        DrawTwilight(ds, rc, tw, w, h, horizonY);
        if (tw.MilkyWay > 0.01f) DrawMilkyWay(ds, rc, geo, w, h, tw.MilkyWay);

        float dust = Math.Clamp((c.StarAlpha - 0.45f) / 0.4f, 0f, 1f);
        if (dust > 0.01f) DrawStarDust(ds, geo, dust);

        if (c.StarAlpha > 0.02f)
            DrawStars(ds, geo, twinkling: false, twinklePhase: 0f, animate: false, horizonY, isDay ? null : new Vector2(orbX, orbY), c.StarAlpha);

        if (showArc && geo.ArcPath != null)
        {
            using var round = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };
            var trackCol = Argb.Lerp(Argb.Hex(0xFFFFFFFF), c.OrbGlow, 0.45f);
            ds.DrawGeometry(geo.ArcPath, ThemeService.ToColor(trackCol, 0.26f), 1.3f, round);

            float end = Math.Clamp(f.Arc.Progress, 0f, 1f);
            if (end > 0.005f)
            {
                using var pb = new CanvasPathBuilder(rc);
                pb.BeginFigure(geo.Bezier(0f));
                const int steps = 52;
                for (int i = 1; i <= steps; i++) pb.AddLine(geo.Bezier(end * i / steps));
                pb.EndFigure(CanvasFigureLoop.Open);
                using var trail = CanvasGeometry.CreatePath(pb);
                ds.DrawGeometry(trail, ThemeService.ToColor(c.CardBorderTop, 0.285f), 4.2f, round);
                ds.DrawGeometry(trail, ThemeService.ToColor(c.OrbGlow, 0.95f), 1.9f, round);
            }
        }
    }

    // ---- Layer 2: Twinkling stars, meteor, sun rays, milestones, sun/moon, and two volumetric cloud decks ----

    public static void DrawLive(CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo, float w, float h, SkyFrame f, bool showArc)
    {
        var c = f.Moment.Colors;
        var tw = f.Moment.Twilight;
        float horizonY = geo.HorizonY;
        float t = f.Motion ? (float)f.Seconds : 0f;
        float progress = f.Arc.Progress;
        bool isDay = f.Arc.IsDaytime;
        var orb = geo.Bezier(progress);

        float twinklePhase = 0.22f + t / 12f;
        float orbBreath = f.Motion ? 0.5f + 0.5f * MathF.Sin(MathF.Tau * t / 9f) : 0.5f;
        float beaconPulse = f.Motion ? 0.5f + 0.5f * MathF.Sin(MathF.Tau * t / 6f) : 0.5f;

        if (c.StarAlpha > 0.02f)
            DrawStars(ds, geo, twinkling: true, twinklePhase, f.Motion, horizonY, isDay ? null : orb, c.StarAlpha);

        if (f.Motion)
        {
            float meteorWeight = MathF.Max(tw.MilkyWay, Math.Clamp((c.StarAlpha - 0.7f) / 0.3f, 0f, 1f) * 0.5f);
            float mp = MeteorProgress(t, meteorWeight);
            if (mp >= 0f) DrawMeteor(ds, rc, t, mp, w, h, horizonY);
        }

        if (showArc)
        {
            if (isDay && c.RayAlpha > 0.02f)
                DrawSunRays(ds, rc, orb, c, progress, w, c.RayAlpha, f.Motion ? t * 360f / RayTurnSeconds : 0f);

            DrawMilestones(ds, rc, geo, c, f.Arc, w, horizonY, beaconPulse);

            float sunAlpha, moonAlpha;
            if (isDay)
            {
                sunAlpha = progress > 0.82f ? Math.Clamp((0.94f - progress) / 0.12f, 0.55f, 1f) : 1f;
                moonAlpha = 0f;
            }
            else
            {
                moonAlpha = progress < 0.14f
                    ? Math.Clamp((progress - 0.08f) / 0.06f, 0.40f, 1f)
                    : progress > 0.86f
                        ? Math.Clamp((0.92f - progress) / 0.06f, 0.30f, 1f)
                        : 1f;
                sunAlpha = 0f;
            }

            if (sunAlpha > 0.02f) DrawSun(ds, rc, orb, c, progress, sunAlpha, orbBreath, w);
            if (moonAlpha > 0.02f) DrawCelestialMoon(ds, rc, orb, f.Moon.PhaseFraction, (float)f.Moon.Illumination, moonAlpha, orbBreath);
        }

        // Two offscreen volumetric cloud decks in front of the arc and sun/moon.
        float nightness = isDay ? c.StarAlpha * 0.5f : c.StarAlpha;
        float cloudFade = c.CloudAlpha + (SkyClouds.NightCloudStrength - c.CloudAlpha) * Math.Clamp(nightness, 0f, 1f);
        float cloudT = MathF.Floor(t * 15f) / 15f;
        SkyClouds.DrawDeck(ds, geo.Clouds, c, tw, layer: 0,
            drift: 0.18f + cloudT * SkyClouds.CloudLoop / SkyClouds.FarLoopSeconds,
            orb.X, orb.Y, nightness, cloudFade * 0.65f, w, h);
        SkyClouds.DrawDeck(ds, geo.Clouds, c, tw, layer: 1,
            drift: 0.42f + cloudT * SkyClouds.CloudLoop / SkyClouds.NearLoopSeconds,
            orb.X, orb.Y, nightness, cloudFade * 0.82f, w, h);
    }

    // ---- Layer 3: Static skyline, dunes, mosque lights, and still reflecting pool ----

    public static void DrawFront(CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo, float w, float h, SkyFrame f)
    {
        var c = f.Moment.Colors;
        bool isDay = f.Arc.IsDaytime;
        float orbX = geo.BezierX(f.Arc.Progress);
        float horizonY = geo.HorizonY;
        float baseY = geo.BaseY;
        float u = geo.Unit;

        var horizonSky = c.Sky.Count > 0 ? c.Sky[^1] : Argb.Hex(0xFF14243F);
        float daylight = Math.Clamp(1f - c.StarAlpha, 0f, 1f);
        var ink = Argb.Hex(0xFF03060F);
        var ridgeColor = Argb.Lerp(ink, horizonSky, 0.30f + 0.22f * daylight);
        var duneTop = Argb.Lerp(ink, horizonSky, 0.10f + 0.16f * daylight);
        var stone = Argb.Lerp(ink, horizonSky, 0.07f + 0.19f * daylight);

        // Warm haze settling on the horizon line.
        using (var haze = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0f, Color = ThemeService.ToColor(c.OrbGlow, 0f) },
            new CanvasGradientStop { Position = 1f, Color = ThemeService.ToColor(c.OrbGlow, isDay ? 0.16f : 0.09f) },
        ]) { StartPoint = new Vector2(0f, horizonY - 26f), EndPoint = new Vector2(0f, horizonY + 10f) })
        {
            ds.FillRectangle(0f, horizonY - 26f, w, 36f, haze);
        }

        if (geo.FarRidgePath != null)
            ds.FillGeometry(geo.FarRidgePath, ThemeService.ToColor(ridgeColor, 0.70f));

        if (geo.MidDunePath != null)
        {
            using var duneBrush = new CanvasLinearGradientBrush(rc, [
                new CanvasGradientStop { Position = 0f, Color = ThemeService.ToColor(duneTop, 0.92f) },
                new CanvasGradientStop { Position = 1f, Color = ThemeService.ToColor(Argb.Lerp(duneTop, ink, 0.6f), 0.97f) },
            ]) { StartPoint = new Vector2(0f, horizonY - 18f), EndPoint = new Vector2(0f, h) };
            ds.FillGeometry(geo.MidDunePath, duneBrush);
        }

        if (geo.MosquePath != null)
        {
            var crown = Argb.Lerp(stone, horizonSky, 0.16f * daylight);
            using var mBrush = new CanvasLinearGradientBrush(rc, [
                new CanvasGradientStop { Position = 0f, Color = ThemeService.ToColor(crown) },
                new CanvasGradientStop { Position = 0.5f, Color = ThemeService.ToColor(Argb.Lerp(stone, ink, 0.35f)) },
                new CanvasGradientStop { Position = 1f, Color = ThemeService.ToColor(Argb.Lerp(stone, ink, 0.55f)) },
            ]) { StartPoint = new Vector2(0f, baseY - 76f), EndPoint = new Vector2(0f, h) };
            ds.FillGeometry(geo.MosquePath, mBrush);
        }
        if (geo.PalmsPath != null)
            ds.FillGeometry(geo.PalmsPath, ThemeService.ToColor(stone));

        // Daytime sun warmth grazing the sunward flank of the domes and palms.
        if (isDay && daylight > 0.05f && geo.MosquePath != null && geo.PalmsPath != null)
        {
            float low = Math.Clamp(MathF.Abs(f.Arc.Progress - 0.5f) * 2f, 0f, 1f);
            float warmth = daylight * (0.06f + 0.10f * low);
            var sunPos = new Vector2(Math.Clamp(orbX, 0f, w), baseY - 40f);
            using var sunGlow = new CanvasRadialGradientBrush(rc, [
                new CanvasGradientStop { Position = 0f, Color = ThemeService.ToColor(c.OrbGlow, warmth) },
                new CanvasGradientStop { Position = 1f, Color = ThemeService.ToColor(c.OrbGlow, 0f) },
            ]) { Center = sunPos, RadiusX = w * 0.55f, RadiusY = w * 0.55f };
            ds.FillGeometry(geo.MosquePath, sunGlow);
            ds.FillGeometry(geo.PalmsPath, sunGlow);
        }

        if (geo.MinaretCrescentsPath != null) ds.FillGeometry(geo.MinaretCrescentsPath, ThemeService.ToColor(stone));
        if (geo.FinialPath != null) ds.FillGeometry(geo.FinialPath, ThemeService.ToColor(stone));

        float lightsAlpha = LightsAlpha(f);
        if (lightsAlpha > 0.02f) DrawMosqueLights(ds, rc, geo, baseY, lightsAlpha);

        DrawStillWater(ds, rc, geo, c, f.Arc, f.Moon, w, h, orbX);
    }

    // ---- Layer 4: Live water reflection, glints, lamp halos, mist, crescent glints & birds ----

    public static void DrawLiveForeground(CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo, float w, float h, SkyFrame f)
    {
        var c = f.Moment.Colors;
        var tw = f.Moment.Twilight;
        float t = f.Motion ? (float)f.Seconds : 0f;
        float orbX = geo.BezierX(f.Arc.Progress);
        float lightsAlpha = LightsAlpha(f);

        DrawMovingWater(ds, rc, geo, c, f.Arc, f.Moon, w, h, orbX, lightsAlpha, t, f.Motion);
        if (lightsAlpha > 0.02f) DrawLampGlow(ds, rc, geo, t, lightsAlpha, f.Motion);
        if (tw.Mist > 0.01f) DrawMist(ds, rc, geo, c, t, tw.Mist, w);

        if (f.Motion && f.Arc.IsDaytime)
        {
            float elevation = Math.Clamp(1f - MathF.Abs(f.Arc.Progress - 0.5f) * 2f, 0f, 1f);
            float sunUp = Math.Clamp(elevation / 0.12f, 0f, 1f) * Math.Clamp(1f - c.StarAlpha, 0f, 1f);
            if (sunUp > 0.02f) DrawCrescentGlint(ds, rc, geo, t, sunUp);
        }

        float birds = f.Motion ? tw.Birds : 0f;
        float flight = f.Motion ? FlightProgress(t, birds) : (tw.Birds > 0.3f ? RestingFlight : -1f);
        if (flight >= 0f)
        {
            int cycle = f.Motion ? FlightCycleOf(t) : 0;
            DrawBirds(ds, rc, geo, c, t, flight, cycle, tw.Birds, f.Motion, w, h);
        }
    }

    // ---- Layer 5: Edge vignette and dynamic light caught along the rounded glass rim ----

    public static void DrawGlass(CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo, float w, float h, SkyFrame f, float cornerRadius)
    {
        var c = f.Moment.Colors;
        float nightness = f.Arc.IsDaytime ? c.StarAlpha * 0.5f : c.StarAlpha;

        using (var vig = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0f, Color = Rgba(0, 0, 0, 0f) },
            new CanvasGradientStop { Position = 1f, Color = Rgba(0, 0, 0, 0.18f + 0.10f * Math.Clamp(nightness, 0f, 1f)) },
        ]) { Center = new Vector2(w * 0.5f, h * 0.48f), RadiusX = h * 0.95f, RadiusY = h * 0.95f })
        {
            ds.FillRectangle(0f, 0f, w, h, vig);
        }

        var orb = geo.Bezier(f.Arc.Progress);
        Color rimCol;
        float strength;
        if (f.Arc.IsDaytime)
        {
            rimCol = ThemeService.ToColor(Argb.Lerp(c.OrbGlow, c.OrbCore, 0.35f));
            strength = 0.80f;
        }
        else
        {
            float lit = Math.Clamp((float)f.Moon.Illumination, 0f, 1f);
            rimCol = Color.FromArgb(255, 221, 230, 255);
            strength = 0.42f * (0.35f + 0.65f * lit);
        }

        float radius = w * 0.62f;
        using var rimBrush = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0f, Color = WithA(rimCol, strength) },
            new CanvasGradientStop { Position = 0.45f, Color = WithA(rimCol, strength * 0.32f) },
            new CanvasGradientStop { Position = 1f, Color = WithA(rimCol, 0f) },
        ]) { Center = orb, RadiusX = radius, RadiusY = radius };

        void DrawEdge(float strokeWidth, float alpha)
        {
            float inset = 1f + strokeWidth * 0.5f;
            float cr = MathF.Max(0f, cornerRadius - inset);
            rimBrush.Opacity = alpha;
            ds.DrawRoundedRectangle(inset, inset, w - inset * 2f, h - inset * 2f, cr, cr, rimBrush, strokeWidth);
        }
        DrawEdge(5f, 0.22f);
        DrawEdge(1.3f, 1f);
    }

    // ---- Scene helpers ----

    private static void DrawTwilight(CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyTwilight tw, float w, float h, float horizonY)
    {
        if (tw.FalseDawn > 0.01f)
        {
            float k = tw.FalseDawn;
            var foot = new Vector2(w * 0.13f, horizonY);
            float reach = MathF.Max(1f, horizonY - h * 0.10f);
            using (ds.CreateLayer(1f, new Rect(0, 0, w, horizonY)))
            {
                using var fd = new CanvasRadialGradientBrush(rc, [
                    new CanvasGradientStop { Position = 0.00f, Color = Rgba(210, 220, 255, 0.13f * k) },
                    new CanvasGradientStop { Position = 0.35f, Color = Rgba(200, 210, 255, 0.06f * k) },
                    new CanvasGradientStop { Position = 0.70f, Color = Rgba(200, 210, 255, 0.02f * k) },
                    new CanvasGradientStop { Position = 1.00f, Color = Rgba(200, 210, 255, 0f) },
                ]) { Center = foot, RadiusX = reach * 0.17f, RadiusY = reach };
                ds.FillEllipse(foot, reach * 0.17f, reach, fd);
            }
        }
        if (tw.TrueDawn > 0.01f)
            HorizonBand(ds, rc, horizonY - 20f, w * 1.1f, 0.20f, w * 0.06f, w,
                (Argb.Hex(0xFFF4F6FF), 0.62f), (Argb.Hex(0xFFDCE4FF), 0.30f), (Argb.Hex(0xFFD6E0FF), 0.08f), tw.TrueDawn);
        if (tw.SunriseGlow > 0.01f)
            HorizonBand(ds, rc, horizonY, w * 0.95f, 0.30f, w * 0.06f, w,
                (Argb.Hex(0xFFFFB46B), 0.55f), (Argb.Hex(0xFFFF8F6B), 0.24f), (Argb.Hex(0xFFFFC8A0), 0.06f), tw.SunriseGlow);
        if (tw.SunsetGlow > 0.01f)
            HorizonBand(ds, rc, horizonY, w * 1.0f, 0.32f, w * 0.94f, w,
                (Argb.Hex(0xFFFFC266), 0.50f), (Argb.Hex(0xFFFF9A4D), 0.22f), (Argb.Hex(0xFFFFD8A0), 0.05f), tw.SunsetGlow);
        if (tw.Shafaq > 0.01f)
            HorizonBand(ds, rc, horizonY, w * 0.9f, 0.34f, w * 0.94f, w,
                (Argb.Hex(0xFFFF6E3C), 0.70f), (Argb.Hex(0xFFDC4646), 0.34f), (Argb.Hex(0xFFB03A5A), 0.08f), tw.Shafaq);
    }

    private static void HorizonBand(
        CanvasDrawingSession ds, ICanvasResourceCreator rc,
        float cy, float radius, float squash, float cx, float w,
        (Argb Col, float A) inner, (Argb Col, float A) mid, (Argb Col, float A) outer, float strength)
    {
        float ry = MathF.Max(1f, radius * squash);
        using var brush = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = ThemeService.ToColor(inner.Col, inner.A * strength) },
            new CanvasGradientStop { Position = 0.30f, Color = ThemeService.ToColor(mid.Col, mid.A * strength) },
            new CanvasGradientStop { Position = 0.65f, Color = ThemeService.ToColor(outer.Col, outer.A * strength) },
            new CanvasGradientStop { Position = 1.00f, Color = ThemeService.ToColor(outer.Col, 0f) },
        ]) { Center = new Vector2(cx, cy), RadiusX = radius, RadiusY = ry };
        ds.FillRectangle(0f, cy - ry, w, ry * 2f, brush);
    }

    private static void DrawMilkyWay(CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo, float w, float h, float strength)
    {
        var center = new Vector2(w * 0.52f, h * 0.36f);
        float half = h * 0.16f;
        var saved = ds.Transform;
        ds.Transform = Matrix3x2.CreateRotation(-24f * MathF.PI / 180f, center) * saved;
        using (var band = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.0f, Color = Rgba(190, 200, 255, 0f) },
            new CanvasGradientStop { Position = 0.5f, Color = Rgba(190, 200, 255, 0.15f * strength) },
            new CanvasGradientStop { Position = 1.0f, Color = Rgba(190, 200, 255, 0f) },
        ]) { StartPoint = new Vector2(0f, center.Y - half), EndPoint = new Vector2(0f, center.Y + half) })
        {
            ds.FillRectangle(center.X - w * 0.8f, center.Y - half, w * 1.6f, half * 2f, band);
        }
        ds.Transform = saved;

        var faintCol = Rgba(225, 230, 255, 0.24f * strength);
        var brightCol = Rgba(225, 230, 255, 0.48f * strength);
        foreach (var pt in geo.MilkyWayFaint) ds.FillCircle(pt, 0.42f, faintCol);
        foreach (var pt in geo.MilkyWayBright) ds.FillCircle(pt, 0.48f, brightCol);
    }

    private static void DrawStarDust(CanvasDrawingSession ds, SkyGeometry geo, float strength)
    {
        var faintCol = Rgba(221, 228, 255, 0.30f * strength);
        var brightCol = Rgba(255, 255, 255, 0.55f * strength);
        foreach (var pt in geo.StarDustFaint) ds.FillCircle(pt, 0.50f, faintCol);
        foreach (var pt in geo.StarDustBright) ds.FillCircle(pt, 0.62f, brightCol);
    }

    private static void DrawStars(
        CanvasDrawingSession ds, SkyGeometry geo, bool twinkling, float twinklePhase,
        bool animate, float horizonY, Vector2? moonCenter, float dim)
    {
        const float glareInner = 24f, glareSpan = 46f, glintStroke = 0.55f, glintLength = 4.5f;
        for (int i = 0; i < geo.Stars.Length; i++)
        {
            var star = geo.Stars[i];
            if (star.Twinkles != twinkling) continue;
            float twinkle = !twinkling ? 0.90f : (animate ? 0.80f + 0.20f * MathF.Sin(MathF.Tau * (twinklePhase + star.Phase)) : 0.85f);
            float emergeOut = Math.Clamp((dim - star.Emerge) / 0.35f, 0f, 1f);
            float visibility = Math.Clamp((horizonY - star.Center.Y) / (horizonY * 0.55f), 0f, 1f) * emergeOut * (0.5f + 0.5f * dim);
            if (moonCenter is { } mc)
            {
                float d = Vector2.Distance(star.Center, mc);
                visibility *= Math.Clamp((d - glareInner) / glareSpan, 0f, 1f);
            }
            if (visibility < 0.02f) continue;

            var tint = StarTints[i % StarTints.Length];
            ds.FillCircle(star.Center, star.HaloRadiusPx, WithA(tint, twinkle * 0.16f * visibility));
            ds.FillCircle(star.Center, star.RadiusPx, WithA(tint, twinkle * 0.90f * visibility));
            if (star.HasGlint)
            {
                float len = glintLength * (0.7f + 0.3f * twinkle);
                var gc = WithA(tint, 0.45f * twinkle * visibility);
                ds.DrawLine(star.Center.X - len, star.Center.Y, star.Center.X + len, star.Center.Y, gc, glintStroke);
                ds.DrawLine(star.Center.X, star.Center.Y - len, star.Center.X, star.Center.Y + len, gc, glintStroke);
            }
        }
    }

    private static void DrawMilestones(
        CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo,
        SkyColors c, SolarArc arc, float w, float horizonY, float beaconPulse)
    {
        if (arc.Milestones.Count == 0) return;
        const float padX = 14f, clearance = 4f;
        float progress = arc.Progress;

        foreach (var m in arc.Milestones)
        {
            float px = Math.Clamp(geo.BezierX(m.Progress), padX, w - padX);
            float py = Math.Clamp(geo.BezierY(m.Progress), padX, horizonY - clearance);
            var pt = new Vector2(px, py);

            float dist = MathF.Abs(progress - m.Progress);
            float alpha = dist < 0.05f ? Math.Clamp(dist / 0.05f, 0f, 1f) : 1f;
            if (alpha < 0.06f) continue;

            if (m.IsNext)
            {
                float outer = 7f * (0.88f + 0.24f * beaconPulse);
                DrawDiamond(ds, rc, pt, outer, ThemeService.ToColor(c.OrbGlow, 0.26f * (0.70f + 0.60f * beaconPulse) * alpha));
                DrawDiamond(ds, rc, pt, 3.4f, Rgba(255, 213, 79, alpha));
                DrawDiamond(ds, rc, pt, 1.5f, Rgba(255, 255, 255, 0.90f * alpha));
            }
            else
            {
                ds.FillCircle(pt, 4.4f, ThemeService.ToColor(c.OrbGlow, (m.IsPast ? 0.22f : 0.34f) * alpha));
                ds.FillCircle(pt, m.IsPast ? 1.8f : 2.2f, Rgba(255, 255, 255, (m.IsPast ? 0.42f : 0.80f) * alpha));
            }
        }
    }

    private static void DrawDiamond(CanvasDrawingSession ds, ICanvasResourceCreator rc, Vector2 pt, float r, Color color)
    {
        using var pb = new CanvasPathBuilder(rc);
        pb.BeginFigure(pt.X, pt.Y - r);
        pb.AddLine(pt.X + r, pt.Y);
        pb.AddLine(pt.X, pt.Y + r);
        pb.AddLine(pt.X - r, pt.Y);
        pb.EndFigure(CanvasFigureLoop.Closed);
        using var geo = CanvasGeometry.CreatePath(pb);
        ds.FillGeometry(geo, color);
    }

    private static void DrawSun(
        CanvasDrawingSession ds, ICanvasResourceCreator rc, Vector2 orb,
        SkyColors c, float progress, float sunAlpha, float breath, float w)
    {
        float warm = SunLow(progress, 0.12f);
        float near = SunLow(progress, 0.08f);
        float low = near * near;
        var warmGlow = Argb.Lerp(c.OrbGlow, Argb.Hex(0xFFFF8A4C), warm);
        var coreColor = Argb.Lerp(Argb.Lerp(c.OrbCore, Argb.Hex(0xFFFFD54F), warm), Argb.Hex(0xFFFFBE6A), low);

        float auraR = 42f * (0.96f + 0.08f * breath);
        float auraStrength = sunAlpha * (0.88f + 0.24f * breath);
        using (var aura = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.00f, Color = ThemeService.ToColor(warmGlow, MathF.Min(1f, 0.46f * auraStrength)) },
            new CanvasGradientStop { Position = 0.35f, Color = ThemeService.ToColor(warmGlow, MathF.Min(1f, 0.19f * auraStrength)) },
            new CanvasGradientStop { Position = 0.70f, Color = Rgba(255, 224, 130, 0.055f * sunAlpha) },
            new CanvasGradientStop { Position = 1.00f, Color = Rgba(255, 224, 130, 0f) },
        ]) { Center = orb, RadiusX = auraR, RadiusY = auraR })
        {
            ds.FillCircle(orb, auraR, aura);
        }

        const float chromoR = 18f;
        using (var chromo = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.0f, Color = Rgba(255, 255, 255, 0.95f * sunAlpha) },
            new CanvasGradientStop { Position = 0.5f, Color = ThemeService.ToColor(warmGlow, 0.68f * sunAlpha) },
            new CanvasGradientStop { Position = 1.0f, Color = ThemeService.ToColor(warmGlow, 0f) },
        ]) { Center = orb, RadiusX = chromoR, RadiusY = chromoR })
        {
            ds.FillCircle(orb, chromoR, chromo);
        }

        if (low > 0.01f && w > 0f)
        {
            float flareR = w * 0.36f;
            float flareRy = MathF.Max(1f, flareR * 0.06f);
            using var flare = new CanvasRadialGradientBrush(rc, [
                new CanvasGradientStop { Position = 0.00f, Color = Rgba(255, 226, 176, 0.55f * low * sunAlpha) },
                new CanvasGradientStop { Position = 0.25f, Color = Rgba(255, 180, 107, 0.22f * low * sunAlpha) },
                new CanvasGradientStop { Position = 1.00f, Color = Rgba(255, 180, 107, 0f) },
            ]) { Center = orb, RadiusX = flareR, RadiusY = flareRy };
            ds.FillEllipse(orb, flareR, flareRy, flare);
        }

        float sx = 1f + 0.10f * low, sy = 1f - 0.08f * low;
        float rOuter = 7.2f * (1f + 0.22f * low);
        float rInner = 4.6f * (1f + 0.30f * low);
        ds.FillEllipse(orb, rOuter * sx, rOuter * sy, ThemeService.ToColor(coreColor, sunAlpha));
        ds.FillEllipse(orb, rInner * sx, rInner * sy, ThemeService.ToColor(Argb.Lerp(Argb.Hex(0xFFFFFFFF), Argb.Hex(0xFFFFDDA6), low), sunAlpha));
    }

    private static void DrawCelestialMoon(
        CanvasDrawingSession ds, ICanvasResourceCreator rc, Vector2 orb,
        float phaseFraction, float illuminatedFraction, float moonAlpha, float breath)
    {
        const float moonR = 12f;
        float glow = moonAlpha * (0.30f + 0.70f * Math.Clamp(illuminatedFraction, 0f, 1f));
        float haloGlow = glow * (0.85f + 0.30f * breath);

        float haloR = 28f * (0.95f + 0.10f * breath);
        using (var halo = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.0f, Color = Rgba(255, 255, 255, MathF.Min(1f, 0.28f * haloGlow)) },
            new CanvasGradientStop { Position = 0.5f, Color = Rgba(203, 213, 225, MathF.Min(1f, 0.10f * haloGlow)) },
            new CanvasGradientStop { Position = 1.0f, Color = Rgba(203, 213, 225, 0f) },
        ]) { Center = orb, RadiusX = haloR, RadiusY = haloR })
        {
            ds.FillCircle(orb, haloR, halo);
        }

        const float discGlowR = 16f;
        using (var dg = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.0f, Color = Rgba(255, 255, 255, 0.50f * glow) },
            new CanvasGradientStop { Position = 0.5f, Color = Rgba(226, 232, 240, 0.18f * glow) },
            new CanvasGradientStop { Position = 1.0f, Color = Rgba(226, 232, 240, 0f) },
        ]) { Center = orb, RadiusX = discGlowR, RadiusY = discGlowR })
        {
            ds.FillCircle(orb, discGlowR, dg);
        }

        float unlit = (1f - Math.Clamp(illuminatedFraction, 0f, 1f)) * moonAlpha;
        if (unlit > 0.03f)
        {
            using var earth = new CanvasRadialGradientBrush(rc, [
                new CanvasGradientStop { Position = 0.0f, Color = Rgba(203, 213, 225, 0.10f * unlit) },
                new CanvasGradientStop { Position = 0.5f, Color = Rgba(148, 163, 184, 0.04f * unlit) },
                new CanvasGradientStop { Position = 1.0f, Color = Rgba(148, 163, 184, 0f) },
            ]) { Center = orb, RadiusX = moonR, RadiusY = moonR };
            ds.FillCircle(orb, moonR, earth);
        }

        using var lit = BuildMoonLimb(rc, orb, moonR, phaseFraction);
        if (lit == null) return;
        float dirX = phaseFraction <= 0.5f ? 1f : -1f;
        using var fill = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0.0f, Color = Rgba(255, 255, 255, moonAlpha) },
            new CanvasGradientStop { Position = 0.5f, Color = Rgba(248, 250, 252, moonAlpha) },
            new CanvasGradientStop { Position = 1.0f, Color = Rgba(203, 213, 225, 0.94f * moonAlpha) },
        ])
        {
            StartPoint = new Vector2(orb.X + moonR * 0.85f * dirX, orb.Y - moonR * 0.65f),
            EndPoint = new Vector2(orb.X - moonR * 0.65f * dirX, orb.Y + moonR * 0.65f),
        };
        ds.FillGeometry(lit, fill);
    }

    internal static CanvasGeometry? BuildMoonLimb(ICanvasResourceCreator rc, Vector2 center, float moonR, float phaseFraction)
    {
        if (phaseFraction is >= 0.47f and <= 0.53f)
            return CanvasGeometry.CreateCircle(rc, center, moonR);
        if (phaseFraction < 0.02f || phaseFraction > 0.98f)
            return null;

        float k = MathF.Cos(phaseFraction * MathF.Tau);
        bool isWaxing = phaseFraction is >= 0f and <= 0.5f;
        const float m = 0.55228475f;
        float cx = center.X, cy = center.Y;
        float limbX = isWaxing ? moonR : -moonR;
        float rx = k * limbX;

        using var pb = new CanvasPathBuilder(rc);
        pb.BeginFigure(cx, cy - moonR);
        pb.AddCubicBezier(new(cx + limbX * m, cy - moonR), new(cx + limbX, cy - moonR * m), new(cx + limbX, cy));
        pb.AddCubicBezier(new(cx + limbX, cy + moonR * m), new(cx + limbX * m, cy + moonR), new(cx, cy + moonR));

        bool isCrescent = (phaseFraction is >= 0.02f and <= 0.22f) || (phaseFraction is >= 0.78f and <= 0.98f);
        if (isCrescent && k > 0.05f)
        {
            float innerRx = limbX * Math.Clamp(k * 0.90f, 0.18f, 0.85f);
            pb.AddCubicBezier(new(cx + innerRx * 0.22f, cy + moonR * 0.68f), new(cx + innerRx, cy + moonR * 0.38f), new(cx + innerRx, cy));
            pb.AddCubicBezier(new(cx + innerRx, cy - moonR * 0.38f), new(cx + innerRx * 0.22f, cy - moonR * 0.68f), new(cx, cy - moonR));
        }
        else
        {
            pb.AddCubicBezier(new(cx + rx * m, cy + moonR), new(cx + rx, cy + moonR * m), new(cx + rx, cy));
            pb.AddCubicBezier(new(cx + rx, cy - moonR * m), new(cx + rx * m, cy - moonR), new(cx, cy - moonR));
        }

        pb.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(pb);
    }

    internal static void DrawMoon(CanvasDrawingSession ds, ICanvasResourceCreator rc, Vector2 centre, float r, MoonPhase moon, SkyColors c)
    {
        ds.FillCircle(centre, r, Rgba(200, 210, 230, 0.18f));
        using var shape = BuildMoonLimb(rc, centre, r, moon.PhaseFraction);
        if (shape == null) return;
        ds.FillGeometry(shape, Rgba(250, 248, 235, 1f));
        using (ds.CreateLayer(1f, shape))
        {
            ds.FillCircle(centre + new Vector2(r * 0.25f, -r * 0.2f), r * 0.22f, Rgba(180, 180, 170, 0.35f));
            ds.FillCircle(centre + new Vector2(-r * 0.2f, r * 0.3f), r * 0.16f, Rgba(180, 180, 170, 0.30f));
        }
    }

    private static float LightsAlpha(SkyFrame f)
    {
        if (!f.Arc.IsDaytime || f.Moment.Colors.StarAlpha > 0.5f) return 1f;
        // Keep the mosque unlit during daytime (including Asr); warm lamps up only at dusk/dawn edge.
        float dusk = Math.Clamp((f.Arc.Progress - 0.815f) / 0.025f, 0f, 1f);
        float dawn = Math.Clamp((0.205f - f.Arc.Progress) / 0.025f, 0f, 1f);
        return MathF.Max(dusk, dawn) * 0.60f;
    }

    private static float LampOn(float alpha, float rank) => Math.Clamp(alpha * 1.8f - rank * 0.8f, 0f, 1f);

    private static void DrawMosqueLights(CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo, float baseY, float alpha)
    {
        float u = geo.Unit;
        float cx = geo.ComplexCenterX;

        void Window(float x, float dyDp, float wDp, float hDp, float strength, float rank)
        {
            float on = LampOn(alpha, rank);
            if (on < 0.01f) return;
            float ww = wDp * u, hh = hDp * u;
            float left = x - ww * 0.5f, top = baseY - dyDp * u;
            var center = new Vector2(x, top + hh * 0.5f);
            Glow(ds, rc, center, 6.5f * u, Rgba(255, 196, 107, 0.26f * strength * on));
            ds.FillRoundedRectangle(left, top, ww, hh, 1.5f * u, 1.5f * u, Rgba(255, 224, 163, 0.92f * strength * on));
        }

        Window(geo.LeftMinaretX, 46f, 2.3f, 4.8f, 1.0f, LampRanks[1]);
        Window(geo.RightMinaretX, 37f, 2.0f, 4.2f, 0.9f, LampRanks[2]);
        Window(cx - 5.8f * u, 37f, 2.4f, 5.6f, 0.95f, LampRanks[3]);
        Window(cx + 5.8f * u, 37f, 2.4f, 5.6f, 0.95f, LampRanks[4]);
        Window(geo.LeftDomeX, 28f, 2.1f, 4.4f, 0.8f, LampRanks[5]);
        Window(geo.RightDomeX, 28f, 2.1f, 4.4f, 0.8f, LampRanks[6]);
        for (int i = 0; i < geo.ArcadeWindowXs.Length; i++)
            Window(geo.ArcadeWindowXs[i], 15.5f, 1.8f, 4.0f, 0.62f, 0.30f + 0.65f * SkyGeometry.Seeded(i * 7 + 3));

        float gate = LampOn(alpha, LampRanks[0]);
        if (gate > 0.01f && geo.GatePath != null)
        {
            using var gateBrush = new CanvasLinearGradientBrush(rc, [
                new CanvasGradientStop { Position = 0f, Color = Rgba(255, 196, 107, 0.88f * gate) },
                new CanvasGradientStop { Position = 1f, Color = Rgba(255, 158, 61, 0.28f * gate) },
            ]) { StartPoint = new Vector2(0f, baseY - 18f * u), EndPoint = new Vector2(0f, baseY - 9f * u) };
            ds.FillGeometry(geo.GatePath, gateBrush);
            Glow(ds, rc, new Vector2(cx, baseY - 9f * u), 16f * u, Rgba(255, 158, 61, 0.22f * gate));
        }

        if (geo.FinialPath != null)
            ds.FillGeometry(geo.FinialPath, Rgba(255, 213, 79, 0.95f * LampOn(alpha, 0.7f)));
        if (geo.MinaretCrescentsPath != null)
            ds.FillGeometry(geo.MinaretCrescentsPath, Rgba(255, 213, 79, 0.85f * LampOn(alpha, 0.8f)));
    }

    private readonly record struct WaterRoadInfo(Argb Core, Argb Halo, Argb Column, float Strength);

    private static WaterRoadInfo? GetWaterRoad(SkyColors sky, SolarArc arc, MoonPhase moon)
    {
        float p = arc.Progress;
        if (p < 0.03f || p > 0.97f) return null;
        if (arc.IsDaytime)
        {
            float amber = SunLow(p, 0.16f);
            float fiery = SunLow(p, 0.06f);
            return new WaterRoadInfo(
                Argb.Lerp(Argb.Lerp(Argb.Hex(0xFFFFFDF5), Argb.Hex(0xFFFFF8E1), amber), Argb.Hex(0xFFFFECE5), fiery),
                Argb.Lerp(Argb.Lerp(Argb.Hex(0xFFFFE082), Argb.Hex(0xFFFFB74D), amber), Argb.Hex(0xFFFF7043), fiery),
                Argb.Lerp(Argb.Lerp(sky.OrbGlow, Argb.Hex(0xFFF57C00), amber), Argb.Hex(0xFFD84315), fiery),
                0.38f + 0.14f * amber + 0.03f * fiery);
        }
        float lunar = Math.Clamp((float)moon.Illumination, 0f, 1f);
        return new WaterRoadInfo(Argb.Hex(0xFFFFFFFF), Argb.Hex(0xFFE2E8F0), Argb.Hex(0xFF94A3B8), 0.22f + 0.30f * lunar);
    }

    private static void DrawStillWater(
        CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo,
        SkyColors sky, SolarArc arc, MoonPhase moon, float w, float h, float orbX)
    {
        float top = geo.PoolTop;
        float depth = h - top;
        if (depth < 4f) return;
        float u = geo.Unit;
        bool isDay = arc.IsDaytime;

        using (ds.CreateLayer(1f, new Rect(0, top, w, depth)))
        {
            var horizonSky = sky.Sky.Count > 0 ? sky.Sky[^1] : Argb.Hex(0xFF14243F);
            var zenithSky = sky.Sky.Count > 0 ? sky.Sky[0] : Argb.Hex(0xFF0A0F1D);
            using (var water = new CanvasLinearGradientBrush(rc, [
                new CanvasGradientStop { Position = 0.0f, Color = ThemeService.ToColor(Argb.Lerp(horizonSky, Argb.Hex(0xFF020612), 0.35f)) },
                new CanvasGradientStop { Position = 0.5f, Color = ThemeService.ToColor(Argb.Lerp(zenithSky, Argb.Hex(0xFF030714), 0.58f)) },
                new CanvasGradientStop { Position = 1.0f, Color = isDay ? Rgba(2, 7, 19, 1f) : Rgba(1, 3, 7, 1f) },
            ]) { StartPoint = new Vector2(0f, top), EndPoint = new Vector2(0f, h) })
            {
                ds.FillRectangle(0f, top, w, depth, water);
            }

            using (var sides = new CanvasLinearGradientBrush(rc, [
                new CanvasGradientStop { Position = 0.00f, Color = Rgba(0, 0, 0, 0.34f) },
                new CanvasGradientStop { Position = 0.10f, Color = Rgba(0, 0, 0, 0f) },
                new CanvasGradientStop { Position = 0.90f, Color = Rgba(0, 0, 0, 0f) },
                new CanvasGradientStop { Position = 1.00f, Color = Rgba(0, 0, 0, 0.34f) },
            ]) { StartPoint = new Vector2(0f, top), EndPoint = new Vector2(w, top) })
            {
                ds.FillRectangle(0f, top, w, depth, sides);
            }

            float shade = MathF.Min(5.5f * u, depth * 0.35f);
            using (var lipShadow = new CanvasLinearGradientBrush(rc, [
                new CanvasGradientStop { Position = 0.0f, Color = Rgba(0, 0, 0, 0.55f) },
                new CanvasGradientStop { Position = 0.5f, Color = Rgba(0, 0, 0, 0.22f) },
                new CanvasGradientStop { Position = 1.0f, Color = Rgba(0, 0, 0, 0f) },
            ]) { StartPoint = new Vector2(0f, top), EndPoint = new Vector2(0f, top + shade) })
            {
                ds.FillRectangle(0f, top, w, shade, lipShadow);
            }

            if (GetWaterRoad(sky, arc, moon) is { } road)
            {
                using var lipGlow = new CanvasRadialGradientBrush(rc, [
                    new CanvasGradientStop { Position = 0.0f, Color = ThemeService.ToColor(road.Core, road.Strength * 0.62f) },
                    new CanvasGradientStop { Position = 0.5f, Color = ThemeService.ToColor(road.Halo, road.Strength * 0.26f) },
                    new CanvasGradientStop { Position = 1.0f, Color = ThemeService.ToColor(road.Halo, 0f) },
                ]) { Center = new Vector2(orbX, top), RadiusX = 14f, RadiusY = 14f };
                ds.FillCircle(orbX, top, 14f, lipGlow);
            }
        }

        // Ground wash before the mosque and pool.
        float washTop = geo.HorizonY - 4f;
        using (ds.CreateLayer(1f, new Rect(0, washTop, w, MathF.Max(1f, h - washTop))))
        {
            Glow(ds, rc, new Vector2(orbX, geo.HorizonY), w * 0.5f, ThemeService.ToColor(sky.OrbGlow, isDay ? 0.13f : 0.06f));
        }
    }

    private static void DrawMovingWater(
        CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo,
        SkyColors sky, SolarArc arc, MoonPhase moon, float w, float h, float orbX,
        float lightsAlpha, float t, bool animate)
    {
        float top = geo.PoolTop;
        float depth = h - top;
        if (depth < 4f) return;
        float u = geo.Unit;
        bool isDay = arc.IsDaytime;
        float waveTime = animate ? t * WaterPace : 0f;

        using (ds.CreateLayer(1f, new Rect(0, top, w, depth)))
        {
            // 12 gently swaying horizontal strips of the anisotropically blurred skyline reflection.
            if (geo.ReflectionMask != null && geo.ReflectionHeight > 0)
            {
                var horizonSky = sky.Sky.Count > 0 ? sky.Sky[^1] : Argb.Hex(0xFF010308);
                var reflCol = ThemeService.ToColor(Argb.Lerp(Argb.Hex(0xFF010308), horizonSky, 0.08f * (1f - sky.StarAlpha)));
                using var tinted = new TintEffect { Source = geo.ReflectionMask, Color = reflCol };
                int band = Math.Max(1, (int)MathF.Ceiling(geo.ReflectionHeight / (float)ReflectionBands));
                int y = 0, k = 0;
                while (y < geo.ReflectionHeight)
                {
                    int rows = Math.Min(band, geo.ReflectionHeight - y);
                    float deep = (y + rows * 0.5f) / geo.ReflectionHeight;
                    float dx = animate ? (0.3f + 1.3f * deep) * u * MathF.Sin(waveTime * 1.8f + k * 1.35f) : 0f;
                    var src = new Rect(0, y, geo.ReflectionWidth, rows);
                    var dst = new Rect(dx, top + y, geo.ReflectionWidth, rows);
                    ds.DrawImage(tinted, dst, src);
                    y += rows;
                    k++;
                }
            }

            // Soft warm lamp reflection streaks under the 7 lit openings.
            if (lightsAlpha > 0.02f)
            {
                for (int i = 0; i < geo.ReflectedLightXs.Length; i++)
                {
                    float on = LampOn(lightsAlpha, i < LampRanks.Length ? LampRanks[i] : 0.5f);
                    if (on < 0.01f) continue;
                    bool isGate = i == 0;
                    float len = (isGate ? 30f : 16f) * u;
                    float half = (isGate ? 4.4f : 1.9f) * u;
                    float sway = animate ? 0.7f * u * MathF.Sin(waveTime * 1.7f + i * 1.25f) : 0f;
                    float shimmer = animate ? 0.80f + 0.20f * MathF.Sin(waveTime * 2.3f + i * 2.1f) : 0.9f;
                    float a = Math.Clamp(0.62f * on * shimmer, 0f, 1f);
                    var center = new Vector2(geo.ReflectedLightXs[i] + sway, top + len * 0.40f);
                    using var streak = new CanvasRadialGradientBrush(rc, [
                        new CanvasGradientStop { Position = 0.00f, Color = Rgba(255, 224, 163, 0.85f * a) },
                        new CanvasGradientStop { Position = 0.35f, Color = Rgba(255, 196, 107, 0.42f * a) },
                        new CanvasGradientStop { Position = 1.00f, Color = Rgba(255, 196, 107, 0f) },
                    ]) { Center = center, RadiusX = half, RadiusY = len * 0.5f };
                    ds.FillEllipse(center, half, len * 0.5f, streak);
                }
            }

            // Top 10 bright stars reflected in still night water.
            if (sky.StarAlpha > 0.5f)
            {
                float starDim = animate ? 0.85f : 0.70f;
                int major = Math.Min(10, geo.Stars.Length);
                for (int s = 0; s < major; s++)
                {
                    var star = geo.Stars[s];
                    float reflY = top + MathF.Max(0f, geo.HorizonY - star.Center.Y) * 0.28f;
                    if (reflY < top + 3f || reflY > h - 4f) continue;
                    float reflX = star.Center.X + (animate ? 0.9f * u * MathF.Sin(waveTime * 1.8f + star.Phase) : 0f);
                    float tw = animate ? 0.35f + 0.65f * Math.Clamp(MathF.Sin(MathF.Tau * (waveTime / 7f + star.Phase)), 0f, 1f) : 0.65f;
                    float a = 0.25f * tw * starDim;
                    ds.FillCircle(reflX, reflY, 1.6f, WithA(StarTints[s % StarTints.Length], a * 0.40f));
                    ds.FillCircle(reflX, reflY, 0.7f, Rgba(255, 255, 255, a * 0.80f));
                }
            }

            // Sun or moon shimmering road on the water + 44 depth-weighted flecks with rolling swell bands.
            if (GetWaterRoad(sky, arc, moon) is { } road)
            {
                float breath = animate ? 0.88f + 0.12f * MathF.Sin(waveTime * 0.7f) : 0.94f;
                void RoadEllip(float cy, float halfW, float halfH, Argb col, float a)
                {
                    a = Math.Clamp(a, 0f, 1f);
                    if (a < 0.01f) return;
                    var pt = new Vector2(orbX, cy);
                    using var b = new CanvasRadialGradientBrush(rc, [
                        new CanvasGradientStop { Position = 0.0f, Color = ThemeService.ToColor(col, 0.70f * a) },
                        new CanvasGradientStop { Position = 0.4f, Color = ThemeService.ToColor(col, 0.30f * a) },
                        new CanvasGradientStop { Position = 1.0f, Color = ThemeService.ToColor(col, 0f) },
                    ]) { Center = pt, RadiusX = halfW, RadiusY = halfH };
                    ds.FillEllipse(pt, halfW, halfH, b);
                }

                RoadEllip(top, 34f, 4.5f * u, road.Halo, road.Strength * 1.0f * breath);
                RoadEllip(top, (RoadHead + 5f) * u, depth * 0.85f, road.Halo, road.Strength * 1.7f * breath);
                RoadEllip(top + depth * 0.62f, (RoadHead + RoadSpread * 0.75f) * u, depth * 0.62f, road.Column, road.Strength * 1.0f * breath);

                using var round = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };
                for (int i = 0; i < geo.WaterGlints.Length; i++)
                {
                    var g = geo.WaterGlints[i];
                    float cone = RoadHead * u + (RoadSpread - 4f) * u * g.Depth;
                    float x = orbX + g.Lateral * cone + (animate ? 0.8f * u * MathF.Sin(waveTime * 0.9f + g.Phase) : 0f);
                    float wave = animate ? 0.5f + 0.5f * MathF.Sin(waveTime * g.Speed + g.Phase) : 0.4f;
                    float swell = 0.55f + 0.45f * MathF.Sin(MathF.Tau * (g.Depth * 1.6f - waveTime * 0.22f));
                    float lit = wave * wave * wave * wave;
                    float a = Math.Clamp(road.Strength * 1.45f * lit * swell * (1f - 0.55f * MathF.Abs(g.Lateral)), 0f, 1f);
                    if (a < 0.03f) continue;
                    float len = g.Reach * u * (0.6f + 0.4f * lit);
                    float fleck = 0.9f + 0.6f * g.Depth;
                    if (lit > 0.45f)
                        ds.DrawLine(x - len, g.Y, x + len, g.Y, ThemeService.ToColor(road.Halo, a * 0.22f), fleck * 2.8f, round);
                    ds.DrawLine(x - len, g.Y, x + len, g.Y, ThemeService.ToColor(Argb.Lerp(road.Halo, road.Core, lit), a), fleck, round);
                }
            }
        }

        // Lit stone lip + glint point right under orbX.
        float rimPeak = isDay ? 0.34f : 0.18f;
        float rimBase = isDay ? 0.08f : 0.05f;
        float rimCenter = Math.Clamp(orbX / w, 0.05f, 0.95f);
        using (var lip = new CanvasLinearGradientBrush(rc, [
            new CanvasGradientStop { Position = 0f, Color = ThemeService.ToColor(sky.OrbGlow, rimBase) },
            new CanvasGradientStop { Position = MathF.Max(0.001f, rimCenter - 0.18f), Color = ThemeService.ToColor(sky.OrbGlow, rimPeak * 0.6f) },
            new CanvasGradientStop { Position = rimCenter, Color = ThemeService.ToColor(sky.OrbGlow, rimPeak) },
            new CanvasGradientStop { Position = MathF.Min(0.999f, rimCenter + 0.18f), Color = ThemeService.ToColor(sky.OrbGlow, rimPeak * 0.6f) },
            new CanvasGradientStop { Position = 1f, Color = ThemeService.ToColor(sky.OrbGlow, rimBase) },
        ]) { StartPoint = new Vector2(0f, top), EndPoint = new Vector2(w, top) })
        {
            ds.DrawLine(0f, top, w, top, lip, 1.2f);
        }
        Glow(ds, rc, new Vector2(orbX, top), 8f, isDay ? Rgba(255, 248, 225, rimPeak * 0.9f) : Rgba(255, 255, 255, rimPeak * 0.9f));
    }

    private static void DrawLampGlow(CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo, float t, float alpha, bool animate)
    {
        float u = geo.Unit;
        for (int i = 0; i < geo.Lamps.Length; i++)
        {
            float flicker = animate ? 0.90f + 0.10f * MathF.Sin(t * 0.9f + i * 1.7f) : 1f;
            float a = Math.Clamp(flicker * LampOn(alpha, i < LampRanks.Length ? LampRanks[i] : 0.5f), 0f, 1f);
            if (a < 0.02f) continue;
            float r = (i == 0 ? 12f : 7.5f) * u;
            Glow(ds, rc, geo.Lamps[i], r, Rgba(255, 196, 107, (i == 0 ? 0.16f : 0.16f) * a));
        }
    }

    private static void DrawMist(CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo, SkyColors sky, float t, float strength, float w)
    {
        var horizon = sky.Sky.Count > 0 ? sky.Sky[^1] : Argb.Hex(0xFFFFFFFF);
        var col = Argb.Lerp(horizon, Argb.Hex(0xFFFFFFFF), 0.62f);
        float u = geo.Unit;
        foreach (var b in MistBanks)
        {
            float x = (((b.Start + t * b.Pace) % MistLoop) - 0.4f) * w;
            float r = b.Reach * w;
            float ry = MathF.Max(1f, r * b.Squash);
            float cy = geo.HorizonY + b.HeightDp * u;
            float a = Math.Clamp(b.Strength * strength, 0f, 1f);
            using var brush = new CanvasRadialGradientBrush(rc, [
                new CanvasGradientStop { Position = 0.00f, Color = ThemeService.ToColor(col, 0.55f * a) },
                new CanvasGradientStop { Position = 0.45f, Color = ThemeService.ToColor(col, 0.26f * a) },
                new CanvasGradientStop { Position = 1.00f, Color = ThemeService.ToColor(col, 0f) },
            ]) { Center = new Vector2(x, cy), RadiusX = r, RadiusY = ry };
            ds.FillEllipse(x, cy, r, ry, brush);
        }
    }

    private static void DrawCrescentGlint(CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo, float t, float strength)
    {
        int cycle = (int)MathF.Floor(t / GlintCycle);
        float p = (t - cycle * GlintCycle - GlintDelay) / GlintSeconds;
        if (p < 0f || p > 1f || geo.CrescentGlints.Length == 0) return;
        var at = geo.CrescentGlints[cycle % geo.CrescentGlints.Length];
        float e = MathF.Sin(MathF.PI * p);
        float k = e * e * strength;
        float u = geo.Unit;
        Glow(ds, rc, at, 7f * u, Rgba(255, 241, 201, 0.55f * k));
        using var round = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };
        var col = Rgba(255, 255, 255, 0.9f * k);
        float lx = 7.5f * u * e, ly = 5f * u * e;
        ds.DrawLine(at.X - lx, at.Y, at.X + lx, at.Y, col, 0.7f, round);
        ds.DrawLine(at.X, at.Y - ly, at.X, at.Y + ly, col, 0.7f, round);
        float d = 2.2f * u * e;
        var faint = Rgba(255, 255, 255, 0.45f * k);
        ds.DrawLine(at.X - d, at.Y - d, at.X + d, at.Y + d, faint, 0.7f, round);
        ds.DrawLine(at.X - d, at.Y + d, at.X + d, at.Y - d, faint, 0.7f, round);
    }

    private static int FlightCycleOf(float t) => (int)MathF.Floor((t - FirstFlight) / FlightCycle);

    private static float FlightProgress(float t, float weight)
    {
        if (weight < 0.05f || t < FirstFlight) return -1f;
        int cycle = FlightCycleOf(t);
        if (cycle > 0 && SkyGeometry.Seeded(cycle * 31 + 7) > weight) return -1f;
        float p = (t - FirstFlight - cycle * FlightCycle) / FlightSeconds;
        return p is >= 0f and <= 1f ? p : -1f;
    }

    private static void DrawBirds(
        CanvasDrawingSession ds, ICanvasResourceCreator rc, SkyGeometry geo, SkyColors sky,
        float t, float flight, int cycle, float weight, bool animate, float w, float h)
    {
        float u = geo.Unit;
        int s = cycle * 31;
        bool eastward = SkyGeometry.Seeded(s + 1) > 0.5f;
        float altitude = h * (0.19f + 0.15f * SkyGeometry.Seeded(s + 2));
        float climb = h * 0.08f * (SkyGeometry.Seeded(s + 3) - 0.5f);
        int count = Math.Min(6, 3 + (int)(SkyGeometry.Seeded(s + 4) * 2f + weight * 2f));
        float span = w * 1.36f;
        float leadX = eastward ? -0.18f * w + span * flight : 1.18f * w - span * flight;
        float leadY = altitude + climb * flight - h * 0.025f * MathF.Sin(MathF.PI * flight);
        float behind = eastward ? -1f : 1f;
        var topSky = sky.Sky.Count > 0 ? sky.Sky[0] : Argb.Hex(0xFF0B1220);
        var ink = ThemeService.ToColor(Argb.Lerp(topSky, Argb.Hex(0xFF05070D), 0.70f), 0.80f);

        using var stroke = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round, LineJoin = CanvasLineJoin.Round };
        for (int i = 0; i < count; i++)
        {
            int row = (i + 1) / 2;
            float side = i % 2 == 0 ? 1f : -1f;
            float x = leadX + behind * row * 13f * u + (SkyGeometry.Seeded(s + 10 + i) - 0.5f) * 7f * u;
            float bob = animate ? MathF.Sin(t * 1.7f + i * 2.1f) * 1.6f * u : 0f;
            float y = leadY + side * row * 6.5f * u + (SkyGeometry.Seeded(s + 20 + i) - 0.5f) * 6f * u + bob;
            if (x < -12f * u || x > w + 12f * u) continue;
            float size = (1.05f - 0.07f * row) * (0.88f + 0.24f * SkyGeometry.Seeded(s + 40 + i));
            float beat;
            if (animate)
            {
                float pace = 2.1f + 0.5f * SkyGeometry.Seeded(s + 50 + i);
                float ease = 0.5f + 0.5f * MathF.Sin(t * 0.8f + i * 1.3f);
                float reach = 1f - 0.45f * ease;
                beat = EasedWings * ease + reach * MathF.Sin(MathF.Tau * (t * pace + SkyGeometry.Seeded(s + 30 + i)));
            }
            else
            {
                beat = -0.4f + 1.2f * SkyGeometry.Seeded(s + 30 + i);
            }

            float wing = 4.6f * u * size;
            float tipY = y - wing * (0.10f + 0.62f * beat);
            float midY = y - wing * (0.42f + 0.22f * beat);
            using var pb = new CanvasPathBuilder(rc);
            pb.BeginFigure(x - wing, tipY);
            pb.AddQuadraticBezier(new(x - wing * 0.45f, midY), new(x, y));
            pb.AddQuadraticBezier(new(x + wing * 0.45f, midY), new(x + wing, tipY));
            pb.EndFigure(CanvasFigureLoop.Open);
            using var bird = CanvasGeometry.CreatePath(pb);
            ds.DrawGeometry(bird, ink, 1.25f, stroke);
        }
    }

    // ---- Sun rays & meteors ----

    private const int RaySize = 256;
    private static float[]? _rayMask;
    private static CanvasBitmap? _rays;
    private static uint _raysColour;

    private static float SunLow(float progress, float span)
    {
        float fromHorizon = MathF.Min(progress - SolarArc.SunriseOnArc, SolarArc.SunsetOnArc - progress);
        return Math.Clamp(1f - MathF.Max(fromHorizon, 0f) / span, 0f, 1f);
    }

    private static void DrawSunRays(CanvasDrawingSession ds, ICanvasResourceCreator rc, Vector2 orb, SkyColors c, float progress, float w, float strength, float turn)
    {
        float low = SunLow(progress, 0.12f);
        float reach = 110f + MathF.Max(w * 0.50f - 110f, 0f) * low;
        var col = ThemeService.ToColor(Argb.Lerp(c.OrbGlow, Argb.Hex(0xFFFFB070), low));
        float alpha = Math.Clamp((0.045f + 0.02f * low) * strength, 0f, 1f);
        var rays = RayPicture(rc, col);
        var saved = ds.Transform;
        ds.Transform = Matrix3x2.CreateRotation(turn * MathF.PI / 180f, orb) * saved;
        ds.DrawImage(rays, new Rect(orb.X - reach, orb.Y - reach, reach * 2f, reach * 2f), rays.Bounds, alpha, CanvasImageInterpolation.Linear);
        ds.Transform = saved;
    }

    private static CanvasBitmap RayPicture(ICanvasResourceCreator rc, Color colour)
    {
        uint key = (uint)(colour.R << 16 | colour.G << 8 | colour.B);
        if (_rays != null && _raysColour == key && ReferenceEquals(_rays.Device, rc.Device)) return _rays;
        var mask = _rayMask ??= RayMask();
        var bytes = new byte[RaySize * RaySize * 4];
        for (int i = 0; i < mask.Length; i++)
        {
            float a = mask[i];
            if (a <= 0f) continue;
            bytes[i * 4] = (byte)(colour.B * a);
            bytes[i * 4 + 1] = (byte)(colour.G * a);
            bytes[i * 4 + 2] = (byte)(colour.R * a);
            bytes[i * 4 + 3] = (byte)(255 * a);
        }
        _rays?.Dispose();
        _rays = CanvasBitmap.CreateFromBytes(rc, bytes, RaySize, RaySize, DirectXPixelFormat.B8G8R8A8UIntNormalized, 96f, CanvasAlphaMode.Premultiplied);
        _raysColour = key;
        return _rays;
    }

    private static float[] RayMask()
    {
        const int count = 12;
        float half = RaySize * 0.5f;
        var mask = new float[RaySize * RaySize];
        for (int j = 0; j < RaySize; j++)
        {
            for (int i = 0; i < RaySize; i++)
            {
                float dx = i + 0.5f - half, dy = j + 0.5f - half;
                float r = MathF.Sqrt(dx * dx + dy * dy) / half;
                if (r >= 1f) continue;
                float along = r < 0.6f ? 1f - 0.7f * r / 0.6f : 0.3f * (1f - (r - 0.6f) / 0.4f);
                float angle = MathF.Atan2(dy, dx);
                float across = 0f;
                for (int k = 0; k < count; k++)
                {
                    float d = MathF.Abs(angle - k * MathF.Tau / count);
                    if (d > MathF.PI) d = MathF.Tau - d;
                    float width = 0.8f * (0.035f + 0.015f * (k * 7 % 3));
                    float q = d / width;
                    across = MathF.Max(across, MathF.Exp(-q * q));
                }
                mask[j * RaySize + i] = along * across;
            }
        }
        return mask;
    }

    private static float MeteorProgress(float t, float weight)
    {
        if (weight < 0.05f || t < FirstMeteor) return -1f;
        float local = t - FirstMeteor;
        int cycle = (int)MathF.Floor(local / MeteorCycle);
        if (cycle > 0 && SkyGeometry.Seeded(cycle * 13 + 5) > 0.2f + 0.6f * weight) return -1f;
        float start = cycle == 0 ? 0f : SkyGeometry.Seeded(cycle * 13 + 9) * (MeteorCycle - MeteorSeconds);
        float p = (local - cycle * MeteorCycle - start) / MeteorSeconds;
        return p is >= 0f and <= 1f ? p : -1f;
    }

    private static void DrawMeteor(CanvasDrawingSession ds, ICanvasResourceCreator rc, float t, float p, float w, float h, float horizonY)
    {
        int s = (int)MathF.Floor((t - FirstMeteor) / MeteorCycle) * 13;
        float angle = (16f + 22f * SkyGeometry.Seeded(s + 2)) * MathF.PI / 180f;
        var dir = new Vector2(MathF.Cos(angle) * (SkyGeometry.Seeded(s + 1) > 0.5f ? -1f : 1f), MathF.Sin(angle));
        var start = new Vector2(w * (0.18f + 0.64f * SkyGeometry.Seeded(s + 3)), h * (0.05f + 0.20f * SkyGeometry.Seeded(s + 4)));
        float travel = w * (0.24f + 0.10f * SkyGeometry.Seeded(s + 6));
        float eased = 1f - (1f - p) * (1f - p);
        var head = start + dir * travel * eased;
        if (head.Y > horizonY * 0.8f) return;
        float glow = Math.Clamp(MathF.Sin(MathF.PI * p), 0f, 1f);
        var end = head - dir * 64f * (0.35f + 0.65f * glow);
        using var round = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };
        using (var wide = Tail(rc, end, head, Rgba(207, 219, 255, 0.22f * glow))) ds.DrawLine(end, head, wide, 3.4f, round);
        using (var thin = Tail(rc, end, head, Rgba(255, 255, 255, 0.92f * glow))) ds.DrawLine(end, head, thin, 1.2f, round);
        Glow(ds, rc, head, 5f, Rgba(255, 255, 255, 0.55f * glow));
        ds.FillCircle(head, 1.1f, Rgba(255, 255, 255, 0.95f * glow));
    }

    private static CanvasLinearGradientBrush Tail(ICanvasResourceCreator rc, Vector2 from, Vector2 to, Color colour) =>
        new(rc, [
            new CanvasGradientStop { Position = 0f, Color = Color.FromArgb(0, colour.R, colour.G, colour.B) },
            new CanvasGradientStop { Position = 1f, Color = colour },
        ]) { StartPoint = from, EndPoint = to };

    private static void Glow(CanvasDrawingSession ds, ICanvasResourceCreator rc, Vector2 centre, float r, Color colour)
    {
        if (colour.A < 3 || r < 0.5f) return;
        using var brush = new CanvasRadialGradientBrush(rc, [
            new CanvasGradientStop { Position = 0f, Color = colour },
            new CanvasGradientStop { Position = 1f, Color = Color.FromArgb(0, colour.R, colour.G, colour.B) },
        ]) { Center = centre, RadiusX = r, RadiusY = r };
        ds.FillCircle(centre, r, brush);
    }

    private static CanvasGradientStop[] Stops(IReadOnlyList<Argb> colours) =>
        colours.Select((col, i) => new CanvasGradientStop { Position = (float)i / Math.Max(1, colours.Count - 1), Color = ThemeService.ToColor(col) }).ToArray();

    private static Color Rgba(byte r, byte g, byte b, float a) => Color.FromArgb((byte)Math.Clamp(a * 255f, 0f, 255f), r, g, b);
    private static Color WithA(Color c, float a) => Color.FromArgb((byte)Math.Clamp(a * 255f, 0f, 255f), c.R, c.G, c.B);
}
