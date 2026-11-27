using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Controls;
using Siraj.App.Native;
using Siraj.App.Services;
using Siraj.Core.Prayer;
using Siraj.Core.Settings;
using Siraj.Core.Sky;
using Siraj.Core.Text;
using Siraj.Core.Theme;
using Windows.Foundation;
using Windows.UI;

namespace Siraj.App.Widgets;

internal sealed class TaskbarPillView : UserControl
{
    private static readonly Color GoldBg = Color.FromArgb(236, 186, 152, 92);
    private static readonly Color GoldBgHover = Color.FromArgb(248, 202, 168, 108);
    private static readonly Color GoldBorder = Color.FromArgb(220, 238, 206, 144);
    private static readonly Color CoralBg = Color.FromArgb(236, 198, 72, 52);
    private static readonly Color CoralBgHover = Color.FromArgb(248, 214, 86, 66);
    private static readonly Color CoralBorder = Color.FromArgb(225, 255, 160, 140);
    private static readonly Color GoldText = Color.FromArgb(255, 240, 202, 114);
    private static readonly Color UrgentFg = Color.FromArgb(255, 255, 252, 245);
    private static readonly Color UrgentFgMuted = Color.FromArgb(230, 255, 248, 232);

    private readonly Border _shell = new() { BorderThickness = new Thickness(1) };
    private readonly Border _glassSheen = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Border _pulseOverlay = new() { IsHitTestVisible = false, Opacity = 0 };
    private readonly Grid _root = new() { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
    private readonly StackPanel _content = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
    private readonly TextBlock _prayerText = MakeLine(11.5, true);
    private readonly TextBlock _timeText = MakeLine(10.0, false);
    private readonly TextBlock _prefixText = MakeLine(10.0, false);
    private readonly TextBlock _countdownText = MakeLine(11.5, true);
    private readonly SirajVectorIcon _icon = new()
    {
        IconKey = "timer",
        Width = 13,
        Height = 13,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly StackPanel _leftCol = new() { VerticalAlignment = VerticalAlignment.Center, Spacing = 0 };
    private readonly StackPanel _rightCol = new() { VerticalAlignment = VerticalAlignment.Center, Spacing = 0 };

    private TaskbarWidgetConfig _config;
    private readonly bool _live;
    private readonly bool _draggable;
    private bool _hovered;
    private bool _urgent;
    private bool _forceUrgent;
    private double _pulsePhase;
    private bool _pointerDown;
    private bool _dragging;
    private int _dragStartCursorX;
    private int _dragLastCursorX;
    private IDisposable? _seconds;
    private DispatcherQueueTimer? _pulseTimer;
    private bool _useRealBackdrop;

    public bool IsInteracting => _pointerDown || _dragging;

    public event Action? Clicked;
    public event Action? SizeInvalidated;
    public event Action<int>? DragDelta;
    public event Action? DragCompleted;

    public bool ForceUrgent
    {
        get => _forceUrgent;
        set
        {
            if (_forceUrgent == value) return;
            _forceUrgent = value;
            UpdateVisuals();
        }
    }

    public TaskbarPillView(TaskbarWidgetConfig config, bool live, bool draggable = false, bool useRealBackdrop = false)
    {
        _config = config;
        _live = live;
        _draggable = draggable;
        _useRealBackdrop = useRealBackdrop;

        _root.Children.Add(_shell);
        _root.Children.Add(_glassSheen);
        _root.Children.Add(_pulseOverlay);
        _shell.Child = _content;
        Content = _root;

        _root.PointerEntered += (_, _) =>
        {
            _hovered = true;
            ApplyPalette();
        };
        _root.PointerExited += (_, _) =>
        {
            _hovered = false;
            if (!_dragging) _pointerDown = false;
            ApplyPalette();
        };
        _root.PointerPressed += OnPointerPressed;
        _root.PointerMoved += OnPointerMoved;
        _root.PointerReleased += OnPointerReleased;
        _root.PointerCanceled += OnPointerCanceled;
        _root.PointerCaptureLost += OnPointerCaptureLost;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        RebuildLayout();
        UpdateVisuals();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint(_root).Properties;
        if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse && !props.IsLeftButtonPressed)
            return;
        _pointerDown = true;
        _dragging = false;
        if (_draggable && !_config.Locked)
        {
            Win32.GetCursorPos(out var pt);
            _dragStartCursorX = _dragLastCursorX = pt.X;
        }
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_pointerDown || !_draggable || _config.Locked) return;
        Win32.GetCursorPos(out var pt);
        if (!_dragging && Math.Abs(pt.X - _dragStartCursorX) >= 8)
        {
            _dragging = true;
            _root.CapturePointer(e.Pointer);
        }
        if (_dragging)
        {
            int delta = pt.X - _dragLastCursorX;
            if (delta != 0)
            {
                _dragLastCursorX = pt.X;
                DragDelta?.Invoke(delta);
            }
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pointerDown) return;
        bool wasDragging = _dragging;
        _pointerDown = false;
        _dragging = false;
        _root.ReleasePointerCaptures();
        e.Handled = true;
        if (wasDragging) DragCompleted?.Invoke();
        else Clicked?.Invoke();
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        bool wasDragging = _dragging;
        _pointerDown = false;
        _dragging = false;
        _root.ReleasePointerCaptures();
        if (wasDragging) DragCompleted?.Invoke();
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _pointerDown = false;
        _dragging = false;
        DragCompleted?.Invoke();
    }

