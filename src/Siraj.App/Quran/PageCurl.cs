using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Windows.Foundation;
using Windows.UI;

namespace Siraj.App.Quran;

internal sealed class PageCurl
{
    public Rect Leaf { get; init; }

    public bool Single { get; init; }

    public CanvasBitmap? Front { get; init; }
    public CanvasBitmap? Back { get; init; }

    public CanvasBitmap? Under { get; init; }
    public CanvasBitmap? Landing { get; init; }

    public bool IsDarkPaper { get; init; }

    public double Progress { get; set; }

    private Rect LandingRect => new(Leaf.Right, Leaf.Y, Leaf.Width, Leaf.Height);

    public void Draw(CanvasDrawingSession ds)
    {
        float t = (float)Math.Clamp(Progress, 0, 1);
        float spine = (float)Leaf.Right, top = (float)Leaf.Y, w = (float)Leaf.Width, h = (float)Leaf.Height;
        float midY = top + h / 2;

        if (Under != null) ds.DrawImage(Under, Leaf);
        if (Landing != null && !Single) ds.DrawImage(Landing, LandingRect);
        if (t <= 0.0005f)
        {
            if (Front != null) ds.DrawImage(Front, Leaf);
            return;
        }

        // Slice the turning page into vertical strips with perspective projection.
        int n = Math.Clamp((int)(w / 4), 24, 180);
        float du = w / n;
        float turn = MathF.PI * t;
        float bend = 1.15f * MathF.Sin(MathF.PI * t);
        float eye = 5.5f * h;
        var xs = new float[n + 1];
        var zs = new float[n + 1];
        var angles = new float[n];
        for (int i = 0; i < n; i++)
        {
            float u = (i + 0.5f) / n;
            float a = Math.Clamp(turn + bend * MathF.Pow(u, 1.7f), 0, MathF.PI);
            angles[i] = a;
            xs[i + 1] = xs[i] + MathF.Cos(a) * du;
            zs[i + 1] = zs[i] + MathF.Sin(a) * du;
        }
        float Screen(int i) => spine - xs[i] * eye / (eye - zs[i]);
        float Scale(int i) => eye / (eye - zs[i]);

        // Fade out single-page turns near completion.
        float alpha = Single ? Math.Clamp((0.86f - t) / 0.3f, 0, 1) : 1;

        DrawCastShadow(ds, t, Screen, n, spine, top, h);

        var order = Enumerable.Range(0, n).OrderBy(i => zs[i] + zs[i + 1]).ToArray();
        foreach (int i in order)
        {
            float x0 = Screen(i), x1 = Screen(i + 1);
            float s = (Scale(i) + Scale(i + 1)) / 2;
            float left = Math.Min(x0, x1), right = Math.Max(x0, x1);
            if (right - left < 0.05f) continue;
            // Overlap strips slightly to avoid subpixel seams.
            var dest = new Rect(left - 0.6, midY - h / 2 * s, right - left + 1.2, h * s);
            bool front = x1 < x0;
            float u0 = i * du, u1 = (i + 1) * du;
            var face = front ? Front : Back;
            if (face != null)
            {
                // Front maps from the right spine; back maps from the left spine.
                var source = front ? new Rect(w - u1, 0, du, h) : new Rect(u0, 0, du, h);
                ds.DrawImage(face, dest, Scaled(source, face, w, h), alpha, CanvasImageInterpolation.Linear);
            }

            // Shade strips by tilt angle and add a highlight near the curl crest.
            float facing = MathF.Abs(MathF.Cos(angles[i]));
            float dark = (1 - facing) * (IsDarkPaper ? 0.55f : 0.42f);
            if (dark > 0.01f) ds.FillRectangle(dest, Color.FromArgb((byte)(255 * dark * alpha), 0, 0, 0));
            float crest = front ? angles[i] - 0.62f : (MathF.PI - angles[i]) - 0.62f;
            float sheen = 0.16f * MathF.Exp(-crest * crest / 0.06f) * MathF.Sin(MathF.PI * t);
            if (sheen > 0.01f && !IsDarkPaper) ds.FillRectangle(dest, Color.FromArgb((byte)(255 * sheen * alpha), 255, 255, 255));
        }

        float edgeX = Screen(n), edgeS = Scale(n);
        ds.DrawLine(edgeX, midY - h / 2 * edgeS, edgeX, midY + h / 2 * edgeS, Color.FromArgb((byte)(40 * alpha), 0, 0, 0), 0.8f);
    }

