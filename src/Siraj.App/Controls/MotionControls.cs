using System.Diagnostics;
using System.Numerics;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Services;

namespace Siraj.App.Controls;

internal sealed class SlidingPills<T> : UserControl
{
    private readonly Grid _host = new();
    private readonly Canvas _pillLayer = new() { FlowDirection = FlowDirection.LeftToRight, IsHitTestVisible = false };
    private readonly Grid _grid = new() { ColumnSpacing = 4 };
    private readonly Border _pill;
    private readonly List<(T Value, Button Button, TextBlock Label)> _items = [];
    private int _selected;
    private bool _laidOut;
    private double _lastLeft = double.NaN;
    private double _lastWidth = double.NaN;

    public SlidingPills(IList<(T Value, string Label)> options, T selected, Action<T> changed)
    {
        HorizontalAlignment = HorizontalAlignment.Left;

        _pill = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = Kit.B("SirajPrimaryBrush"),
            IsHitTestVisible = false,
        };
        ElementCompositionPreview.SetIsTranslationEnabled(_pill, true);
        _pillLayer.Children.Add(_pill);

        _host.Children.Add(_pillLayer);
        _host.Children.Add(_grid);

        var shell = new Border
        {
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(4),
            Background = Kit.B("SirajGlassPillBrush"),
            BorderBrush = Kit.B("SirajGlassBorderBrush"),
            BorderThickness = new Thickness(1),
            Child = _host,
        };

        _selected = Math.Max(0, options.ToList().FindIndex(o => EqualityComparer<T>.Default.Equals(o.Value, selected)));

        for (int i = 0; i < options.Count; i++)
        {
            int idx = i;
            var (value, text) = options[i];
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock
            {
                Text = text,
                FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
                FontSize = 13,
                FontWeight = idx == _selected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = Kit.B(idx == _selected ? "SirajOnPrimaryBrush" : "SirajOnSurfaceVariantBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
            };
            var btn = new Button
            {
                Content = label,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(15, 7, 15, 7),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinWidth = 0,
                UseSystemFocusVisuals = false,
            };
            btn.Resources["ButtonBackgroundPointerOver"] = Kit.Clone("SirajPrimarySoftBrush");
            btn.Resources["ButtonBackgroundPressed"] = Kit.Clone("SirajPrimarySoftBrush");
            AutomationProperties.SetName(btn, text);
            btn.Click += (_, _) =>
            {
                if (_selected == idx) return;
                Select(idx, animate: true);
                changed(value);
            };
            Grid.SetColumn(btn, i);
            _grid.Children.Add(btn);
            _items.Add((value, btn, label));
        }

        Content = shell;
        _grid.SizeChanged += (_, _) => PositionPill(animate: false);
        _grid.LayoutUpdated += (_, _) => PositionPill(animate: false, onlyIfChanged: true);
    }

    public void Select(int index, bool animate)
    {
        if (index < 0 || index >= _items.Count) return;
        _selected = index;
        for (int i = 0; i < _items.Count; i++)
        {
            bool active = i == _selected;
            _items[i].Label.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            _items[i].Label.Foreground = Kit.B(active ? "SirajOnPrimaryBrush" : "SirajOnSurfaceVariantBrush");
        }
        PositionPill(animate && _laidOut && Kit.Motion);
    }

    private void PositionPill(bool animate, bool onlyIfChanged = false)
    {
        if (_items.Count == 0 || _grid.ActualWidth <= 0 || _pillLayer.ActualWidth <= 0) return;
        var activeBtn = _items[_selected].Button;
        if (activeBtn.ActualWidth <= 0 || activeBtn.ActualHeight <= 0) return;

        try
        {
            var transform = activeBtn.TransformToVisual(_pillLayer);
            var p0 = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
            var p1 = transform.TransformPoint(new Windows.Foundation.Point(activeBtn.ActualWidth, activeBtn.ActualHeight));
            double left = Math.Min(p0.X, p1.X);
            double top = Math.Min(p0.Y, p1.Y);
            double w = Math.Abs(p1.X - p0.X);
            double h = Math.Abs(p1.Y - p0.Y);
            if (w <= 0 || h <= 0) return;
            if (onlyIfChanged && Math.Abs(left - _lastLeft) < 0.5 && Math.Abs(w - _lastWidth) < 0.5) return;

            _lastLeft = left;
            _lastWidth = w;
            _pill.Width = w;
            _pill.Height = h;
            Canvas.SetLeft(_pill, 0);
            Canvas.SetTop(_pill, top);

            var visual = ElementCompositionPreview.GetElementVisual(_pill);
            if (!animate)
            {
                visual.StopAnimation("Translation");
                visual.Properties.InsertVector3("Translation", new Vector3((float)left, 0, 0));
            }
            else
            {
                Kit.SpringTo(_pill, new Vector3((float)left, 0, 0), damping: 0.90f, periodMs: 120);
            }
            _laidOut = true;
        }
        catch
        {
            // Control may be transiently detached during page teardown.
        }
    }
}