    public void Apply(TaskbarWidgetConfig config)
    {
        bool layoutChanged = config.Layout != _config.Layout
            || config.ShowPrayerTime != _config.ShowPrayerTime
            || config.ShowPrefix != _config.ShowPrefix
            || config.CornerRadius != _config.CornerRadius
            || config.Style != _config.Style
            || Math.Abs(config.Scale - _config.Scale) > 0.001
            || config.ShowSeconds != _config.ShowSeconds;
        _config = config;
        if (layoutChanged) RebuildLayout();
        UpdateVisuals();
        if (layoutChanged) SizeInvalidated?.Invoke();
    }

    public void Refresh()
    {
        RebuildLayout();
        UpdateVisuals();
        SizeInvalidated?.Invoke();
    }

    public double MeasureWidth(double availableHeightDip)
    {
        Measure(new Size(640, Math.Max(28, availableHeightDip)));
        double w = DesiredSize.Width;
        if (w < 40 || double.IsNaN(w) || double.IsInfinity(w))
        {
            double s = Math.Clamp(_config.Scale, 0.85, 1.15);
            return (_config.ShowSeconds ? 168 : 144) * s;
        }
        return Math.Ceiling(w + 2);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_live) _seconds = App.Clock.SubscribeSeconds();
        App.Clock.Tick += OnClockTick;
        App.Clock.DayChanged += OnClockTick;
        App.Theme.Changed += OnThemeChanged;
        if (App.Adhan != null) App.Adhan.PlayingChanged += OnAdhanChanged;

        _pulseTimer = DispatcherQueue.CreateTimer();
        _pulseTimer.Interval = TimeSpan.FromMilliseconds(55);
        _pulseTimer.Tick += (_, _) =>
        {
            _pulsePhase += 0.14;
            double wave = 0.5 + 0.5 * Math.Sin(_pulsePhase);
            _pulseOverlay.Opacity = 0.08 + 0.22 * wave;
        };