    private void DrawCastShadow(CanvasDrawingSession ds, float t, Func<int, float> screen, int n, float spine, float top, float h)
    {
        float lift = MathF.Sin(MathF.PI * t);
        if (lift < 0.01f) return;
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i <= n; i++)
        {
            float x = screen(i);
            min = Math.Min(min, x);
            max = Math.Max(max, x);
        }
        float strength = (IsDarkPaper ? 0.55f : 0.32f) * MathF.Sqrt(lift);
        float reach = 14 + (float)Leaf.Width * 0.22f * lift;
        // Cast shadow on the uncovered page, target page, and gutter.
        if (min < spine)
            Band(ds, min, min - reach, strength, Math.Max((float)Leaf.X, min - reach), min, top, h);
        if (max > spine && (Landing != null || !Single))
            Band(ds, max, max + reach, strength * (Single ? 0.6f : 1), max, Math.Min((float)LandingRect.Right, max + reach), top, h);
        float gutter = (float)Leaf.Width * 0.35f;
        Band(ds, spine, spine - gutter, strength * 0.45f, Math.Max((float)Leaf.X, spine - gutter), spine, top, h);
        if (!Single) Band(ds, spine, spine + gutter, strength * 0.45f, spine, spine + gutter, top, h);
    }

    private static void Band(CanvasDrawingSession ds, float from, float to, float strength, float left, float right, float top, float h)
    {
        if (right - left < 0.5f) return;
        var stops = new[]
        {
            new CanvasGradientStop { Position = 0, Color = Color.FromArgb((byte)(255 * Math.Clamp(strength, 0, 1)), 0, 0, 0) },
            new CanvasGradientStop { Position = 0.45f, Color = Color.FromArgb((byte)(255 * Math.Clamp(strength * 0.35f, 0, 1)), 0, 0, 0) },
            new CanvasGradientStop { Position = 1, Color = Color.FromArgb(0, 0, 0, 0) },
        };
        using var brush = new CanvasLinearGradientBrush(ds, stops) { StartPoint = new Vector2(from, 0), EndPoint = new Vector2(to, 0) };
        ds.FillRectangle(left, top, right - left, h, brush);
    }

    private static Rect Scaled(Rect r, CanvasBitmap bitmap, float w, float h)
    {
        var size = bitmap.Size;
        double sx = size.Width / w, sy = size.Height / h;
        return new Rect(Math.Max(0, r.X * sx), r.Y * sy, Math.Max(0.01, r.Width * sx), r.Height * sy);
    }

    public static CanvasRenderTarget Verso(CanvasBitmap front, Color paper, float dpi)
    {
        var size = front.Size;
        var verso = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), (float)size.Width, (float)size.Height, dpi);
        using var ds = verso.CreateDrawingSession();
        ds.Clear(Color.FromArgb(0, 0, 0, 0));
        ds.Transform = Matrix3x2.CreateScale(-1, 1) * Matrix3x2.CreateTranslation((float)size.Width, 0);
        ds.DrawImage(front);
        ds.Transform = Matrix3x2.Identity;
        ds.FillRoundedRectangle(new Rect(0.5, 0.5, size.Width - 1, size.Height - 1), 6, 6, Color.FromArgb(232, paper.R, paper.G, paper.B));
        return verso;
    }
}
