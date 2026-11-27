using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Services;
using Siraj.Core.Quran;
using Windows.Foundation;
using Windows.UI;

namespace Siraj.App.Quran;

internal enum PageSide { Single, Left, Right }

internal sealed class MushafPageView : UserControl
{
    // Margin around the sheet for shadow and page stack; none on the spine edge.
    private const float Outer = 10, Top = 4, Bottom = 14;
    private const float HeadBand = 30, FootBand = 28, TextSide = 6;

    private static readonly Dictionary<int, Task<PageLayout?>> Layouts = new();
    private static readonly LinkedList<int> LayoutOrder = new();
    private static CanvasTextFormat? _captionLeft, _captionRight, _captionCenter;

    private readonly CanvasControl _canvas;
    private int _page;
    private int _request;
    private PageLayout? _layout;
    private PlacedPage? _placed;
    private Size _placedFor;
    private string _header = "", _juz = "", _footer = "";
    private (int Surah, int Ayah)? _playing, _selected;
    private MushafColors _colors = MushafColors.For("Light", "Creamy", false, false, Microsoft.UI.Colors.Green);
    private PageSide _side = PageSide.Single;

    public event Action<(int Surah, int Ayah)>? AyahClicked;

    public event Action? Drawn;

    public int PageNumber => _page;

    public bool IsReady => _layout != null;

    public PageSide Side
    {
        get => _side;
        set
        {
            if (_side == value) return;
            _side = value;
            _placed = null;
            _canvas.Invalidate();
        }
    }

    public static (double Width, double Height) Frame(double sheetWidth, double sheetHeight, PageSide side) =>
        (sheetWidth + (side == PageSide.Single ? 2 * Outer : Outer), sheetHeight + Top + Bottom);