        UpdateVisuals();
        SizeInvalidated?.Invoke();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _seconds?.Dispose();
        _seconds = null;
        App.Clock.Tick -= OnClockTick;
        App.Clock.DayChanged -= OnClockTick;
        App.Theme.Changed -= OnThemeChanged;
        if (App.Adhan != null) App.Adhan.PlayingChanged -= OnAdhanChanged;
        _pulseTimer?.Stop();
        _pulseTimer = null;
    }

    private void OnClockTick()
    {
        string prev = _prayerText.Text + _countdownText.Text + _timeText.Text;
        UpdateVisuals();
        if (_prayerText.Text + _countdownText.Text + _timeText.Text != prev)
            SizeInvalidated?.Invoke();
    }

    private void OnThemeChanged() => ApplyPalette();
    private void OnAdhanChanged(PrayerType? _) => DispatcherQueue.TryEnqueue(UpdateVisuals);

    private void RebuildLayout()
    {
        var loc = Loc.Current;
        FlowDirection = loc.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        double s = Math.Clamp(_config.Scale, 0.85, 1.15);
        // Keep two-line stacks comfortably within the standard 40px taskbar height even at Large.
        double twoLineScale = Math.Min(s, 1.06);
        bool pillCap = _config.CornerRadius >= 16 && _config.Style is not ("none" or "glass");
        double padX = (_config.Style == "none" ? 6 : pillCap ? 14 : 10) * twoLineScale;

        _content.Children.Clear();
        _leftCol.Children.Clear();
        _rightCol.Children.Clear();

        _prayerText.HorizontalAlignment = HorizontalAlignment.Center;
        _timeText.HorizontalAlignment = HorizontalAlignment.Center;
        _prefixText.HorizontalAlignment = HorizontalAlignment.Center;
        _countdownText.HorizontalAlignment = HorizontalAlignment.Center;

        switch (_config.Layout)
        {
            case "compact":
                _prayerText.FontSize = 11.5 * s;
                _timeText.FontSize = 11.0 * s;
                _countdownText.FontSize = 11.5 * s;
                _icon.Width = _icon.Height = 13.0 * s;
                _content.Spacing = 6 * s;
                _shell.Padding = new Thickness(padX, 3, padX, 3);
                _content.Children.Add(_icon);
                _content.Children.Add(_prayerText);
                if (_config.ShowPrayerTime) _content.Children.Add(_timeText);
                _content.Children.Add(_countdownText);
                break;

            case "countdown":
                _prayerText.FontSize = 11.0 * twoLineScale;
                _countdownText.FontSize = 11.0 * twoLineScale;
                _content.Spacing = 0;
                _shell.Padding = new Thickness(padX, 1, padX, 1);
                _leftCol.Children.Add(_prayerText);
                _leftCol.Children.Add(_countdownText);
                _content.Children.Add(_leftCol);
                break;

            default:
                _prayerText.FontSize = 11.0 * twoLineScale;
                _timeText.FontSize = 10.0 * twoLineScale;
                _prefixText.FontSize = 10.0 * twoLineScale;
                _countdownText.FontSize = 11.0 * twoLineScale;
                _content.Spacing = 10 * twoLineScale;
                _shell.Padding = new Thickness(padX, 1, padX, 1);
                _leftCol.Children.Add(_prayerText);
                if (_config.ShowPrayerTime) _leftCol.Children.Add(_timeText);
                if (_config.ShowPrefix) _rightCol.Children.Add(_prefixText);
                _rightCol.Children.Add(_countdownText);
                _content.Children.Add(_leftCol);
                _content.Children.Add(_rightCol);
                break;
        }
    }

    private void UpdateVisuals()
    {
        var loc = Loc.Current;
        var clock = App.Clock;
        var now = clock.Now;
        var day = clock.Day;
        bool eastern = Digits.UseEastern(App.Settings.Current.NumeralSystem, loc.Language);

        if (day == null)
        {
            _prayerText.Text = loc["app_name"];
            _timeText.Text = "";
            _prefixText.Text = "";
            _countdownText.Text = "--:--";
            _urgent = _forceUrgent && _config.UrgentStyle != "none";
            ApplyPalette();
            return;
        }

        var state = clock.State ?? PrayerState.At(day, now);
        var displayed = state switch
        {
            PrayerState.AzanCalled a => a.Prayer,
            PrayerState.WaitingForIqama w => w.Prayer,
            PrayerState.InPrayer p => p.Prayer,
            PrayerState.UpcomingAzan u => u.Prayer,
            _ => day.Next,
        };

        string prayerName = displayed != null ? loc.PrayerName(displayed) : loc["app_name"];
        string timeStr = displayed != null ? loc.Time(displayed.Time) : "";
        bool isPlaying = App.Adhan?.IsPlaying == true;
        bool dismissed = displayed != null && App.TaskbarWidget?.IsUrgentDismissed(day.Date, displayed.Type) == true;

        string prefix = loc["win_taskbar_in_prefix"];
        string countdown = "";
        bool urgent = false;

        switch (state)
        {
            case PrayerState.AzanCalled:
                prefix = loc["adhan_badge"];
                countdown = loc["win_now"];
                urgent = !dismissed || isPlaying;
                break;
            case PrayerState.WaitingForIqama w:
                prefix = loc["iqama"];
                countdown = FormatSpan(w.UntilIqama, _config.ShowSeconds, eastern, loc.Language);
                urgent = !dismissed;
                break;
            case PrayerState.InPrayer:
                prefix = loc["in_prayer"];
                countdown = loc["win_now"];
                urgent = false;
                break;
            case PrayerState.UpcomingAzan u:
                prefix = loc["win_taskbar_in_prefix"];
                countdown = FormatSpan(u.Remaining, _config.ShowSeconds, eastern, loc.Language);
                double mins = u.Remaining.TotalMinutes;
                urgent = !dismissed && _config.UrgentMinutes > 0 && mins > 0 && mins <= _config.UrgentMinutes;
                break;
        }

        _urgent = (_forceUrgent || urgent || isPlaying) && _config.UrgentStyle != "none";
        _prayerText.Text = prayerName;
        _timeText.Text = _config.Layout == "compact" ? $"· {timeStr} ·" : timeStr;
        _prefixText.Text = prefix;
        _countdownText.Text = countdown;
        _icon.IconKey = _urgent ? "notifications" : "timer";

        ApplyPalette();
        UpdatePulseTimer();
    }

    private void UpdatePulseTimer()
    {
        if (_pulseTimer == null) return;
        bool want = _urgent && _config.PulseOnUrgent && _config.Style != "none" && Kit.Motion;
        if (want && !_pulseTimer.IsRunning)
        {
            _pulseTimer.Start();
        }
        else if (!want)
        {
            if (_pulseTimer.IsRunning) _pulseTimer.Stop();
            _pulseOverlay.Opacity = 0;
        }
    }

    internal static ColorScheme ResolveScheme(TaskbarWidgetConfig config, bool dark)
    {
        if (string.IsNullOrEmpty(config.Palette) || config.Palette == "App")
            return Palettes.Scheme(App.Settings.Current.Palette, dark, false);
        return Palettes.Scheme(config.Palette, dark, false);
    }

    private void ApplyPalette()
    {
        bool lightTaskbar = Win32.IsTaskbarLightTheme();
        var scheme = ResolveScheme(_config, dark: !lightTaskbar);
        bool isGlass = _config.Style == "glass";
        bool isTextOnly = _config.Style == "none";

        // When using real DWM acrylic backdrop, match Windows 11's rounded corner radius (8px or 4px).
        double radiusVal = isGlass && _useRealBackdrop
            ? (_config.CornerRadius <= 6 ? 4 : 8)
            : Math.Clamp(_config.CornerRadius, 4, 20);
        var r = new CornerRadius(radiusVal);
        _shell.CornerRadius = r;
        _glassSheen.CornerRadius = r;
        _pulseOverlay.CornerRadius = r;
        _glassSheen.Visibility = Visibility.Collapsed;

        double opacityFrac = Math.Clamp(_config.Opacity, 25, 100) / 100.0;
        byte Op(double baseAlpha) => (byte)Math.Clamp(baseAlpha * opacityFrac * 255.0, 0, 255);

        if (_urgent && !isTextOnly)
        {
            Color bg, border, pulse;
            Color fg = UrgentFg, fgMuted = UrgentFgMuted;
            switch (_config.UrgentStyle)
            {
                case "accent":
                    bg = ThemeService.ToColor(scheme.Primary, _hovered ? 0.96f : 0.88f);
                    border = ThemeService.ToColor(scheme.OnPrimary, 0.34f);
                    pulse = ThemeService.ToColor(scheme.OnPrimary);
                    fg = ThemeService.ToColor(scheme.OnPrimary);
                    fgMuted = ThemeService.ToColor(scheme.OnPrimary, 0.84f);
                    break;
                case "coral":
                    bg = _hovered ? CoralBgHover : CoralBg;
                    border = CoralBorder;
                    pulse = Color.FromArgb(255, 255, 200, 180);
                    break;
                default:
                    bg = _hovered ? GoldBgHover : GoldBg;
                    border = GoldBorder;
                    pulse = Color.FromArgb(255, 255, 224, 150);
                    break;
            }
            _shell.Background = new SolidColorBrush(Color.FromArgb(Op(bg.A / 255.0), bg.R, bg.G, bg.B));
            _shell.BorderThickness = new Thickness(_config.ShowBorder ? 1 : 0);
            _shell.BorderBrush = new SolidColorBrush(border);
            _pulseOverlay.Background = new SolidColorBrush(pulse);
            ApplyTextColors(fg, fgMuted, fg, scheme, lightTaskbar);
            return;
        }

        Color bgColor, borderColor, defaultFg, defaultMuted, defaultIcon;
        bool customColorSelected = !string.IsNullOrEmpty(_config.Palette) && _config.Palette != "App";

        switch (_config.Style)
        {
            case "none":
                bgColor = _hovered
                    ? (lightTaskbar ? Color.FromArgb(26, 0, 0, 0) : Color.FromArgb(30, 255, 255, 255))
                    : Color.FromArgb(0, 0, 0, 0);
                borderColor = Color.FromArgb(0, 0, 0, 0);
                _shell.BorderThickness = new Thickness(0);
                if (_urgent)
                {
                    defaultFg = _config.UrgentStyle switch
                    {
                        "coral" => Color.FromArgb(255, 255, 138, 114),
                        "accent" => ThemeService.ToColor(scheme.Primary),
                        _ => GoldText,
                    };
                    defaultMuted = Color.FromArgb(225, defaultFg.R, defaultFg.G, defaultFg.B);
                    defaultIcon = defaultFg;
                }
                else
                {
                    defaultFg = lightTaskbar ? Color.FromArgb(255, 20, 22, 28) : Color.FromArgb(255, 248, 250, 255);
                    defaultMuted = lightTaskbar ? Color.FromArgb(200, 48, 54, 66) : Color.FromArgb(205, 208, 218, 236);
                    defaultIcon = ThemeService.ToColor(scheme.Primary);
                }
                break;

            case "glass":
                // Acrylic sits behind the window; overlay a tinted glaze and diagonal highlight.
                double glaze = _useRealBackdrop ? (_hovered ? 0.34 : 0.24) : (_hovered ? 0.56 : 0.42);
                var glassBase = customColorSelected
                    ? scheme.Primary
                    : (lightTaskbar ? Argb.Hex(0xFFF6F8FC) : Argb.Hex(0xFF1A2232));
                bgColor = Color.FromArgb(Op(glaze), glassBase.R, glassBase.G, glassBase.B);
                borderColor = lightTaskbar && !customColorSelected
                    ? Color.FromArgb(88, 255, 255, 255)
                    : Color.FromArgb(95, 255, 255, 255);
                _shell.BorderThickness = new Thickness(_config.ShowBorder ? 1 : 0);
                _glassSheen.Visibility = Visibility.Visible;
                _glassSheen.Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0.4, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = Color.FromArgb(62, 255, 255, 255), Offset = 0 },
                        new GradientStop { Color = Color.FromArgb(14, 255, 255, 255), Offset = 0.48 },
                        new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 1 },
                    },
                };
                bool lightGlassText = lightTaskbar && !customColorSelected;
                defaultFg = lightGlassText ? Color.FromArgb(255, 18, 22, 30) : Color.FromArgb(255, 250, 252, 255);
                defaultMuted = lightGlassText ? Color.FromArgb(200, 46, 52, 66) : Color.FromArgb(215, 224, 234, 248);
                defaultIcon = customColorSelected ? defaultFg : ThemeService.ToColor(scheme.Primary);
                break;

            case "accent":
                bgColor = ThemeService.ToColor(scheme.Primary, _hovered ? 0.96f : 0.86f);
                bgColor = Color.FromArgb(Op(bgColor.A / 255.0), bgColor.R, bgColor.G, bgColor.B);
                borderColor = ThemeService.ToColor(scheme.OnPrimary, 0.30f);
                _shell.BorderThickness = new Thickness(_config.ShowBorder ? 1 : 0);
                defaultFg = ThemeService.ToColor(scheme.OnPrimary);
                defaultMuted = ThemeService.ToColor(scheme.OnPrimary, 0.84f);
                defaultIcon = defaultFg;
                break;

            case "solid":
                var solidCol = customColorSelected
                    ? Argb.Lerp(lightTaskbar ? Argb.Hex(0xFFF3F5FA) : Argb.Hex(0xFF161A24), scheme.Primary, lightTaskbar ? 0.18f : 0.34f)
                    : (lightTaskbar ? Argb.Hex(0xFFF4F6FA) : Argb.Hex(0xFF1A1E28));
                bgColor = Color.FromArgb(Op(1.0), solidCol.R, solidCol.G, solidCol.B);
                borderColor = customColorSelected
                    ? ThemeService.ToColor(scheme.Primary, 0.55f)
                    : (lightTaskbar ? Color.FromArgb(46, 0, 0, 0) : Color.FromArgb(46, 255, 255, 255));
                _shell.BorderThickness = new Thickness(_config.ShowBorder ? 1 : 0);
                defaultFg = lightTaskbar ? Color.FromArgb(255, 18, 22, 28) : Color.FromArgb(255, 246, 249, 255);
                defaultMuted = lightTaskbar ? Color.FromArgb(185, 52, 60, 74) : Color.FromArgb(195, 202, 212, 232);
                defaultIcon = ThemeService.ToColor(scheme.Primary);
                break;

            default:
                if (customColorSelected)
                {
                    bgColor = Color.FromArgb(Op(_hovered ? 0.28 : 0.18), scheme.Primary.R, scheme.Primary.G, scheme.Primary.B);
                    borderColor = ThemeService.ToColor(scheme.Primary, _hovered ? 0.55f : 0.36f);
                }
                else
                {
                    bgColor = lightTaskbar
                        ? Color.FromArgb(Op(_hovered ? 0.16 : 0.06), 0, 0, 0)
                        : Color.FromArgb(Op(_hovered ? 0.18 : 0.09), 255, 255, 255);
                    borderColor = lightTaskbar
                        ? Color.FromArgb(_hovered ? (byte)38 : (byte)20, 0, 0, 0)
                        : Color.FromArgb(_hovered ? (byte)48 : (byte)24, 255, 255, 255);
                }
                _shell.BorderThickness = new Thickness(_config.ShowBorder ? 1 : 0);
                defaultFg = lightTaskbar ? Color.FromArgb(255, 20, 20, 24) : Color.FromArgb(255, 246, 248, 252);
                defaultMuted = lightTaskbar ? Color.FromArgb(195, 52, 56, 66) : Color.FromArgb(195, 206, 216, 232);
                defaultIcon = ThemeService.ToColor(scheme.Primary);
                break;
        }

        _shell.Background = new SolidColorBrush(bgColor);
        _shell.BorderBrush = new SolidColorBrush(borderColor);
        ApplyTextColors(defaultFg, defaultMuted, defaultIcon, scheme, lightTaskbar);
    }

    private void ApplyTextColors(Color defaultFg, Color defaultMuted, Color defaultIcon, ColorScheme scheme, bool lightTaskbar)
    {
        Color fg = defaultFg, muted = defaultMuted, iconCol = defaultIcon;
        switch (_config.TextColor)
        {
            case "white":
                fg = Color.FromArgb(255, 255, 255, 255);
                muted = Color.FromArgb(215, 235, 240, 250);
                iconCol = fg;
                break;
            case "black":
                fg = Color.FromArgb(255, 16, 20, 26);
                muted = Color.FromArgb(195, 42, 48, 58);
                iconCol = fg;
                break;
            case "gold":
                fg = GoldText;
                muted = Color.FromArgb(220, 238, 210, 142);
                iconCol = fg;
                break;
            case "accent":
                var accentSrc = lightTaskbar
                    ? Palettes.Scheme(_config.Palette == "App" ? App.Settings.Current.Palette : _config.Palette, false, false).Primary
                    : Palettes.Scheme(_config.Palette == "App" ? App.Settings.Current.Palette : _config.Palette, true, false).Primary;
                fg = ThemeService.ToColor(accentSrc);
                muted = ThemeService.ToColor(accentSrc, 0.84f);
                iconCol = fg;
                break;
        }
        var pb = new SolidColorBrush(fg);
        var mb = new SolidColorBrush(muted);
        _prayerText.Foreground = pb;
        _countdownText.Foreground = pb;
        _timeText.Foreground = mb;
        _prefixText.Foreground = mb;
        _icon.Brush = new SolidColorBrush(iconCol);
    }

    private static string FormatSpan(TimeSpan span, bool showSeconds, bool eastern, string lang)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        string raw;
        if (showSeconds)
        {
            raw = span.TotalHours >= 1
                ? $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}"
                : $"{span.Minutes:00}:{span.Seconds:00}";
        }
        else
        {
            int totalMins = Math.Max(0, (int)Math.Ceiling(span.TotalSeconds / 60.0));
            raw = $"{totalMins / 60:00}:{totalMins % 60:00}";
        }
        return Digits.Localize(raw, eastern, lang);
    }

    private static TextBlock MakeLine(double size, bool bold)
    {
        var tb = new TextBlock
        {
            FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.NoWrap,
            IsTextScaleFactorEnabled = false,
            LineStackingStrategy = LineStackingStrategy.MaxHeight,
        };
        Typography.SetNumeralAlignment(tb, FontNumeralAlignment.Tabular);
        return tb;
    }
}
