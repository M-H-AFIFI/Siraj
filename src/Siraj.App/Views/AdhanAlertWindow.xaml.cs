using System.Diagnostics;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Controls;
using Siraj.App.Native;
using Siraj.App.Services;
using Siraj.App.Sky;
using Siraj.Core.Adhan;
using Siraj.Core.Audio;
using Siraj.Core.Hijri;
using Siraj.Core.Prayer;
using Siraj.Core.Sky;
using Siraj.Core.Text;
using Windows.Graphics;
using Windows.UI;

namespace Siraj.App.Views;

public sealed partial class AdhanAlertWindow : Window
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static AdhanAlertWindow? _current;

    private readonly AdhanSkySpec _skySpec;
    private readonly double _born = Clock.Elapsed.TotalSeconds;
    private readonly EqualizerBars _callerEq = new(11, new SolidColorBrush(ColorHelper.FromArgb(225, 255, 255, 255))) { IsActive = true };
    private DispatcherQueueTimer? _frames;
    private DispatcherQueueTimer? _closeTimer;
    private bool _sounding = true;
    private bool _closing;
    private float _centerpieceY = 175f;

    public static void ShowFor(PrayerType prayer, DateTimeOffset time)
    {
        _current?.Close();
        _current = new AdhanAlertWindow(prayer, time);
        _current.Closed += (_, _) => _current = null;
        _current.ShowWithoutFocus();
    }

    private AdhanAlertWindow(PrayerType prayer, DateTimeOffset time)
    {
        _skySpec = AdhanSkySpec.For(prayer);
        InitializeComponent();

        var loc = Loc.Current;
        var settings = App.Settings.Current;
        bool eastern = Digits.UseEastern(settings.NumeralSystem, loc.Language);

        Root.FlowDirection = loc.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        AppWindow.Title = loc["adhan_screen_setting_title"];
        AppWindow.SetIcon(AppPaths.Asset("Siraj.ico"));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);
        StyleTitleBar();

        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.IsAlwaysOnTop = true;
            p.IsMaximizable = false;
            p.IsMinimizable = false;
            p.IsResizable = false;
        }

        var today = App.Clock.Place?.Today(time) ?? DateOnly.FromDateTime(time.LocalDateTime);
        var hijri = App.Clock.Hijri.Year == 0 ? HijriCalendars.Current.ToHijri(today) : App.Clock.Hijri;
        GregText.Text = HijriText.Gregorian(today, loc.Language, eastern);
        HijriTextBlock.Text = HijriText.Format(hijri, loc.Language, eastern);

        string? place = App.Clock.Place?.DisplayName(loc.Language);
        PlaceText.Text = place ?? "";
        PlaceText.Visibility = string.IsNullOrEmpty(place) ? Visibility.Collapsed : Visibility.Visible;

        NowText.Text = loc["adhan_screen_now"];
        NameText.Text = loc.PrayerName(new PrayerTime(prayer, time));
        NameText.Foreground = BuildTitleBrush();
        TimeText.Text = loc.Time(time);

        int iqama = PrayerCalculator.IqamaMinutes(prayer);
        if (iqama > 0)
        {
            IqamaText.Text = loc.Format("adhan_notification_iqama", loc.Time(time.AddMinutes(iqama)));
            IqamaPill.Visibility = Visibility.Visible;
        }
        else
        {
            IqamaPill.Visibility = Visibility.Collapsed;
        }

        var voice = AdhanVoices.Find(settings.AdhanVoice);
        string? portraitFile = ListeningLibrary.PortraitForAdhan(voice.Id);
        var callerAvatar = PortraitAvatar.Create(
            portraitFile != null ? $"Portraits/{portraitFile}" : null,
            voice.Name(loc.Language),
            size: 24,
            radius: 12);
        callerAvatar.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(72, 255, 255, 255));
        CallerAvatarSlot.Content = callerAvatar;
        CallerText.Text = voice.Name(loc.Language);
        CallerEqSlot.Content = _callerEq;

        StopLabel.Text = loc["adhan_screen_stop"];
        HintText.Text = loc["win_alert_hint"];
        StyleStopButton(finished: false);
        Kit.Lift(StopButton);

        Root.Loaded += OnLoaded;
        Root.SizeChanged += (_, _) => UpdateCenterpieceAnchor();
        App.Adhan.PlayingChanged += OnPlayingChanged;
        Closed += (_, _) =>
        {
            App.Adhan.PlayingChanged -= OnPlayingChanged;
            _frames?.Stop();
            _frames = null;
            _closeTimer?.Stop();
            _closeTimer = null;
            Sky.RemoveFromVisualTree();
        };
    }

    private LinearGradientBrush BuildTitleBrush()
    {
        var white = Argb.Hex(0xFFFFFFFF);
        var tint = _skySpec.IsDaytime
            ? Argb.Lerp(_skySpec.SkyTheme.OrbCore, _skySpec.SkyTheme.OrbGlow, 0.45f)
            : _skySpec.DuskUndersideGlow > 0.5f
                ? Argb.Hex(0xFFFFDFB0)
                : Argb.Hex(0xFFDBEAFE);
        return new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0, 1),
            GradientStops =
            {
                new GradientStop { Color = ThemeService.ToColor(white), Offset = 0 },
                new GradientStop { Color = ThemeService.ToColor(white), Offset = 0.55 },
                new GradientStop { Color = ThemeService.ToColor(Argb.Lerp(white, tint, 0.34f)), Offset = 1 },
            },
        };
    }

    private void StyleTitleBar()
    {
        var bar = AppWindow.TitleBar;
        var fg = ColorHelper.FromArgb(190, 255, 255, 255);
        bar.ButtonForegroundColor = fg;
        bar.ButtonInactiveForegroundColor = ColorHelper.FromArgb(130, 255, 255, 255);
        bar.ButtonBackgroundColor = Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        bar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(38, 255, 255, 255);
        bar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(62, 255, 255, 255);
        bar.ButtonHoverForegroundColor = Colors.White;
        bar.ButtonPressedForegroundColor = Colors.White;
    }

    private void StyleStopButton(bool finished)
    {
        byte restTop = finished ? (byte)32 : (byte)48;
        byte restBot = finished ? (byte)18 : (byte)28;
        byte hoverTop = finished ? (byte)48 : (byte)68;
        byte hoverBot = finished ? (byte)30 : (byte)44;
        byte pressTop = finished ? (byte)62 : (byte)84;
        byte pressBot = finished ? (byte)42 : (byte)58;
        byte borderTopA = finished ? (byte)60 : (byte)96;
        byte borderBotA = finished ? (byte)24 : (byte)36;

        static LinearGradientBrush VerticalSheen(byte topA, byte botA) => new()
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0, 1),
            GradientStops =
            {
                new GradientStop { Color = ColorHelper.FromArgb(topA, 255, 255, 255), Offset = 0 },
                new GradientStop { Color = ColorHelper.FromArgb(botA, 255, 255, 255), Offset = 1 },
            },
        };

        var bg = VerticalSheen(restTop, restBot);
        var border = VerticalSheen(borderTopA, borderBotA);

        StopButton.Background = bg;
        StopButton.BorderBrush = border;
        StopButton.Resources["ButtonBackground"] = VerticalSheen(restTop, restBot);
        StopButton.Resources["ButtonBackgroundPointerOver"] = VerticalSheen(hoverTop, hoverBot);
        StopButton.Resources["ButtonBackgroundPressed"] = VerticalSheen(pressTop, pressBot);
        StopButton.Resources["ButtonBorderBrush"] = Kit.Clone(border);
        StopButton.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(ColorHelper.FromArgb(128, 255, 255, 255));
        StopButton.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(ColorHelper.FromArgb(152, 255, 255, 255));
        StopButton.Resources["ButtonForegroundPointerOver"] = new SolidColorBrush(Colors.White);
        StopButton.Resources["ButtonForegroundPressed"] = new SolidColorBrush(Colors.White);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateCenterpieceAnchor();
        Kit.AnimateIn(Root, 0, 5, 0.992f, 130);
        Kit.Rise(TopMeta, 15, 6, 155);
        Kit.Rise(HeroStack, 35, 8, 170);
        Kit.Rise(ActionStack, 55, 8, 170);

        if (_sounding && Kit.Motion)
        {
            _frames = DispatcherQueue.CreateTimer();
            _frames.Interval = TimeSpan.FromMilliseconds(33);
            _frames.Tick += (_, _) => Sky.Invalidate();
            _frames.Start();
        }
        Sky.Invalidate();
    }

    private void UpdateCenterpieceAnchor()
    {
        if (CenterpieceSlot.ActualHeight <= 0 || Root.ActualHeight <= 0) return;
        try
        {
            var pt = CenterpieceSlot.TransformToVisual(Root)
                .TransformPoint(new Windows.Foundation.Point(0, CenterpieceSlot.ActualHeight * 0.5));
            _centerpieceY = (float)pt.Y;
            Sky.Invalidate();
        }
        catch
        {
            _centerpieceY = (float)(Root.ActualHeight * 0.30);
        }
    }

    private void ShowWithoutFocus()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Win32.GetCursorPos(out var cursor);
        var area = Win32.WorkAreaAt(cursor.X, cursor.Y);
        double scale = Win32.GetDpiForWindow(hwnd) / 96.0;
        int areaW = area.Right - area.Left, areaH = area.Bottom - area.Top;
        int w = (int)Math.Min(460 * scale, areaW * 0.92);
        int h = (int)Math.Min(648 * scale, areaH * 0.90);
        AppWindow.MoveAndResize(new RectInt32(area.Left + (areaW - w) / 2, area.Top + (areaH - h) / 2, w, h));
        AppWindow.Show(false);
        Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
    }

    private void OnPlayingChanged(PrayerType? playing)
    {
        if (playing != null) return;
        ApplyFinishedState(autoClose: true);
    }

    private void ApplyFinishedState(bool autoClose)
    {
        _sounding = false;
        _frames?.Stop();
        _callerEq.IsActive = false;
        Sky.Invalidate();

        var loc = Loc.Current;
        NowText.Text = loc["adhan_screen_finished"];
        NameText.FontSize = 30;
        NameText.Text = loc["adhan_screen_finished_body"];
        TimeText.Visibility = Visibility.Collapsed;
        CallerPill.Visibility = Visibility.Collapsed;
        StopIcon.Visibility = Visibility.Collapsed;
        StopLabel.Text = loc["adhan_screen_close"];
        HintText.Visibility = Visibility.Collapsed;
        StyleStopButton(finished: true);

        if (!autoClose || _closeTimer != null) return;
        _closeTimer = DispatcherQueue.CreateTimer();
        _closeTimer.Interval = TimeSpan.FromSeconds(20);
        _closeTimer.IsRepeating = false;
        _closeTimer.Tick += (_, _) => DismissSmoothly();
        _closeTimer.Start();
    }

    private void DismissSmoothly()
    {
        if (_closing) return;
        _closing = true;
        App.Adhan.Stop();
        Kit.AnimateOut(Root, 0, 10, 0.985f, 150, () => Close());
    }

    private void OnStop(object sender, RoutedEventArgs e) => DismissSmoothly();

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Escape or Windows.System.VirtualKey.Space or Windows.System.VirtualKey.Enter)
        {
            DismissSmoothly();
            e.Handled = true;
        }
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        float w = (float)sender.ActualWidth, h = (float)sender.ActualHeight;
        if (w < 1 || h < 1) return;
        double seconds = Clock.Elapsed.TotalSeconds - _born;
        AdhanSkyRenderer.Draw(args.DrawingSession, sender, w, h, _skySpec, _centerpieceY, seconds, _sounding, Kit.Motion);
    }
}
