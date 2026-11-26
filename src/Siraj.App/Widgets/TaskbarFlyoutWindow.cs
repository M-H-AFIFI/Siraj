using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Controls;
using Siraj.App.Native;
using Siraj.App.Services;
using Siraj.Core.Hijri;
using Siraj.Core.Prayer;
using Siraj.Core.Text;
using Windows.Graphics;
using Windows.System;
using Windows.UI;

namespace Siraj.App.Widgets;

internal sealed class TaskbarFlyoutWindow : Window
{
    private const double PopupWidth = 296;
    private static readonly Color UrgentRowBg = Color.FromArgb(236, 186, 152, 92);
    private static readonly Color UrgentRowFg = Color.FromArgb(255, 255, 252, 245);

    private readonly IntPtr _hwnd;
    private readonly Border _frame = new() { CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(14, 12, 14, 14) };
    private readonly Grid _root = new();
    private readonly StackPanel _body = new() { Spacing = 8 };
    private readonly TextBlock _titleText = new() { FontSize = 14.5, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _muteBtn = MakeIconButton("volume_up");
    private readonly Button _openBtn = MakeIconButton("open_in_new");
    private readonly Button _refreshBtn = MakeIconButton("refresh");
    private readonly Button _settingsBtn = MakeIconButton("nav_settings_outlined");
    private readonly SirajVectorIcon _muteIcon = SirajVectorIcon.Make("volume_up", 16);
    private readonly TextBlock _weekdayText = new() { FontSize = 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _hijriText = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _locationText = new() { FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.78 };
    private readonly StackPanel _rows = new() { Spacing = 3, Margin = new Thickness(0, 6, 0, 4) };
    private readonly TextBlock _verseText = new()
    {
        FontFamily = new FontFamily("ms-appx:///Assets/Fonts/Amiri-Regular.ttf#Amiri"),
        FontSize = 15,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        FlowDirection = FlowDirection.RightToLeft,
        Margin = new Thickness(4, 4, 4, 0),
        Opacity = 0.85,
    };

    private readonly TaskbarWidgetController _owner;
    private readonly Win32.SubclassProc _subclass;
    private string _prevNonSilentAlert = "azan";
    private bool _closing;
    private bool _subclassRemoved;
    private long _shownAtTick;
    private long _hiddenAtTick;
#if DEBUG
    private bool _pinnedForDebug;
#endif

    public IntPtr Handle => _hwnd;
    public bool IsShown => !_closing && AppWindow.IsVisible;
    public bool WasHiddenRecently(long ms) => Environment.TickCount64 - _hiddenAtTick < ms;

    public TaskbarFlyoutWindow(TaskbarWidgetController owner)
    {
        _owner = owner;
        Title = "Siraj";

        _titleText.FontFamily = (FontFamily)Application.Current.Resources["SirajFont"];
        _weekdayText.FontFamily = (FontFamily)Application.Current.Resources["SirajFont"];
        _hijriText.FontFamily = (FontFamily)Application.Current.Resources["SirajFont"];
        _locationText.FontFamily = (FontFamily)Application.Current.Resources["SirajFont"];

        _muteBtn.Content = _muteIcon;
        _muteBtn.Click += (_, _) => ToggleMute();
        _openBtn.Click += (_, _) => { HideFlyout(); App.ShowMainWindow("prayer"); };
        _refreshBtn.Click += (_, _) => { App.Clock.Recompute(); Refresh(); };
        _settingsBtn.Click += (_, _) => { HideFlyout(); App.ShowMainWindow("widgets"); };

        var header = new Grid { ColumnSpacing = 4, Margin = new Thickness(2, 0, 2, 4) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(_titleText);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(_settingsBtn);
        actions.Children.Add(_refreshBtn);
        actions.Children.Add(_openBtn);
        actions.Children.Add(_muteBtn);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);

        var meta = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        meta.Children.Add(_weekdayText);
        meta.Children.Add(_hijriText);
        meta.Children.Add(_locationText);

        _body.Children.Add(header);
        _body.Children.Add(meta);
        _body.Children.Add(_rows);
        _body.Children.Add(_verseText);

        _frame.Child = _body;
        _root.Children.Add(_frame);
        _root.KeyDown += (_, e) => { if (e.Key == VirtualKey.Escape) HideFlyout(); };
        Content = _root;
        App.Theme.Register(_root);

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        try { AppWindow.IsShownInSwitchers = false; } catch { }

        _subclass = OnMessage;
        Win32.SetWindowSubclass(_hwnd, _subclass, 3, 0);

        long ex = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE).ToInt64();
        ex = (ex | Win32.WS_EX_TOOLWINDOW) & ~Win32.WS_EX_APPWINDOW;
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex));
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_STYLE, Win32.GetWindowLongPtr(_hwnd, Win32.GWL_STYLE));
        Win32.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);

        if (DesktopAcrylicController.IsSupported())
            SystemBackdrop = new DesktopAcrylicBackdrop();
        int corner = Win32.DWMWCP_ROUND;
        Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        int border = Win32.DWMWA_COLOR_NONE;
        Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_BORDER_COLOR, ref border, sizeof(int));

        Activated += (_, e) =>
        {
#if DEBUG
            if (_pinnedForDebug) return;
#endif
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                // Ignore transient deactivation events fired while the popup is still taking focus.
                if (Environment.TickCount64 - _shownAtTick < 250) return;
                HideFlyout();
            }
        };
        App.Clock.Tick += OnTick;
        App.Clock.DayChanged += OnTick;
        App.Theme.Changed += OnThemeChanged;
        if (App.Adhan != null) App.Adhan.PlayingChanged += OnAdhanChanged;
        Closed += (_, _) =>
        {
            _closing = true;
            App.Clock.Tick -= OnTick;
            App.Clock.DayChanged -= OnTick;
            App.Theme.Changed -= OnThemeChanged;
            if (App.Adhan != null) App.Adhan.PlayingChanged -= OnAdhanChanged;
        };

        Refresh();
    }

    private void OnTick()
    {
        if (IsShown) Refresh();
    }

    private void OnThemeChanged() => ApplyTheme();
    private void OnAdhanChanged(PrayerType? _) => DispatcherQueue.TryEnqueue(Refresh);

    public void ShowNear(RectInt32 anchorRect, Win32.RECT workRect)
    {
        _shownAtTick = Environment.TickCount64;
        Refresh();
        ApplyTheme();

        double scale = Win32.GetDpiForWindow(_hwnd) / 96.0;
        if (scale <= 0) scale = 1.0;

        _root.Measure(new Windows.Foundation.Size(PopupWidth, 900));
        double hDip = Math.Clamp(_root.DesiredSize.Height, 320, 520);
        int pw = (int)Math.Ceiling(PopupWidth * scale);
        int ph = (int)Math.Ceiling(hDip * scale);
        int gap = (int)Math.Ceiling(10 * scale);

        int x = anchorRect.X + (anchorRect.Width - pw) / 2;
        x = Math.Clamp(x, workRect.Left + gap, Math.Max(workRect.Left + gap, workRect.Right - pw - gap));
        int y = anchorRect.Y - ph - gap;
        if (y < workRect.Top + gap) y = anchorRect.Y + anchorRect.Height + gap;

        AppWindow.MoveAndResize(new RectInt32(x, y, pw, ph));
        AppWindow.Show(true);
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, x, y, pw, ph, Win32.SWP_SHOWWINDOW);
        Activate();
        Win32.SetForegroundWindow(_hwnd);
        Kit.AnimateIn(_frame, 0, 6, 1f, 110);
    }

    public void HideFlyout()
    {
        if (_closing) return;
        _hiddenAtTick = Environment.TickCount64;
        AppWindow.Hide();
    }

    public void Refresh()
    {
        if (_closing) return;
        var loc = Loc.Current;
        var cfg = App.Settings.Current.TaskbarWidget;
        var clock = App.Clock;
        var now = clock.Now;
        var day = clock.Day;
        bool eastern = Digits.UseEastern(App.Settings.Current.NumeralSystem, loc.Language);
        _root.FlowDirection = loc.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        _titleText.Text = loc["win_nav_prayer"];
        ToolTipService.SetToolTip(_settingsBtn, loc["win_taskbar_customize"]);
        ToolTipService.SetToolTip(_refreshBtn, loc["win_taskbar_refresh"]);
        ToolTipService.SetToolTip(_openBtn, loc["win_tray_open"]);

        bool muted = App.Settings.Current.AlertType == "silent";
        _muteIcon.IconKey = muted ? "volume_off" : "volume_up";
        ToolTipService.SetToolTip(_muteBtn, loc[muted ? "win_taskbar_unmute" : "win_taskbar_mute"]);

        var today = clock.Place?.Today(now) ?? DateOnly.FromDateTime(DateTime.Now);
        _weekdayText.Text = today.ToDateTime(TimeOnly.MinValue).ToString("dddd", loc.Culture);

        var hijri = clock.Hijri.Year == 0 ? HijriCalendars.Current.ToHijri(today) : clock.Hijri;
        _hijriText.Text = HijriText.Format(hijri, loc.Language, eastern);
        _hijriText.Visibility = cfg.ShowFlyoutHijri ? Visibility.Visible : Visibility.Collapsed;

        _locationText.Text = clock.Place?.DisplayName(loc.Language) ?? "";
        _locationText.Visibility = string.IsNullOrEmpty(_locationText.Text) ? Visibility.Collapsed : Visibility.Visible;

        _verseText.Text = loc["win_taskbar_flyout_verse_text"];
        _verseText.Visibility = cfg.ShowFlyoutVerse ? Visibility.Visible : Visibility.Collapsed;

        _rows.Children.Clear();
        if (day != null)
        {
            var state = clock.State ?? PrayerState.At(day, now);
            var activePrayer = state switch
            {
                PrayerState.AzanCalled a => a.Prayer,
                PrayerState.WaitingForIqama w => w.Prayer,
                PrayerState.InPrayer p => p.Prayer,
                PrayerState.UpcomingAzan u => u.Prayer,
                _ => day.Next,
            };
            bool isPlaying = App.Adhan?.IsPlaying == true;
            bool isUrgent = false;
            if (activePrayer != null && !_owner.IsUrgentDismissed(day.Date, activePrayer.Type))
            {
                if (state is PrayerState.AzanCalled or PrayerState.WaitingForIqama || isPlaying)
                    isUrgent = true;
                else if (state is PrayerState.UpcomingAzan u && cfg.UrgentMinutes > 0 && u.Remaining.TotalMinutes is > 0 and <= 60 && u.Remaining.TotalMinutes <= cfg.UrgentMinutes)
                    isUrgent = true;
            }

            foreach (var p in day.Times)
            {
                bool isNext = activePrayer?.Type == p.Type;
                bool isPassed = !isNext && p.Time < now;
                _rows.Children.Add(BuildPrayerRow(p, isPassed, isNext, isNext && isUrgent, isNext && isPlaying, day.Date));
            }
        }

        ApplyTheme();
    }

    private UIElement BuildPrayerRow(PrayerTime p, bool passed, bool active, bool urgent, bool playing, DateOnly date)
    {
        var loc = Loc.Current;
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new SirajVectorIcon
        {
            IconKey = playing ? "notifications" : urgent ? "warning" : passed ? "check" : "schedule",
            Width = 15,
            Height = 15,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = passed ? 0.55 : 0.92,
        };
        var name = new TextBlock
        {
            Text = loc.PrayerName(p),
            FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
            FontSize = 13,
            FontWeight = active ? FontWeights.Bold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = passed ? 0.65 : 1.0,
        };
        var time = new TextBlock
        {
            Text = loc.Time(p.Time),
            FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
            FontSize = 12.5,
            FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = passed ? 0.65 : 0.95,
        };
        Typography.SetNumeralAlignment(time, FontNumeralAlignment.Tabular);

        row.Children.Add(icon);
        Grid.SetColumn(name, 1);
        row.Children.Add(name);

        if (playing || urgent)
        {
            var action = new Button
            {
                Content = playing ? loc["adhan_screen_stop"] : loc["win_taskbar_dismiss"],
                Padding = new Thickness(8, 2, 8, 2),
                MinHeight = 0,
                MinWidth = 0,
                CornerRadius = new CornerRadius(6),
                FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromArgb(46, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(UrgentRowFg),
                VerticalAlignment = VerticalAlignment.Center,
            };
            action.Click += (_, _) =>
            {
                if (playing) App.Adhan.Stop();
                else _owner.DismissUrgent(date, p.Type);
                Refresh();
            };
            Grid.SetColumn(action, 2);
            row.Children.Add(action);
        }

        Grid.SetColumn(time, 3);
        row.Children.Add(time);

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6.5, 10, 6.5),
            Child = row,
        };

        if (urgent || playing)
        {
            var cfg = App.Settings.Current.TaskbarWidget;
            var tbScheme = TaskbarPillView.ResolveScheme(cfg, App.Theme.IsDark);
            Color rowBg = cfg.UrgentStyle switch
            {
                "coral" => Color.FromArgb(236, 198, 72, 52),
                "accent" => ThemeService.ToColor(tbScheme.Primary, 0.90f),
                _ => UrgentRowBg,
            };
            Color rowFg = cfg.UrgentStyle == "accent" ? ThemeService.ToColor(tbScheme.OnPrimary) : UrgentRowFg;
            border.Background = new SolidColorBrush(rowBg);
            var fg = new SolidColorBrush(rowFg);
            icon.Brush = fg;
            name.Foreground = fg;
            time.Foreground = fg;
        }
        else if (active)
        {
            var scheme = TaskbarPillView.ResolveScheme(App.Settings.Current.TaskbarWidget, App.Theme.IsDark);
            border.Background = new SolidColorBrush(ThemeService.ToColor(scheme.Primary, 0.20f));
            border.BorderBrush = new SolidColorBrush(ThemeService.ToColor(scheme.Primary, 0.42f));
            border.BorderThickness = new Thickness(1);
            var fg = new SolidColorBrush(ThemeService.ToColor(App.Theme.Scheme.OnSurface));
            icon.Brush = new SolidColorBrush(ThemeService.ToColor(scheme.Primary));
            name.Foreground = fg;
            time.Foreground = fg;
        }
        else
        {
            var fg = new SolidColorBrush(ThemeService.ToColor(App.Theme.Scheme.OnSurface));
            icon.Brush = fg;
            name.Foreground = fg;
            time.Foreground = fg;
        }

        return border;
    }

    private void ToggleMute()
    {
        var cur = App.Settings.Current.AlertType;
        if (cur == "silent")
        {
            App.Settings.Update(s => s with { AlertType = _prevNonSilentAlert });
        }
        else
        {
            _prevNonSilentAlert = cur;
            App.Settings.Update(s => s with { AlertType = "silent" });
            if (App.Adhan.IsPlaying) App.Adhan.Stop();
        }
        Refresh();
    }

    private void ApplyTheme()
    {
        var scheme = App.Theme.Scheme;
        bool dark = App.Theme.IsDark;
        _frame.Background = new SolidColorBrush(ThemeService.ToColor(scheme.Surface, dark ? 0.80f : 0.88f));
        _frame.BorderBrush = new SolidColorBrush(dark ? Color.FromArgb(46, 255, 255, 255) : Color.FromArgb(36, 0, 0, 0));
        var onSurface = new SolidColorBrush(ThemeService.ToColor(scheme.OnSurface));
        var muted = new SolidColorBrush(ThemeService.ToColor(scheme.OnSurfaceVariant));
        _titleText.Foreground = onSurface;
        _weekdayText.Foreground = onSurface;
        _hijriText.Foreground = muted;
        _locationText.Foreground = muted;
        _verseText.Foreground = onSurface;

        bool mutedAlert = App.Settings.Current.AlertType == "silent";
        _muteIcon.Brush = mutedAlert ? new SolidColorBrush(Color.FromArgb(255, 234, 179, 8)) : onSurface;
        foreach (var btn in new[] { _settingsBtn, _refreshBtn, _openBtn })
        {
            if (btn.Content is SirajVectorIcon vi) vi.Brush = onSurface;
        }
    }

    private IntPtr OnMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, nuint data)
    {
        if (msg == Win32.WM_STYLECHANGING)
        {
            var result = Win32.DefSubclassProc(hwnd, msg, wParam, lParam);
            unsafe
            {
                var change = (Win32.STYLESTRUCT*)lParam;
                if (wParam.ToInt32() == Win32.GWL_STYLE)
                    change->styleNew = (uint)((change->styleNew & ~(Win32.WS_CAPTION | Win32.WS_THICKFRAME | Win32.WS_SYSMENU)) | Win32.WS_POPUP);
                else if (wParam.ToInt32() == Win32.GWL_EXSTYLE)
                    change->styleNew = (uint)(change->styleNew & ~(Win32.WS_EX_WINDOWEDGE | Win32.WS_EX_DLGMODALFRAME));
            }
            return result;
        }
        if (msg == Win32.WM_NCDESTROY)
        {
            if (!_subclassRemoved)
            {
                _subclassRemoved = true;
                Win32.RemoveWindowSubclass(hwnd, _subclass, 3);
            }
            return Win32.DefSubclassProc(hwnd, msg, wParam, lParam);
        }
        return Win32.DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    private static Button MakeIconButton(string iconKey) => new()
    {
        Content = SirajVectorIcon.Make(iconKey, 16),
        Width = 30,
        Height = 30,
        Padding = new Thickness(0),
        CornerRadius = new CornerRadius(8),
        Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
        BorderThickness = new Thickness(0),
    };

#if DEBUG
    internal void ShowDebugShot(string path)
    {
        _pinnedForDebug = true;
        Refresh();
        ApplyTheme();
        double scale = Win32.GetDpiForWindow(_hwnd) / 96.0;
        if (scale <= 0) scale = 1.0;
        _root.Measure(new Windows.Foundation.Size(PopupWidth, 900));
        double hDip = Math.Clamp(_root.DesiredSize.Height, 340, 520);
        AppWindow.MoveAndResize(new RectInt32(200, 200, (int)Math.Ceiling(PopupWidth * scale), (int)Math.Ceiling(hDip * scale)));
        AppWindow.Show(true);
        Activate();
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(900);
        timer.IsRepeating = false;
        timer.Tick += async (_, _) =>
        {
            try
            {
                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
                await bitmap.RenderAsync(_root);
                var pixels = await bitmap.GetPixelsAsync();
                if (File.Exists(path)) File.Delete(path);
                using var fs = File.Create(path);
                using var stream = fs.AsRandomAccessStream();
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(pixels));
                await encoder.FlushAsync();
            }
            catch (Exception ex) { Log.Error("taskbar flyout shot", ex); }
            App.Quit();
        };
        timer.Start();
    }
#endif
}
