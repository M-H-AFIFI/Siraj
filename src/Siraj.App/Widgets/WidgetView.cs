using System.Numerics;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.App.Sky;
using Siraj.Core.Hijri;
using Siraj.Core.Prayer;
using Siraj.Core.Settings;
using Siraj.Core.Sky;
using Siraj.Core.Text;
using Siraj.Core.Theme;
using Windows.UI;

namespace Siraj.App.Widgets;

internal sealed record WidgetFace(string Id, string TitleKey, string DescriptionKey, double Width, double Height, bool Seconds, string Dress);

internal static class WidgetFaces
{
    public static readonly WidgetFace[] All =
    [
        new("next", "widget_next_name", "widget_next_description", 264, 152, true, "glass"),
        new("list", "widget_list_name", "widget_list_description", 252, 320, false, "glass"),
        new("dual", "widget_dual_name", "widget_dual_description", 348, 150, true, "filled"),
        new("ring", "widget_ring_name", "widget_ring_description", 204, 204, true, "soft"),
        new("clock", "widget_clock_name", "widget_clock_description", 340, 188, false, "none"),
        new("day", "widget_day_name", "widget_day_description", 440, 184, false, "glass"),
        new("hijri", "widget_hijri_name", "widget_hijri_description", 300, 184, false, "filled"),
        new("moon", "widget_moon_name", "widget_moon_description", 272, 180, false, "glass"),
    ];

    public static WidgetFace Find(string id) => All.FirstOrDefault(f => f.Id == id) ?? All[0];
}

internal sealed class WidgetView : UserControl
{
    private WidgetConfig _config;
    private readonly bool _live;
    private Action? _update;
    private IDisposable? _seconds;
    private Inks _ink = Inks.Plain;
    private double _s = 1;

    private static readonly FontFamily Amiri = new("ms-appx:///Assets/Quran/quran_amiri.ttf#Amiri");
    private static readonly FontFamily Numerals = new("Segoe UI Variable Display, Segoe UI");

    public WidgetFace Face { get; private set; }

    public WidgetView(WidgetConfig config, bool live)
    {
        _config = config;
        _live = live;
        Face = WidgetFaces.Find(config.Face);
        Loaded += (_, _) =>
        {
            App.Clock.Tick += Tick;
            App.Clock.DayChanged += Rebuild;
            App.Theme.Changed += Rebuild;
            Rebuild();
            UpdateCadence();
        };
        Unloaded += (_, _) =>
        {
            App.Clock.Tick -= Tick;
            App.Clock.DayChanged -= Rebuild;
            App.Theme.Changed -= Rebuild;
            _seconds?.Dispose();
            _seconds = null;
        };
    }

    public void Apply(WidgetConfig config)
    {
        bool secondsChanged = WidgetFaces.Find(config.Face).Seconds != Face.Seconds;
        _config = config;
        Face = WidgetFaces.Find(config.Face);
        Rebuild();
        if (secondsChanged && IsLoaded) UpdateCadence();
    }

    private long _minute = -1;

    private void Tick()
    {
        long minute = App.Clock.Now.ToUnixTimeSeconds() / 60;
        bool seconds = WantsSeconds;
        if (!seconds && minute == _minute) return;
        _minute = minute;
        _update?.Invoke();
        UpdateCadence();
    }

    private bool WantsSeconds => Face.Seconds && App.Clock.State switch
    {
        PrayerState.UpcomingAzan u => u.Remaining <= SecondsWindow,
        null => false,
        _ => true,
    };

    private static readonly TimeSpan SecondsWindow = TimeSpan.FromMinutes(10);

    private void UpdateCadence()
    {
        bool want = WantsSeconds;
        if (want && _seconds == null) _seconds = App.Clock.SubscribeSeconds();
        else if (!want && _seconds != null)
        {
            _seconds.Dispose();
            _seconds = null;
        }
    }

    private sealed record Inks(Brush Ink, Brush Muted, Brush Accent, Brush Pill, Brush OnPill, Brush Track, Brush Tile, Color InkColor, bool Shadows)
    {
        public static readonly Inks Plain = Make(Colors.White, 0.8f, Colors.White, Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF), Colors.White, true);