internal sealed class EqualizerBars : UserControl
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private readonly Border[] _bars = new Border[3];
    private DispatcherQueueTimer? _timer;
    private bool _active;

    public bool IsActive
    {
        get => _active;
        set { _active = value; UpdateState(); }
    }

    public EqualizerBars(double height = 16, Brush? brush = null)
    {
        Height = height;
        Width = height * 1.05;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2.5, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center };
        var fg = brush ?? Kit.B("SirajPrimaryBrush");
        for (int i = 0; i < 3; i++)
        {
            _bars[i] = new Border { Width = 3, Height = height * 0.45, CornerRadius = new CornerRadius(1.5), Background = fg, VerticalAlignment = VerticalAlignment.Bottom };
            row.Children.Add(_bars[i]);
        }
        Content = row;
        Loaded += (_, _) =>
        {
            _timer = DispatcherQueue.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(55);
            _timer.Tick += (_, _) => Step();
            UpdateState();
        };
        Unloaded += (_, _) => { _timer?.Stop(); _timer = null; };
    }

    private void UpdateState()
    {
        if (_timer == null) return;
        if (_active && Kit.Motion) _timer.Start();
        else
        {
            _timer.Stop();
            double h = Height;
            _bars[0].Height = h * 0.45; _bars[1].Height = h * 0.80; _bars[2].Height = h * 0.55;
        }
    }

    private void Step()
    {
        double t = Clock.Elapsed.TotalSeconds;
        double h = Height;
        _bars[0].Height = h * (0.28 + 0.68 * (0.5 + 0.5 * Math.Sin(t * 7.2)));
        _bars[1].Height = h * (0.28 + 0.72 * (0.5 + 0.5 * Math.Sin(t * 8.6 + 1.8)));
        _bars[2].Height = h * (0.28 + 0.66 * (0.5 + 0.5 * Math.Sin(t * 6.4 + 3.4)));
    }
}

internal sealed class WavyProgressBar : UserControl
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private readonly CanvasControl _canvas = new() { Height = 28 };
    private DispatcherQueueTimer? _timer;
    private bool _dragging;
    private double _fraction;
    private bool _playing;

    public event Action<double>? SeekRequested;

    public double Fraction
    {
        get => _fraction;
        set { if (!_dragging) { _fraction = Math.Clamp(value, 0, 1); _canvas.Invalidate(); } }
    }

    public bool IsPlaying
    {
        get => _playing;
        set { _playing = value; UpdateTimer(); _canvas.Invalidate(); }
    }

    public WavyProgressBar()
    {
        Height = 28;
        FlowDirection = FlowDirection.LeftToRight;
        Content = _canvas;
        _canvas.Draw += OnDraw;
        PointerPressed += OnDown;
        PointerMoved += OnMove;
        PointerReleased += OnUp;
        PointerCanceled += OnUp;
        Loaded += (_, _) =>
        {
            _timer = DispatcherQueue.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(38);
            _timer.Tick += (_, _) => _canvas.Invalidate();
            UpdateTimer();
        };
        Unloaded += (_, _) => { _timer?.Stop(); _timer = null; _canvas.RemoveFromVisualTree(); };
    }

    private void UpdateTimer()
    {
        if (_timer == null) return;
        if (_playing && Kit.Motion) _timer.Start(); else _timer.Stop();
    }

    private void UpdateFromPointer(PointerRoutedEventArgs e)
    {
        if (ActualWidth <= 16) return;
        var pt = e.GetCurrentPoint(this).Position;
        _fraction = Math.Clamp((pt.X - 8) / (ActualWidth - 16), 0, 1);
        _canvas.Invalidate();
    }

    private void OnDown(object sender, PointerRoutedEventArgs e)
    {
        _dragging = true;
        CapturePointer(e.Pointer);
        UpdateFromPointer(e);
    }

    private void OnMove(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging) UpdateFromPointer(e);
    }

    private void OnUp(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleasePointerCapture(e.Pointer);
        SeekRequested?.Invoke(_fraction);
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        float w = (float)sender.ActualWidth, h = (float)sender.ActualHeight;
        if (w < 20 || h < 4) return;
        var ds = args.DrawingSession;
        float pad = 8f, cy = h / 2f, usable = w - pad * 2f;
        float splitX = pad + usable * (float)_fraction;

        var primary = ThemeService.ToColor(App.Theme.Scheme.Primary);
        var track = ThemeService.ToColor(App.Theme.Scheme.OnSurfaceVariant, 0.24f);
        using var style = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round, LineJoin = CanvasLineJoin.Round };

        if (splitX < w - pad)
            ds.DrawLine(splitX + 6f, cy, w - pad, cy, track, 3.2f, style);

        if (splitX > pad + 2f)
        {
            float amp = _playing && Kit.Motion ? 3.2f : 0f;
            float phase = (float)Clock.Elapsed.TotalSeconds * 5.2f;
            using var pb = new CanvasPathBuilder(sender);
            pb.BeginFigure(pad, cy);
            for (float x = pad; x <= splitX; x += 2.5f)
            {
                float fade = Math.Clamp(MathF.Min((x - pad) / 10f, (splitX - x) / 10f), 0f, 1f);
                float y = cy + amp * fade * MathF.Sin((x - pad) * 0.22f - phase);
                pb.AddLine(x, y);
            }
            pb.EndFigure(CanvasFigureLoop.Open);
            using var geo = CanvasGeometry.CreatePath(pb);
            ds.DrawGeometry(geo, primary, 3.6f, style);
        }

        ds.FillCircle(splitX, cy, _dragging ? 7.5f : 6f, primary);
        ds.FillCircle(splitX, cy, 2.5f, ThemeService.ToColor(App.Theme.Scheme.OnPrimary));
    }
}