    public MushafPageView()
    {
        _canvas = new CanvasControl { ClearColor = Microsoft.UI.Colors.Transparent, FlowDirection = FlowDirection.LeftToRight };
        _canvas.Draw += OnDraw;
        _canvas.SizeChanged += (_, _) =>
        {
            _placed = null;
            _canvas.Invalidate();
        };
        // Transparent grid captures taps because CanvasControl has no hit-testable background.
        var host = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), FlowDirection = FlowDirection.LeftToRight };
        host.Children.Add(_canvas);
        host.Tapped += OnTapped;
        Content = host;
        Unloaded += (_, _) => _canvas.RemoveFromVisualTree();
    }

    public void SetColors(MushafColors colors)
    {
        _colors = colors;
        _canvas.Invalidate();
    }

    public void SetHighlights((int, int)? playing, (int, int)? selected)
    {
        if (_playing == playing && _selected == selected) return;
        _playing = playing;
        _selected = selected;
        _canvas.Invalidate();
    }

    public bool Shows((int Surah, int Ayah) ayah) => _layout?.Page.Ayahs().Contains(ayah) ?? false;

    public static void Prefetch(int page)
    {
        if (page >= 1 && page <= QuranDatabase.PageCount) _ = LayoutOf(page);
    }

    private static Task<PageLayout?> LayoutOf(int page)
    {
        lock (Layouts)
        {
            if (Layouts.TryGetValue(page, out var kept))
            {
                LayoutOrder.Remove(page);
                LayoutOrder.AddFirst(page);
                return kept;
            }
            var task = Task.Run(() =>
            {
                try
                {
                    return MushafRenderer.Build(App.Quran.Database.Page(page), App.Quran.Glyphs, App.Quran.Assets);
                }
                catch (Exception e)
                {
                    Log.Error($"building page {page}", e);
                    return null;
                }
            });
            Layouts[page] = task;
            LayoutOrder.AddFirst(page);
            // Cache pages around the current spread; MushafRenderer keeps their fonts loaded.
            while (LayoutOrder.Count > MushafRenderer.KeptPages - 2)
            {
                Layouts.Remove(LayoutOrder.Last!.Value);
                LayoutOrder.RemoveLast();
            }
            return task;
        }
    }

    public void Show(int page) => _ = ShowAsync(page);

    public Task ShowAsync(int page)
    {
        if (page == _page && _layout != null) return Task.CompletedTask;
        _page = page;
        int request = ++_request;
        var loc = Loc.Current;
        _footer = loc.Number(page);
        _juz = loc.Format("quran_juz_label", loc.Number(QuranDatabase.JuzOfPage(page)));
        var task = LayoutOf(page);
        if (task.IsCompleted)
        {
            Apply(task.Result);
            return Task.CompletedTask;
        }
        _layout = null;
        _placed = null;
        _header = "";
        _canvas.Invalidate();
        return Finish(task, request);
    }

    private async Task Finish(Task<PageLayout?> task, int request)
    {
        var layout = await task;
        if (request == _request) Apply(layout);
    }

    private void Apply(PageLayout? layout)
    {
        _layout = layout;
        _placed = null;
        var surah = layout?.Page.Lines.SelectMany(l => l.Words).FirstOrDefault(w => w.Surah != null)?.Surah
                    ?? layout?.Page.Lines.FirstOrDefault(l => l.Surah != null)?.Surah;
        _header = surah != null ? App.Quran.Database.Surah(surah.Value).Name : "";
        AutomationProperties.SetName(this, string.Join(" ", layout?.Page.Lines.SelectMany(l => l.Words).Select(w => w.Text) ?? []));
        _canvas.Invalidate();
    }

    public Rect Sheet
    {
        get
        {
            double w = ActualWidth, h = ActualHeight;
            double left = _side == PageSide.Right ? 0 : Outer;
            double right = _side == PageSide.Left ? 0 : Outer;
            return new Rect(left, Top, Math.Max(1, w - left - right), Math.Max(1, h - Top - Bottom));
        }
    }

    public CanvasRenderTarget? Snapshot(float dpi)
    {
        var sheet = Sheet;
        if (sheet.Width < 10 || sheet.Height < 10 || ActualWidth < 10) return null;
        var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), (float)sheet.Width, (float)sheet.Height, dpi);
        using var ds = target.CreateDrawingSession();
        ds.Clear(Color.FromArgb(0, 0, 0, 0));
        ds.Transform = Matrix3x2.CreateTranslation(-(float)sheet.X, -(float)sheet.Y);
        DrawSheet(ds, sheet);
        return target;
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        var sheet = Sheet;
        if (sheet.Width < 10 || sheet.Height < 10) return;
        using var shape = SheetShape(ds, sheet);
        DrawStack(ds, sheet);
        using (var mask = new CanvasCommandList(ds))
        {
            using (var m = mask.CreateDrawingSession()) m.FillGeometry(shape, Microsoft.UI.Colors.Black);
            using var shadow = new ShadowEffect { Source = mask, BlurAmount = 7, ShadowColor = Color.FromArgb((byte)(_colors.IsDark ? 150 : 70), 0, 0, 0) };
            ds.DrawImage(shadow, 0, 3);
        }
        DrawSheet(ds, sheet);
        Drawn?.Invoke();
    }

    private CanvasGeometry SheetShape(ICanvasResourceCreator rc, Rect r)
    {
        float x = (float)r.X, y = (float)r.Y, w = (float)r.Width, h = (float)r.Height;
        float rl = _side == PageSide.Right ? 1.5f : 7f, rr = _side == PageSide.Left ? 1.5f : 7f;
        using var b = new CanvasPathBuilder(rc);
        b.BeginFigure(x + rl, y);
        b.AddLine(x + w - rr, y);
        b.AddArc(new Vector2(x + w, y + rr), rr, rr, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
        b.AddLine(x + w, y + h - rr);
        b.AddArc(new Vector2(x + w - rr, y + h), rr, rr, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
        b.AddLine(x + rl, y + h);
        b.AddArc(new Vector2(x, y + h - rl), rl, rl, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
        b.AddLine(x, y + rl);
        b.AddArc(new Vector2(x + rl, y), rl, rl, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
        b.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(b);
    }

    private void DrawStack(CanvasDrawingSession ds, Rect sheet)
    {
        if (_page == 0) return;
        float share = _side == PageSide.Right ? _page / (float)QuranDatabase.PageCount : (QuranDatabase.PageCount - _page) / (float)QuranDatabase.PageCount;
        int leaves = (int)Math.Round(1 + 3 * share);
        var edge = Shade(_colors.Paper, _colors.IsDark ? 1.25f : 0.9f);
        var line = Color.FromArgb((byte)(_colors.IsDark ? 70 : 45), _colors.Ink.R, _colors.Ink.G, _colors.Ink.B);
        for (int i = leaves; i >= 1; i--)
        {
            float dx = (_side == PageSide.Right ? 1 : -1) * 2.2f * i;
            var r = new Rect(sheet.X + dx, sheet.Y + 0.9 * i, sheet.Width, sheet.Height - 0.9 * i);
            using var g = SheetShape(ds, r);
            ds.FillGeometry(g, edge);
            ds.DrawGeometry(g, line, 0.6f);
        }
    }

    private void DrawSheet(CanvasDrawingSession ds, Rect sheet)
    {
        var c = _colors;
        using var shape = SheetShape(ds, sheet);
        ds.FillGeometry(shape, c.Paper);
        float x = (float)sheet.X, y = (float)sheet.Y, w = (float)sheet.Width, h = (float)sheet.Height;

        // Gutter shadow along the spine and highlight along the outer edge.
        {
            bool spineLeft = _side == PageSide.Right;
            float depth = w * 0.11f;
            float strength = c.IsDark ? 0.42f : 0.17f;
            var stops = new[]
            {
                new CanvasGradientStop { Position = 0, Color = Color.FromArgb((byte)(255 * strength), 0, 0, 0) },
                new CanvasGradientStop { Position = 0.28f, Color = Color.FromArgb((byte)(255 * strength * 0.35f), 0, 0, 0) },
                new CanvasGradientStop { Position = 1, Color = Color.FromArgb(0, 0, 0, 0) },
            };
            float spineX = spineLeft ? x : x + w;
            float inner = spineLeft ? x + depth : x + w - depth;
            using var gutter = new CanvasLinearGradientBrush(ds, stops) { StartPoint = new Vector2(spineX, 0), EndPoint = new Vector2(inner, 0) };
            ds.FillRectangle(spineLeft ? x : x + w - depth, y, depth, h, gutter);
            float lip = Math.Min(14, w * 0.03f);
            var lipStops = new[]
            {
                new CanvasGradientStop { Position = 0, Color = Color.FromArgb((byte)(c.IsDark ? 18 : 40), 255, 255, 255) },
                new CanvasGradientStop { Position = 1, Color = Color.FromArgb(0, 255, 255, 255) },
            };
            float outerX = spineLeft ? x + w : x;
            using var light = new CanvasLinearGradientBrush(ds, lipStops) { StartPoint = new Vector2(outerX, 0), EndPoint = new Vector2(spineLeft ? outerX - lip : outerX + lip, 0) };
            ds.FillRectangle(spineLeft ? outerX - lip : outerX, y, lip, h, light);
        }
        ds.DrawGeometry(shape, Color.FromArgb(40, c.Ink.R, c.Ink.G, c.Ink.B), 1);

        var caption = Color.FromArgb(191, c.Ink.R, c.Ink.G, c.Ink.B);
        Formats();
        float band = w - 36;
        if (band > 20)
        {
            using (var juz = new CanvasTextLayout(ds, _juz, _captionLeft, band, 20)) ds.DrawTextLayout(juz, x + 18, y + 9, caption);
            using (var head = new CanvasTextLayout(ds, _header, _captionRight, band, 20)) ds.DrawTextLayout(head, x + 18, y + 9, caption);
            using var foot = new CanvasTextLayout(ds, _footer, _captionCenter, w, 20);
            ds.DrawTextLayout(foot, x, y + h - FootBand + 6, caption);
        }

        if (_layout == null) return;
        var text = TextArea(sheet);
        if (text.Width < 10 || text.Height < 10) return;
        if (_placed == null || _placedFor != new Size(text.Width, text.Height))
        {
            _placed = MushafRenderer.Place(_layout, App.Quran.Assets, (float)text.Width, (float)text.Height);
            _placedFor = new Size(text.Width, text.Height);
        }
        var saved = ds.Transform;
        ds.Transform = Matrix3x2.CreateTranslation((float)text.X, (float)text.Y) * saved;
        MushafRenderer.Draw(ds, _placed, _layout, c, _playing, _selected);
        ds.Transform = saved;
    }

    private static Rect TextArea(Rect sheet) =>
        new(sheet.X + TextSide, sheet.Y + HeadBand, Math.Max(0, sheet.Width - 2 * TextSide), Math.Max(0, sheet.Height - HeadBand - FootBand));

    private static void Formats()
    {
        if (_captionLeft != null) return;
        // Use bundled Outfit font; Arabic falls back to the system font.
        string family = AppPaths.Asset("Fonts", "Outfit.ttf") + "#Outfit";
        CanvasTextFormat Make(CanvasHorizontalAlignment align) => new()
        {
            FontFamily = family, FontSize = 12, HorizontalAlignment = align, WordWrapping = CanvasWordWrapping.NoWrap,
        };
        _captionLeft = Make(CanvasHorizontalAlignment.Left);
        _captionRight = Make(CanvasHorizontalAlignment.Right);
        _captionCenter = Make(CanvasHorizontalAlignment.Center);
    }

    private static Color Shade(Color c, float by) => Color.FromArgb(255,
        (byte)Math.Clamp(c.R * by, 0, 255), (byte)Math.Clamp(c.G * by, 0, 255), (byte)Math.Clamp(c.B * by, 0, 255));

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_placed == null) return;
        var p = e.GetPosition(_canvas);
        var text = TextArea(Sheet);
        if (MushafRenderer.HitTest(_placed, (float)(p.X - text.X), (float)(p.Y - text.Y)) is { } ayah) AyahClicked?.Invoke(ayah);
    }
}