        public static Inks Make(Color ink, float muted, Color accent, Color pill, Color onPill, bool shadows) => new(
            new SolidColorBrush(ink), new SolidColorBrush(Alpha(ink, muted)), new SolidColorBrush(accent),
            new SolidColorBrush(pill), new SolidColorBrush(onPill), new SolidColorBrush(Alpha(ink, 0.18f)),
            new SolidColorBrush(Alpha(ink, 0.07f)), ink, shadows);
    }

    private static Color Alpha(Color c, float a) => Color.FromArgb((byte)Math.Round(c.A * Math.Clamp(a, 0, 1)), c.R, c.G, c.B);

    private static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private static Brush Diagonal(params (Color Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1) };
        foreach (var (color, offset) in stops) brush.GradientStops.Add(new GradientStop { Color = color, Offset = offset });
        return brush;
    }

    private static Brush Sheen(float strength) => Diagonal(
        (Alpha(Colors.White, strength), 0), (Alpha(Colors.White, strength * 0.18f), 0.42), (Alpha(Colors.White, 0), 1));

    private (Brush? Card, Brush? Sheen, Brush? Rim, Color Motif) Dress()
    {
        bool dark = App.Theme.IsDark;
        var scheme = _config.Palette == "App" ? App.Theme.Scheme : Palettes.Scheme(_config.Palette, dark, false);
        float alpha = Math.Clamp(_config.Opacity, 0, 100) / 100f;
        Color C(Argb argb, float a = 1f) => ThemeService.ToColor(argb, a);
        var primary = C(scheme.Primary);
        var onPrimary = C(scheme.OnPrimary);
        switch (_config.Background)
        {
            case "filled":
            {
                _ink = Inks.Make(onPrimary, 0.8f, onPrimary, Alpha(onPrimary, 0.2f), onPrimary, false);
                var top = C(scheme.Primary, alpha);
                var bottom = Mix(top, Color.FromArgb(top.A, 0, 0, 0), 0.22f);
                return (Diagonal((top, 0), (bottom, 1)), Sheen(0.22f), new SolidColorBrush(Alpha(Colors.White, 0.2f)), Alpha(onPrimary, 0.09f));
            }
            case "soft":
            {
                var ink = C(scheme.OnPrimaryContainer);
                _ink = Inks.Make(ink, 0.74f, primary, primary, onPrimary, false);
                var top = C(scheme.PrimaryContainer, alpha);
                var bottom = Mix(top, Color.FromArgb(top.A, primary.R, primary.G, primary.B), 0.12f);
                return (Diagonal((top, 0), (bottom, 1)), Sheen(dark ? 0.1f : 0.4f), new SolidColorBrush(Alpha(ink, 0.1f)), Alpha(primary, 0.12f));
            }
            case "none":
                _ink = Inks.Plain;
                return (null, null, null, Colors.Transparent);
            default: // glass: the window's acrylic shows through a light tint of the palette's surface
            {
                var ink = C(scheme.OnSurface);
                _ink = Inks.Make(ink, 0.7f, primary, primary, onPrimary, false);
                var tint = C(scheme.Surface, alpha * 0.45f);
                Brush card = _live
                    ? new SolidColorBrush(tint)
                    // In the gallery there is no window acrylic: the app's own blurs the stage behind it.
                    : new AcrylicBrush { TintColor = C(scheme.Surface), TintOpacity = 0.25 + alpha * 0.4, TintLuminosityOpacity = 0.55, FallbackColor = C(scheme.Surface, 0.9f) };
                var rim = dark ? Alpha(Colors.White, 0.14f) : Alpha(Colors.Black, 0.07f);
                return (card, Sheen(dark ? 0.08f : 0.3f), new SolidColorBrush(rim), Alpha(ink, 0.06f));
            }
        }
    }

    public void Rebuild()
    {
        var loc = Loc.Current;
        var (card, sheen, rim, motif) = Dress();
        _s = _config.Scale;
        double s = _s, width = Face.Width * s, height = Face.Height * s;
        // Glass is the window's own acrylic, which Windows rounds at 8 px; the other cards follow
        double radius = _config.Background == "glass" ? (_live ? 8 : 8 * s) : 16 * s;
        var root = new Grid
        {
            Width = width, Height = height,
            CornerRadius = new CornerRadius(radius),
            Background = card,
            FlowDirection = loc.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
        };
        if (rim != null)
        {
            root.BorderBrush = rim;
            root.BorderThickness = new Thickness(1);
        }
        if (sheen != null) root.Children.Add(new Border { Background = sheen, CornerRadius = new CornerRadius(radius), IsHitTestVisible = false });
        if (motif.A > 0) root.Children.Add(Star(width, height, motif));
        ClipToCard(root, width, height, radius);

        var body = new Grid { Padding = new Thickness(16 * s, 14 * s, 16 * s, 14 * s) };
        root.Children.Add(body);
        bool needsTimes = Face.Id is not ("hijri" or "moon");
        _update = needsTimes && App.Clock.Day == null ? BuildNoPlace(body) : Face.Id switch
        {
            "list" => BuildList(body),
            "dual" => BuildDual(body),
            "ring" => BuildRing(body),
            "clock" => BuildClock(body),
            "day" => BuildDay(body),
            "hijri" => BuildHijri(body),
            "moon" => BuildMoon(body),
            _ => BuildNext(body),
        };
        _update();
        if (_ink.Shadows) TextShadow.Wrap(body);
        Content = root;
    }

    private static void ClipToCard(UIElement element, double width, double height, double radius)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var shape = compositor.CreateRoundedRectangleGeometry();
        shape.Size = new Vector2((float)width, (float)height);
        shape.CornerRadius = new Vector2((float)radius);
        visual.Clip = compositor.CreateGeometricClip(shape);
    }

    private static UIElement Star(double width, double height, Color color)
    {
        double r = Math.Min(width, height) * 0.62;
        double cx = width - r * 0.42, cy = height - r * 0.38;
        var group = new GeometryGroup();
        foreach (double scale in new[] { 1.0, 0.64 })
        {
            for (int square = 0; square < 2; square++)
            {
                var figure = new PathFigure { IsClosed = true };
                for (int corner = 0; corner < 4; corner++)
                {
                    double angle = (corner * 90 + square * 45) * Math.PI / 180;
                    var point = new Windows.Foundation.Point(cx + r * scale * Math.Cos(angle), cy + r * scale * Math.Sin(angle));
                    if (corner == 0) figure.StartPoint = point;
                    else figure.Segments.Add(new LineSegment { Point = point });
                }
                var path = new PathGeometry();
                path.Figures.Add(figure);
                group.Children.Add(path);
            }
        }
        group.Children.Add(new EllipseGeometry { Center = new Windows.Foundation.Point(cx, cy), RadiusX = r * 0.36, RadiusY = r * 0.36 });
        return new Path { Data = group, Stroke = new SolidColorBrush(color), StrokeThickness = 1.25, IsHitTestVisible = false };
    }

    private TextBlock T(double size, bool strong = false, bool muted = false) => new()
    {
        FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
        FontSize = size * _s,
        FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = muted ? _ink.Muted : _ink.Ink,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = TextWrapping.NoWrap,
    };

    private TextBlock Label()
    {
        var label = T(10.5, strong: true, muted: true);
        if (!Loc.Current.IsRtl) label.CharacterSpacing = 90;
        return label;
    }

    private static string Caps(string text) => Loc.Current.IsRtl ? text : text.ToUpper(Loc.Current.Culture);

    private TextBlock Figures(double size, bool light = true)
    {
        var text = T(size);
        text.FontFamily = Numerals;
        text.FontWeight = light ? FontWeights.SemiLight : FontWeights.SemiBold;
        return text;
    }

    private TextBlock Calligraphy(double size)
    {
        var text = T(size);
        text.FontFamily = Amiri;
        text.FlowDirection = FlowDirection.RightToLeft;
        text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        text.LineHeight = size * _s * 1.35;
        return text;
    }

    private SirajVectorIcon VectorIcon(string iconKey, double size, Brush? brush = null) =>
        SirajVectorIcon.Make(iconKey, size * _s, brush ?? _ink.Accent);

    private PrayerGlyph Icon(double size, Brush? brush = null) => new() { Width = size * _s, Height = size * _s, Brush = brush ?? _ink.Accent };

    private (Grid Bar, Action<double> Set) Bar(double thickness)
    {
        double h = thickness * _s;
        var filled = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) };
        var rest = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        var bar = new Grid { Height = h, CornerRadius = new CornerRadius(h / 2), Background = _ink.Track };
        bar.ColumnDefinitions.Add(filled);
        bar.ColumnDefinitions.Add(rest);
        bar.Children.Add(new Border { Background = _ink.Accent, CornerRadius = new CornerRadius(h / 2) });
        return (bar, f =>
        {
            f = Math.Clamp(f, 0, 1);
            filled.Width = new GridLength(f, GridUnitType.Star);
            rest.Width = new GridLength(1 - f, GridUnitType.Star);
        });
    }

    private static Grid Rows(params GridLength[] heights)
    {
        var grid = new Grid();
        foreach (var h in heights) grid.RowDefinitions.Add(new RowDefinition { Height = h });
        return grid;
    }

    private static Grid Columns(params GridLength[] widths)
    {
        var grid = new Grid();
        foreach (var w in widths) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        return grid;
    }

    private static readonly GridLength Auto = GridLength.Auto;
    private static readonly GridLength Star1 = new(1, GridUnitType.Star);

    private static T At<T>(T element, int row = 0, int column = 0) where T : FrameworkElement
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        return element;
    }

    private static StackPanel Across(double spacing, params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var c in children) panel.Children.Add(c);
        return panel;
    }

    private static StackPanel Down(double spacing, params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = spacing };
        foreach (var c in children) panel.Children.Add(c);
        return panel;
    }

    private static bool Eastern => Core.Text.Digits.UseEastern(App.Settings.Current.NumeralSystem, Loc.Current.Language);

    private static DateOnly Today => App.Clock.Place?.Today(App.Clock.Now) ?? DateOnly.FromDateTime(DateTime.Now);

    private static bool ArabicScript => Loc.Current.Language is "ar" or "ur";

    private static string ArabicName(PrayerTime prayer) => prayer.Type switch
    {
        PrayerType.Fajr => "الفجر",
        PrayerType.Sunrise => "الشروق",
        PrayerType.Dhuhr => prayer.Time.DayOfWeek == DayOfWeek.Friday ? "الجمعة" : "الظهر",
        PrayerType.Asr => "العصر",
        PrayerType.Maghrib => "المغرب",
        _ => "العشاء",
    };

    private static string Short(string time) => time.Split((char)0xA0)[0];

    private static PrayerTime? Current(PrayerDay day, DateTimeOffset now)
    {
        var today = day.Times.Where(t => t.Time <= now && t.Type != PrayerType.Sunrise).OrderBy(t => t.Time).LastOrDefault();
        if (today != null) return today;
        return PrayerCalculator.Raw(day.Place, day.Date.AddDays(-1), App.Settings.Current.Calculation)[(int)PrayerType.Isha];
    }

    private static double Progress(PrayerTime next)
    {
        var clock = App.Clock;
        if (clock.Day is not { } day) return 0;
        var previous = Current(day, clock.Now)?.Time ?? next.Time.AddHours(-6);
        double span = (next.Time - previous).TotalSeconds;
        return span <= 0 ? 0 : Math.Clamp((clock.Now - previous).TotalSeconds / span, 0, 1);
    }

    private readonly record struct Count(string Head, string Label, string Value, string Line, bool Stopwatch, PrayerTime? Prayer, double Progress);

    private static Count Counting()
    {
        var loc = Loc.Current;
        var now = App.Clock.Now;
        string Clock(TimeSpan t) => TimeText.Countdown(t, false, Eastern, loc.Language);
        switch (App.Clock.State)
        {
            case PrayerState.UpcomingAzan u:
                var value = u.Remaining <= SecondsWindow ? Clock(u.Remaining) : TrayIcon.Duration(u.Remaining);
                return new(loc["next_prayer"], loc["widget_starts_in"], value, loc.Format("dashboard_countdown_format", value), false, u.Prayer, Progress(u.Prayer));
            case PrayerState.AzanCalled a:
            {
                var left = Clock(a.IqamaTime - now);
                double span = (a.IqamaTime - a.Prayer.Time).TotalSeconds;
                double done = span <= 0 ? 1 : Math.Clamp((now - a.Prayer.Time).TotalSeconds / span, 0, 1);
                return new(loc["azan_time_now"], loc["widget_iqama_in"], left, loc.Format("widget_iqama_countdown", left), false, a.Prayer, done);
            }
            case PrayerState.WaitingForIqama w:
            {
                var left = Clock(w.UntilIqama);
                return new(loc["azan_time_now"], loc["widget_iqama_in"], left, loc.Format("widget_iqama_countdown", left), false, w.Prayer, w.Progress);
            }
            case PrayerState.InPrayer p:
            {
                var since = Clock(p.SinceIqama);
                return new(loc["prayer_in_progress"], "", since, since, true, p.Prayer, 1);
            }
            default:
                return new("", "", "", "", false, null, 0);
        }
    }

    private const string StopwatchIconKey = "timer";

    private Action BuildNoPlace(Grid body)
    {
        var text = T(13.5, muted: true);
        text.TextWrapping = TextWrapping.WrapWholeWords;
        text.TextAlignment = TextAlignment.Center;
        text.Text = Loc.Current["widget_no_location"];
        var icon = VectorIcon("location_on", 24);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        body.Children.Add(Down(8 * _s, icon, text).Also(p => p.VerticalAlignment = VerticalAlignment.Center));
        return () => { };
    }

    private Action BuildNext(Grid body)
    {
        double s = _s;
        var grid = Rows(Auto, Auto, Star1, Auto, Auto);
        var icon = Icon(20);
        var head = Label();
        head.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(At(Across(8 * s, icon, head)));

        var nameRow = At(Columns(Star1, Auto), 1);
        nameRow.Margin = new Thickness(0, 4 * s, 0, 0);
        var name = T(26, strong: true);
        var callig = Calligraphy(30);
        callig.HorizontalAlignment = HorizontalAlignment.Left;
        var time = T(14, muted: true);
        time.VerticalAlignment = VerticalAlignment.Center;
        time.Margin = new Thickness(10 * s, 0, 0, 0);
        nameRow.Children.Add(ArabicScript ? callig : name);
        nameRow.Children.Add(At(time, 0, 1));
        grid.Children.Add(nameRow);

        var countRow = At(Columns(Star1, Auto), 3);
        var label = Label();
        label.VerticalAlignment = VerticalAlignment.Bottom;
        label.Margin = new Thickness(0, 0, 0, 5 * s);
        var stopwatch = VectorIcon(StopwatchIconKey, 16);
        stopwatch.VerticalAlignment = VerticalAlignment.Bottom;
        stopwatch.Margin = new Thickness(0, 0, 0, 5 * s);
        var countdown = At(Figures(26), 0, 1);
        countRow.Children.Add(label);
        countRow.Children.Add(stopwatch);
        countRow.Children.Add(countdown);
        grid.Children.Add(countRow);

        var (bar, set) = Bar(4);
        bar.Margin = new Thickness(0, 8 * s, 0, 0);
        grid.Children.Add(At(bar, 4));
        body.Children.Add(grid);
        return () =>
        {
            var loc = Loc.Current;
            var c = Counting();
            head.Text = Caps(c.Head);
            label.Text = Caps(c.Label);
            label.Visibility = c.Stopwatch ? Visibility.Collapsed : Visibility.Visible;
            stopwatch.Visibility = c.Stopwatch ? Visibility.Visible : Visibility.Collapsed;
            countdown.Text = c.Value;
            set(c.Progress);
            if (c.Prayer is not { } prayer) return;
            icon.Prayer = prayer.Type;
            name.Text = loc.PrayerName(prayer);
            callig.Text = ArabicName(prayer);
            time.Text = loc.Time(prayer.Time);
        };
    }

    private Action BuildList(Grid body)
    {
        double s = _s;
        var grid = Rows(Auto, Star1, Auto);
        var weekday = T(15, strong: true);
        var hijri = T(12, muted: true);
        var header = Down(0, weekday, hijri);
        header.Margin = new Thickness(2 * s, 0, 0, 8 * s);
        grid.Children.Add(header);

        var list = At(Down(2 * s), 1);
        var rows = new List<(Border Row, PrayerGlyph Icon, TextBlock Name, TextBlock Time, PrayerType Type)>();
        foreach (var type in Enum.GetValues<PrayerType>())
        {
            var line = Columns(Auto, Star1, Auto);
            line.ColumnSpacing = 10 * s;
            line.Padding = new Thickness(10 * s, 6 * s, 12 * s, 6 * s);
            var icon = Icon(17);
            icon.Prayer = type;
            icon.VerticalAlignment = VerticalAlignment.Center;
            var name = At(T(14.5), 0, 1);
            var time = At(T(14.5, strong: true), 0, 2);
            line.Children.Add(icon);
            line.Children.Add(name);
            line.Children.Add(time);
            var row = new Border { CornerRadius = new CornerRadius(10 * s), Child = line };
            list.Children.Add(row);
            rows.Add((row, icon, name, time, type));
        }
        grid.Children.Add(list);

        var footer = At(T(12, muted: true), 2);
        footer.Margin = new Thickness(2 * s, 6 * s, 0, 0);
        grid.Children.Add(footer);
        body.Children.Add(grid);
        return () =>
        {
            var loc = Loc.Current;
            var clock = App.Clock;
            var today = Today;
            weekday.Text = HijriText.Gregorian(today, loc.Language, Eastern, "dddd");
            hijri.Text = HijriText.Format(HijriCalendars.Current.ToHijri(today), loc.Language, Eastern);
            var focus = clock.Focus;
            foreach (var r in rows)
            {
                var p = clock.Day?.Times.FirstOrDefault(t => t.Type == r.Type);
                r.Name.Text = p != null ? loc.PrayerName(p) : loc.PrayerName(r.Type);
                r.Time.Text = p != null ? loc.Time(p.Time) : "--:--";
                bool isFocus = focus?.Type == r.Type;
                bool past = !isFocus && p != null && p.Time <= clock.Now && clock.Day?.Next?.Type != r.Type;
                r.Row.Background = isFocus ? _ink.Pill : null;
                r.Name.Foreground = r.Time.Foreground = isFocus ? _ink.OnPill : _ink.Ink;
                r.Icon.Brush = isFocus ? _ink.OnPill : _ink.Accent;
                r.Row.Opacity = past ? 0.55 : 1;
            }
            var c = Counting();
            footer.Text = c.Prayer == null ? "" : c.Stopwatch ? $"{c.Head}{loc.Sep}{c.Value}" : $"{loc.PrayerName(c.Prayer)} {c.Line}";
        };
    }

    private Action BuildDual(Grid body)
    {
        double s = _s;
        var grid = Columns(Star1, Star1);
        grid.ColumnSpacing = 8 * s;
        body.Padding = new Thickness(10 * s);

        Border Tile(Brush background, Brush ink, Brush muted, Brush accent, out PrayerGlyph icon, out TextBlock head, out TextBlock name, out TextBlock callig, out TextBlock line)
        {
            icon = Icon(18, accent);
            head = Label();
            head.Foreground = muted;
            head.VerticalAlignment = VerticalAlignment.Center;
            name = T(21, strong: true);
            name.Foreground = ink;
            callig = Calligraphy(24);
            callig.Foreground = ink;
            callig.HorizontalAlignment = HorizontalAlignment.Left;
            line = Figures(17);
            line.Foreground = ink;
            var stack = Rows(Auto, Star1, Auto);
            stack.Children.Add(Across(7 * s, icon, head));
            var names = At(new Grid { VerticalAlignment = VerticalAlignment.Center }, 1);
            names.Children.Add(name);
            names.Children.Add(callig);
            stack.Children.Add(names);
            stack.Children.Add(At(line, 2));
            return new Border { Background = background, CornerRadius = new CornerRadius(12 * s), Padding = new Thickness(12 * s, 10 * s, 12 * s, 10 * s), Child = stack };
        }

        var now = Tile(_ink.Tile, _ink.Ink, _ink.Muted, _ink.Accent, out var nowIcon, out var nowHead, out var nowName, out var nowCallig, out var nowTime);
        var next = At(Tile(_ink.Pill, _ink.OnPill, _ink.OnPill, _ink.OnPill, out var nextIcon, out var nextHead, out var nextName, out var nextCallig, out var nextLine), 0, 1);
        nextHead.Opacity = 0.85;
        nextLine.FontSize = 15 * s;
        nextLine.FontFamily = (FontFamily)Application.Current.Resources["SirajFont"];
        nextLine.FontWeight = FontWeights.Normal;
        grid.Children.Add(now);
        grid.Children.Add(next);
        body.Children.Add(grid);
        return () =>
        {
            var loc = Loc.Current;
            var clock = App.Clock;
            if (clock.Day is not { } day) return;
            nowHead.Text = Caps(loc["win_now"]);
            nextHead.Text = Caps(loc["win_next_short"]);
            var c = Counting();
            // From the adhan to the end of the prayer, "now" is that prayer, and "next" its iqama.
            var current = c.Prayer != null && c.Prayer.Type != PrayerType.Sunrise && clock.State is not PrayerState.UpcomingAzan ? c.Prayer : Current(day, clock.Now);
            nowName.Text = current != null ? loc.PrayerName(current) : "";
            nowCallig.Text = current != null ? ArabicName(current) : "";
            nowName.Visibility = ArabicScript ? Visibility.Collapsed : Visibility.Visible;
            nowCallig.Visibility = ArabicScript ? Visibility.Visible : Visibility.Collapsed;
            if (current != null) nowIcon.Prayer = current.Type;
            nowTime.Text = current != null ? loc.Time(current.Time) : "";

            var upcoming = clock.State is PrayerState.UpcomingAzan u ? u.Prayer : day.Next;
            bool iqama = clock.State is PrayerState.AzanCalled or PrayerState.WaitingForIqama;
            nextName.Visibility = ArabicScript && !iqama ? Visibility.Collapsed : Visibility.Visible;
            nextCallig.Visibility = ArabicScript && !iqama ? Visibility.Visible : Visibility.Collapsed;
            if (iqama)
            {
                nextName.Text = loc["widget_iqama_label"];
                nextLine.Text = loc.Format("dashboard_countdown_format", c.Value);
                if (c.Prayer != null) nextIcon.Prayer = c.Prayer.Type;
            }
            else if (upcoming != null)
            {
                nextName.Text = loc.PrayerName(upcoming);
                nextCallig.Text = ArabicName(upcoming);
                nextIcon.Prayer = upcoming.Type;
                nextLine.Text = clock.State is PrayerState.UpcomingAzan ? c.Line
                    : loc.Format("dashboard_countdown_format", TrayIcon.Duration(upcoming.Time - clock.Now));
            }
        };
    }

    private Action BuildRing(Grid body)
    {
        double s = _s;
        body.Padding = new Thickness(10 * s);
        double size = Face.Width * s - 20 * s, stroke = 7 * s;
        var track = new Ellipse { Width = size, Height = size, Stroke = _ink.Track, StrokeThickness = stroke };
        var arc = new Path { Stroke = _ink.Accent, StrokeThickness = stroke, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        var ring = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        ring.Children.Add(track);
        ring.Children.Add(arc);

        var icon = Icon(18);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        var head = Label();
        var name = T(24, strong: true);
        var callig = Calligraphy(28);
        var countdown = T(14);
        var stopwatch = VectorIcon(StopwatchIconKey, 13);
        stopwatch.VerticalAlignment = VerticalAlignment.Center;
        var countRow = Across(5 * s, stopwatch, countdown);
        foreach (var e in new FrameworkElement[] { head, name, callig, countRow }) e.HorizontalAlignment = HorizontalAlignment.Center;
        head.MaxWidth = countdown.MaxWidth = size - 34 * s;
        var centre = Down(1 * s, icon, head, name, callig, countRow);
        centre.HorizontalAlignment = HorizontalAlignment.Center;
        centre.VerticalAlignment = VerticalAlignment.Center;
        ring.Children.Add(centre);
        body.Children.Add(ring);
        return () =>
        {
            var loc = Loc.Current;
            var c = Counting();
            head.Text = Caps(c.Head);
            stopwatch.Visibility = c.Stopwatch ? Visibility.Visible : Visibility.Collapsed;
            countdown.Text = c.Line;
            name.Visibility = ArabicScript ? Visibility.Collapsed : Visibility.Visible;
            callig.Visibility = ArabicScript ? Visibility.Visible : Visibility.Collapsed;
            if (c.Prayer is { } prayer)
            {
                icon.Prayer = prayer.Type;
                name.Text = loc.PrayerName(prayer);
                callig.Text = ArabicName(prayer);
            }
            // The arc starts at the top and runs clockwise; in Arabic and Urdu, the other way.
            arc.Data = ArcGeometry(size / 2, size / 2 - stroke / 2, c.Progress);
        };
    }

    private static Geometry ArcGeometry(double centre, double radius, double fraction)
    {
        fraction = Math.Clamp(fraction, 0.001, 0.9999);
        double angle = fraction * 2 * Math.PI;
        var start = new Windows.Foundation.Point(centre, centre - radius);
        var end = new Windows.Foundation.Point(centre + radius * Math.Sin(angle), centre - radius * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = end, Size = new Windows.Foundation.Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise, IsLargeArc = fraction > 0.5,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private Action BuildClock(Grid body)
    {
        double s = _s;
        var grid = Rows(Auto, Auto, Auto, Star1, Auto);
        var digits = Figures(56);
        var marker = T(17, muted: true);
        marker.VerticalAlignment = VerticalAlignment.Bottom;
        marker.Margin = new Thickness(0, 0, 0, 12 * s);
        var time = Across(6 * s, digits, marker);
        time.Margin = new Thickness(-2 * s, -8 * s, 0, -4 * s);
        grid.Children.Add(time);
        var gregorian = At(Label(), 1);
        var hijri = At(T(13, muted: true), 2);
        hijri.Margin = new Thickness(0, 1 * s, 0, 0);
        grid.Children.Add(gregorian);
        grid.Children.Add(hijri);

        var icon = Icon(16, _ink.OnPill);
        icon.VerticalAlignment = VerticalAlignment.Center;
        var next = T(13, strong: true);
        next.Foreground = _ink.OnPill;
        var count = T(13);
        count.Foreground = _ink.OnPill;
        count.Opacity = 0.85;
        var pill = At(new Border
        {
            Background = _ink.Pill, CornerRadius = new CornerRadius(14 * s), Padding = new Thickness(10 * s, 5 * s, 12 * s, 6 * s),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = Across(7 * s, icon, next, count),
        }, 4);
        grid.Children.Add(pill);
        body.Children.Add(grid);
        return () =>
        {
            var loc = Loc.Current;
            var clock = App.Clock;
            var now = clock.Place != null ? Core.Places.Zones.On(clock.Now, clock.Place.Zone) : clock.Now;
            var parts = loc.Time(now).Split((char)0xA0);
            // A clock reads 9:51, not 09:51.
            digits.Text = parts[0].Length == 5 && parts.Length > 1 && parts[0][0] is '0' or '٠' or '۰' ? parts[0][1..] : parts[0];
            marker.Text = parts.Length > 1 ? parts[1] : "";
            marker.Visibility = parts.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
            var today = DateOnly.FromDateTime(now.DateTime);
            gregorian.Text = Caps(HijriText.Gregorian(today, loc.Language, Eastern, "dddd") + loc.Sep + HijriText.Gregorian(today, loc.Language, Eastern, "d MMMM"));
            hijri.Text = HijriText.Format(HijriCalendars.Current.ToHijri(Today), loc.Language, Eastern);
            var c = Counting();
            pill.Visibility = c.Prayer != null ? Visibility.Visible : Visibility.Collapsed;
            if (c.Prayer is not { } prayer) return;
            icon.Prayer = prayer.Type;
            next.Text = $"{loc.PrayerName(prayer)} {loc.Time(prayer.Time)}";
            count.Text = c.Stopwatch ? c.Head : c.Line;
        };
    }

    private Action BuildDay(Grid body)
    {
        double s = _s;
        var grid = Rows(Auto, Star1, Auto, Auto);
        var top = Columns(Star1, Auto);
        var weekday = T(25);
        weekday.FontWeight = FontWeights.Light;
        if (!Loc.Current.IsRtl) weekday.CharacterSpacing = 240;
        var callig = Calligraphy(34);
        callig.HorizontalAlignment = HorizontalAlignment.Left;
        callig.Margin = new Thickness(0, -6 * s, 0, -6 * s);
        top.Children.Add(ArabicScript ? callig : weekday);
        var hijri = T(13.5, strong: true);
        var gregorian = T(12, muted: true);
        hijri.HorizontalAlignment = gregorian.HorizontalAlignment = HorizontalAlignment.Right;
        var dates = At(Down(1 * s, hijri, gregorian), 0, 1);
        dates.VerticalAlignment = VerticalAlignment.Center;
        top.Children.Add(dates);
        grid.Children.Add(top);

        var five = At(new Grid { ColumnSpacing = 4 * s }, 2);
        var cells = new List<(Border Cell, Ellipse Dot, TextBlock Name, TextBlock Time, PrayerType Type)>();
        int i = 0;
        foreach (var type in new[] { PrayerType.Fajr, PrayerType.Dhuhr, PrayerType.Asr, PrayerType.Maghrib, PrayerType.Isha })
        {
            five.ColumnDefinitions.Add(new ColumnDefinition());
            var dot = new Ellipse { Width = 5 * s, Height = 5 * s, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 3 * s) };
            var name = T(11.5, muted: true);
            var time = T(15.5, strong: true);
            name.HorizontalAlignment = time.HorizontalAlignment = HorizontalAlignment.Center;
            var cell = At(new Border
            {
                CornerRadius = new CornerRadius(12 * s), Padding = new Thickness(2 * s, 6 * s, 2 * s, 6 * s),
                Child = Down(0, dot, name, time),
            }, 0, i++);
            five.Children.Add(cell);
            cells.Add((cell, dot, name, time, type));
        }
        grid.Children.Add(five);
        var (bar, set) = Bar(3);
        bar.Margin = new Thickness(4 * s, 8 * s, 4 * s, 0);
        grid.Children.Add(At(bar, 3));
        body.Children.Add(grid);
        return () =>
        {
            var loc = Loc.Current;
            var clock = App.Clock;
            var today = Today;
            var name = HijriText.Gregorian(today, loc.Language, Eastern, "dddd");
            weekday.Text = Caps(name);
            callig.Text = name;
            hijri.Text = HijriText.Format(HijriCalendars.Current.ToHijri(today), loc.Language, Eastern);
            gregorian.Text = HijriText.Gregorian(today, loc.Language, Eastern, "d MMMM yyyy");
            var focus = clock.Focus;
            foreach (var c in cells)
            {
                var p = clock.Day?.Times.FirstOrDefault(t => t.Type == c.Type);
                c.Name.Text = p != null ? loc.PrayerName(p) : loc.PrayerName(c.Type);
                c.Time.Text = p != null ? Short(loc.Time(p.Time)) : "--:--";
                bool isFocus = focus?.Type == c.Type;
                bool past = !isFocus && p != null && p.Time <= clock.Now && clock.Day?.Next?.Type != c.Type;
                c.Cell.Background = isFocus ? _ink.Pill : null;
                c.Name.Foreground = isFocus ? _ink.OnPill : _ink.Muted;
                c.Time.Foreground = isFocus ? _ink.OnPill : _ink.Ink;
                c.Dot.Fill = isFocus ? _ink.OnPill : _ink.Track;
                c.Cell.Opacity = past ? 0.55 : 1;
            }
            set(Counting().Progress);
        };
    }

    private Action BuildHijri(Grid body)
    {
        double s = _s;
        var grid = Rows(Auto, Auto, Star1, Auto, Auto);
        grid.Children.Add(Label().Also(l => l.Text = Caps(Loc.Current["widget_hijri_label"])));

        var date = At(Columns(Auto, Star1), 1);
        date.ColumnSpacing = 12 * s;
        date.Margin = new Thickness(0, 2 * s, 0, 0);
        var day = Figures(46);
        day.Margin = new Thickness(-1 * s, -6 * s, 0, -6 * s);
        var month = T(17, strong: true);
        var callig = Calligraphy(26);
        callig.HorizontalAlignment = HorizontalAlignment.Left;
        callig.Margin = new Thickness(0, -4 * s, 0, -2 * s);
        var sub = T(12, muted: true);
        var names = At(Down(0, ArabicScript ? callig : month, sub), 0, 1);
        names.VerticalAlignment = VerticalAlignment.Center;
        date.Children.Add(day);
        date.Children.Add(names);
        grid.Children.Add(date);

        var rule = At(new Border { Height = 1, Background = _ink.Track, Margin = new Thickness(0, 0, 0, 8 * s) }, 3);
        grid.Children.Add(rule);
        var occasion = At(Columns(Star1, Auto), 4);
        occasion.ColumnSpacing = 10 * s;
        var label = Label();
        var title = T(13.5, strong: true);
        occasion.Children.Add(Down(1 * s, label, title));
        var count = Figures(24);
        count.HorizontalAlignment = HorizontalAlignment.Right;
        count.Margin = new Thickness(0, -4 * s, 0, -2 * s);
        var unit = T(11, muted: true);
        unit.HorizontalAlignment = HorizontalAlignment.Right;
        var right = At(Down(0, count, unit), 0, 1);
        right.VerticalAlignment = VerticalAlignment.Bottom;
        occasion.Children.Add(right);
        grid.Children.Add(occasion);
        body.Children.Add(grid);
        return () =>
        {
            var loc = Loc.Current;
            var today = Today;
            var calendar = HijriCalendars.Current;
            var hijri = calendar.ToHijri(today);
            day.Text = loc.Number(hijri.Day);
            month.Text = HijriText.MonthName(hijri.Month, loc.Language);
            callig.Text = HijriText.MonthName(hijri.Month, "ar");
            sub.Text = HijriText.Year(hijri.Year, loc.Language, Eastern) + loc.Sep +
                       HijriText.Gregorian(today, loc.Language, Eastern, ArabicScript ? "d MMMM" : "d MMM");
            var next = IslamicOccasions.Upcoming(today, calendar).FirstOrDefault();
            occasion.Visibility = rule.Visibility = next != null ? Visibility.Visible : Visibility.Collapsed;
            if (next == null) return;
            label.Text = Caps(loc["widget_countdown_label"]);
            title.Text = next.Occasion.Title(loc.Language);
            int days = next.DaysUntil;
            count.Text = days == 0 ? "" : loc.Number(days);
            count.Visibility = days == 0 ? Visibility.Collapsed : Visibility.Visible;
            unit.Text = days == 0 ? loc["calendar_countdown_today"] : loc.Plural("widget_countdown_unit", days);
        };
    }

    private Action BuildMoon(Grid body)
    {
        double s = _s;
        var grid = Rows(Auto, Star1, Auto, Auto);
        var top = Columns(Auto, Star1);
        top.ColumnSpacing = 14 * s;
        var disc = new MoonDisc { Width = 64 * s, Height = 64 * s };
        MoonPhase? phase = null;
        top.Children.Add(disc);
        var day = Figures(32);
        day.Margin = new Thickness(0, -6 * s, 0, -4 * s);
        var month = T(14, strong: true);
        var callig = Calligraphy(22);
        callig.HorizontalAlignment = HorizontalAlignment.Left;
        var dates = At(Down(0, day, ArabicScript ? callig : month), 0, 1);
        dates.VerticalAlignment = VerticalAlignment.Center;
        top.Children.Add(dates);
        grid.Children.Add(top);

        var name = At(T(15, strong: true), 2);
        var line = At(T(12, muted: true), 3);
        line.TextWrapping = TextWrapping.WrapWholeWords;
        line.MaxLines = 2;
        grid.Children.Add(name);
        grid.Children.Add(line);
        body.Children.Add(grid);
        long minute = -1;
        return () =>
        {
            var now = App.Clock.Now;
            if (now.ToUnixTimeSeconds() / 60 == minute && phase != null) return;
            minute = now.ToUnixTimeSeconds() / 60;
            phase = MoonPhase.At(now);
            var loc = Loc.Current;
            var today = Today;
            var calendar = HijriCalendars.Current;
            var hijri = calendar.ToHijri(today);
            day.Text = loc.Number(hijri.Day);
            month.Text = HijriText.MonthName(hijri.Month, loc.Language);
            callig.Text = HijriText.MonthName(hijri.Month, "ar");
            name.Text = loc[MoonDisc.NameKey(phase.Name)];
            line.Text = MoonDisc.Line(loc, phase, today, Eastern);
            disc.Phase = phase;
        };
    }
}

internal static class ElementExtensions
{
    public static T Also<T>(this T element, Action<T> set)
    {
        set(element);
        return element;
    }
}

internal static class TextShadow
{
    public static void Wrap(Panel panel)
    {
        for (int i = 0; i < panel.Children.Count; i++)
        {
            switch (panel.Children[i])
            {
                case TextBlock text:
                    var host = new Grid
                    {
                        HorizontalAlignment = text.HorizontalAlignment, VerticalAlignment = text.VerticalAlignment,
                        Margin = text.Margin, Visibility = text.Visibility,
                    };
                    Grid.SetRow(host, Grid.GetRow(text));
                    Grid.SetColumn(host, Grid.GetColumn(text));
                    text.Margin = new Thickness(0);
                    var caster = new Canvas { IsHitTestVisible = false };
                    panel.Children[i] = host;
                    host.Children.Add(caster);
                    host.Children.Add(text);
                    Cast(caster, text);
                    // The text's visibility now belongs to the host, which the face still sets on the text.
                    text.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => host.Visibility = text.Visibility);
                    break;
                case Panel child:
                    Wrap(child);
                    break;
                case Border { Child: Panel inner }:
                    Wrap(inner);
                    break;
            }
        }
    }

    private static void Cast(Canvas caster, TextBlock text)
    {
        var visual = ElementCompositionPreview.GetElementVisual(caster);
        var compositor = visual.Compositor;
        var shadow = compositor.CreateDropShadow();
        shadow.BlurRadius = 8f;
        shadow.Opacity = 0.55f;
        shadow.Color = Colors.Black;
        shadow.Offset = new Vector3(0, 1.5f, 0);
        var sprite = compositor.CreateSpriteVisual();
        sprite.Shadow = shadow;
        ElementCompositionPreview.SetElementChildVisual(caster, sprite);
        void Fit()
        {
            sprite.Size = new Vector2((float)text.ActualWidth, (float)text.ActualHeight);
            // The mask follows the text as it changes; it is taken again only when the size does.
            shadow.Mask = text.GetAlphaMask();
        }
        text.SizeChanged += (_, _) => Fit();
    }
}
