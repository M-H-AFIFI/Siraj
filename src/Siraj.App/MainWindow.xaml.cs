using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Siraj.App.Controls;
using Siraj.App.Native;
using Siraj.App.Services;
using Windows.Graphics;

namespace Siraj.App.Views;

public sealed partial class MainWindow : Window
{
    private sealed record NavSpec(string Tag, string LabelKey, string OutlineIcon, string FilledIcon, bool IsFooter = false);

    private static readonly NavSpec[] PrimaryNav =
    [
        new("prayer", "nav_prayer", "nav_prayer", "nav_prayer_filled"),
        new("quran", "nav_quran", "nav_quran", "nav_quran_filled"),
        new("listen", "quick_listen", "quick_listen", "quick_listen"),
        new("worship", "nav_azkar", "nav_worship", "nav_worship_filled"),
    ];

    private static readonly NavSpec[] FooterNav =
    [
        new("widgets", "win_nav_widgets", "nav_widgets_outlined", "nav_widgets_filled", IsFooter: true),
        new("settings", "settings_title", "nav_settings_outlined", "nav_settings", IsFooter: true),
    ];

    private string _page = "prayer";
    private bool _compact;
    private bool _pillVisible;
#if DEBUG
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _shotTimer;
#endif
    private readonly Dictionary<string, (Button Btn, SirajVectorIcon Icon, TextBlock Label, EqualizerBars? Eq, NavSpec Spec, int PrimaryIndex)> _navButtons = new();

    public static bool IsActive { get; private set; }
    public static event Action? ActiveChanged;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.SetIcon(AppPaths.Asset("Siraj.ico"));
        AppWindow.Title = "Siraj";
        if (MicaController.IsSupported()) SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 640;
            presenter.PreferredMinimumHeight = 580;
        }
        PlaceWindow();

        BrandBadge.Child = SirajVectorIcon.Make("emblem", 22, Kit.B("SirajOnPrimaryBrush"));
        NavDock.Shadow = new ThemeShadow();
        NavDock.Translation = new Vector3(0, 0, 16);
        ElementCompositionPreview.SetIsTranslationEnabled(ActivePill, true);
        ElementCompositionPreview.SetIsTranslationEnabled(Shell, true);
        ElementCompositionPreview.SetIsTranslationEnabled(SetupFrame, true);
        ContentFrame.CacheSize = 10;
        MiniPlayer.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(140) };

        MiniPlayer.ExpandRequested += () => FullPlayer.Open();
        FullPlayer.OpenChanged += open =>
        {
            MiniPlayer.Opacity = open ? 0 : 1;
            MiniPlayer.IsHitTestVisible = !open;
        };

        App.Theme.Register(Root);
        App.Theme.Changed += OnThemeChanged;
        App.Clock.DayChanged += UpdateBrandSubtitle;
        App.Media.Changed += UpdateNavLiveIndicators;
        App.Updates.StateChanged += RefreshUpdateBanner;
        OnThemeChanged();
        AppWindow.Closing += OnClosing;
        Root.SizeChanged += (_, e) => UpdateCompact(e.NewSize.Width);
        Activated += (_, e) =>
        {
            IsActive = e.WindowActivationState != WindowActivationState.Deactivated;
            ActiveChanged?.Invoke();
        };
        Closed += (_, _) =>
        {
            App.Theme.Changed -= OnThemeChanged;
            App.Clock.DayChanged -= UpdateBrandSubtitle;
            App.Media.Changed -= UpdateNavLiveIndicators;
            App.Updates.StateChanged -= RefreshUpdateBanner;
            IsActive = false;
            ActiveChanged?.Invoke();
        };
#if DEBUG
        if (App.StartArgs.FirstOrDefault(a => a.StartsWith("--page=")) is { } initialPage)
            _page = initialPage["--page=".Length..];
#endif
        Rebuild();
#if DEBUG
        DebugHooks();
#endif
    }

    internal void OpenFullPlayer() => FullPlayer.Open();

    private void UpdateCompact(double width)
    {
        bool want = width < 900;
        if (want == _compact) return;
        _compact = want;
        NavDock.Width = _compact ? 72 : 216;
        BrandLabels.Visibility = _compact ? Visibility.Collapsed : Visibility.Visible;
        BrandHeader.Padding = _compact ? new Thickness(6, 4, 6, 14) : new Thickness(8, 4, 8, 16);
        foreach (var item in _navButtons.Values)
        {
            item.Label.Visibility = _compact ? Visibility.Collapsed : Visibility.Visible;
            if (item.Eq != null) item.Eq.Visibility = !_compact && App.Media.IsPlaying ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void PlaceWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double scale = Win32.GetDpiForWindow(hwnd) / 96.0;
        var area = Win32.WorkAreaOf(hwnd);
        int areaW = area.Right - area.Left, areaH = area.Bottom - area.Top;
        int w = (int)Math.Min(1240 * scale, areaW * 0.9), h = (int)Math.Min(840 * scale, areaH * 0.9);
        AppWindow.MoveAndResize(new RectInt32(area.Left + (areaW - w) / 2, area.Top + (areaH - h) / 2, w, h));
    }

    private void OnThemeChanged()
    {
        var bar = AppWindow.TitleBar;
        var fg = ThemeService.ToColor(App.Theme.Scheme.OnSurface);
        bar.ButtonForegroundColor = fg;
        bar.ButtonInactiveForegroundColor = ThemeService.ToColor(App.Theme.Scheme.OnSurfaceVariant);
        bar.ButtonBackgroundColor = Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        bar.ButtonHoverBackgroundColor = ThemeService.ToColor(App.Theme.Scheme.OnSurface, 0.08f);
        bar.ButtonPressedBackgroundColor = ThemeService.ToColor(App.Theme.Scheme.OnSurface, 0.14f);
        bar.ButtonHoverForegroundColor = fg;
        bar.ButtonPressedForegroundColor = fg;

        var bg = App.Theme.Scheme.Background;
        bool liveOrGlass = App.Settings.Current.LiveBackground || App.Settings.Current.GlassUi;
        Root.Background = new SolidColorBrush(liveOrGlass
            ? ThemeService.ToColor(bg)
            : ThemeService.ToColor(bg, SystemBackdrop != null && !(App.Settings.Current.BlackMode && App.Theme.IsDark) ? 0.88f : 1f));
        Backdrop.Visibility = liveOrGlass ? Visibility.Visible : Visibility.Collapsed;
        UpdateNavVisuals(animatePill: false);
    }

    public void Rebuild()
    {
        var loc = Loc.Current;
        Root.FlowDirection = loc.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        TitleText.Text = loc["app_name"];
        AppWindow.Title = loc["app_name"];
        BrandTitle.Text = loc["app_name"];
        UpdateBrandSubtitle();
        BuildNavRail();

        if (!App.Settings.Current.SetupDone)
        {
            Shell.Visibility = Visibility.Collapsed;
            SetupFrame.Visibility = Visibility.Visible;
            SetupFrame.Navigate(typeof(SetupPage));
            Kit.AnimateIn(SetupFrame, 0, 8, 0.992f, 150);
            return;
        }
        SetupFrame.Visibility = Visibility.Collapsed;
        SetupFrame.Content = null;
        Shell.Visibility = Visibility.Visible;
        // Flush cached pages when rebuilding (e.g., language change) so every tab re-localizes cleanly.
        ContentFrame.CacheSize = 0;
        ContentFrame.CacheSize = 10;
        RefreshUpdateBanner();
        if (_page == "reader" && App.Settings.Current.LastRead is { } last) OpenReader(new ReaderArgs(last.Page));
        else Navigate(_page, force: true);
    }

    private void RefreshUpdateBanner()
    {
        if (App.Updates == null) return;
        var snap = App.Updates.Snapshot;
        var loc = Loc.Current;

        if (snap.JustUpdated && snap.State == UpdateState.Idle)
        {
            var row = BuildBannerShell("check",
                loc.Format("win_update_completed", $"v{loc.Digitize(App.Updates.CurrentVersion.ToString())}"),
                null,
                null,
                dismissAction: () => App.Updates.DismissBanner());
            UpdateBanner.Child = row;
            UpdateBanner.Visibility = Visibility.Visible;
            return;
        }

        if (snap.BannerDismissed && snap.State == UpdateState.Available)
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
            return;
        }

        switch (snap.State)
        {
            case UpdateState.Available when snap.Release is { } rel:
            {
                string ver = $"v{loc.Digitize(rel.Version.ToString())}";
                string? firstNote = rel.Notes.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
                string subtitle = string.IsNullOrEmpty(firstNote) ? loc["win_update_available_body"] : firstNote;
                var action = Ui.Button(loc["win_update_now"], () => _ = App.Updates.DownloadAndInstallAsync(), primary: true);
                UpdateBanner.Child = BuildBannerShell("system_update",
                    loc.Format("win_update_available_title", ver),
                    subtitle,
                    action,
                    dismissAction: () => App.Updates.DismissBanner());
                UpdateBanner.Visibility = Visibility.Visible;
                break;
            }
            case UpdateState.Downloading when snap.Release is { } rel:
            {
                string ver = $"v{loc.Digitize(rel.Version.ToString())}";
                string pct = snap.TotalBytes > 0
                    ? loc.Digitize($"{(int)Math.Round(snap.Progress * 100)}%")
                    : "";
                var bar = new ProgressBar
                {
                    Minimum = 0, Maximum = 100, Value = snap.Progress * 100,
                    IsIndeterminate = snap.TotalBytes <= 0,
                    Margin = new Thickness(0, 6, 0, 0),
                };
                var cancel = Ui.Button(loc["win_update_cancel"], () => App.Updates.CancelDownload());
                UpdateBanner.Child = BuildBannerShell("system_update",
                    loc.Format("win_update_downloading", ver, pct),
                    null,
                    cancel,
                    extraMiddle: bar);
                UpdateBanner.Visibility = Visibility.Visible;
                break;
            }
            case UpdateState.Verifying:
            {
                var bar = new ProgressBar { IsIndeterminate = true, Margin = new Thickness(0, 6, 0, 0) };
                UpdateBanner.Child = BuildBannerShell("system_update", loc["win_update_verifying"], null, null, extraMiddle: bar);
                UpdateBanner.Visibility = Visibility.Visible;
                break;
            }
            case UpdateState.ReadyToInstall when snap.Release is { } rel:
            {
                string ver = $"v{loc.Digitize(rel.Version.ToString())}";
                var install = Ui.Button(loc["win_update_install_restart"], () => App.Updates.InstallNow(), primary: true);
                UpdateBanner.Child = BuildBannerShell("system_update",
                    loc.Format("win_update_ready_title", ver),
                    loc["win_update_available_body"],
                    install);
                UpdateBanner.Visibility = Visibility.Visible;
                break;
            }
            case UpdateState.Installing:
            {
                var ring = new ProgressRing { Width = 20, Height = 20, IsActive = true };
                UpdateBanner.Child = BuildBannerShell("system_update", loc["win_update_installing"], null, ring);
                UpdateBanner.Visibility = Visibility.Visible;
                break;
            }
            default:
                UpdateBanner.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private static Grid BuildBannerShell(string iconKey, string title, string? subtitle, FrameworkElement? action,
        FrameworkElement? extraMiddle = null, Action? dismissAction = null)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(12),
            Background = Kit.B("SirajPrimarySoftBrush"),
            BorderBrush = Kit.B("SirajPrimaryBorderBrush"),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Kit.Icon(iconKey, 18, Kit.B("SirajPrimaryBrush")),
        };
        grid.Children.Add(badge);

        var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        var titleTb = Kit.Text(title, "SirajHeading", 14, bold: true);
        titleTb.TextTrimming = TextTrimming.CharacterEllipsis;
        titleTb.MaxLines = 1;
        mid.Children.Add(titleTb);
        if (!string.IsNullOrEmpty(subtitle))
        {
            var subTb = Kit.Text(subtitle, "SirajCaption", 12.5);
            subTb.TextTrimming = TextTrimming.CharacterEllipsis;
            subTb.MaxLines = 1;
            mid.Children.Add(subTb);
        }
        if (extraMiddle != null) mid.Children.Add(extraMiddle);
        Grid.SetColumn(mid, 1);
        grid.Children.Add(mid);

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (action != null) right.Children.Add(action);
        if (dismissAction != null)
        {
            var close = new Button
            {
                Content = Kit.Icon("close", 15, Kit.B("SirajOnSurfaceVariantBrush")),
                Width = 30, Height = 30, Padding = new Thickness(0),
                CornerRadius = new CornerRadius(15),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
            };
            close.Click += (_, _) => dismissAction();
            right.Children.Add(close);
        }
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return grid;
    }

    private void UpdateBrandSubtitle()
    {
        var loc = Loc.Current;
        var place = App.Clock.Place;
        BrandSub.Text = place?.DisplayName(loc.Language) ?? loc["dashboard_greeting"];
    }

    private void BuildNavRail()
    {
        var loc = Loc.Current;
        NavList.Children.Clear();
        FooterNavList.Children.Clear();
        _navButtons.Clear();

        for (int i = 0; i < PrimaryNav.Length; i++)
            AddNavItem(PrimaryNav[i], i, NavList);
        foreach (var spec in FooterNav)
            AddNavItem(spec, -1, FooterNavList);

        UpdateCompact(Root.ActualWidth > 0 ? Root.ActualWidth : 1200);
        UpdateNavVisuals(animatePill: false);
    }

    private void AddNavItem(NavSpec spec, int primaryIndex, StackPanel host)
    {
        var loc = Loc.Current;
        string text = loc[spec.LabelKey];
        var icon = SirajVectorIcon.Make(spec.OutlineIcon, 21, Kit.B("SirajOnSurfaceVariantBrush"));
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
        var label = new TextBlock
        {
            Text = text, FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
            FontSize = 14, VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1,
        };
        EqualizerBars? eq = spec.Tag == "listen" ? new EqualizerBars(12) { Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center } : null;

        var row = new Grid { ColumnSpacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(icon);
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        if (eq != null) { Grid.SetColumn(eq, 2); row.Children.Add(eq); }

        var btn = new Button
        {
            Content = row, Height = 44, Padding = new Thickness(12, 0, 12, 0),
            CornerRadius = new CornerRadius(16),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            AllowFocusOnInteraction = false,
            UseSystemFocusVisuals = false,
        };
        ToolTipService.SetToolTip(btn, text);
        AutomationProperties.SetName(btn, text);
        btn.Click += (_, _) => Navigate(spec.Tag);
        host.Children.Add(btn);
        _navButtons[spec.Tag] = (btn, icon, label, eq, spec, primaryIndex);
    }

    private string ActiveRailTag() => _page switch
    {
        "reader" or "khatma" => "quran",
        "radio" => "listen",
        "adhkar" or "tasbih" or "hadith" or "names" or "fasting" or "zakat" or "calendar" => "worship",
        _ => _navButtons.ContainsKey(_page) ? _page : "prayer",
    };

    private static bool IsSubpage(string page) => page is
        "reader" or "khatma" or "radio" or "adhkar" or "tasbih" or "hadith" or "names" or "fasting" or "zakat" or "calendar";

    private int PrimaryIndexOf(string page)
    {
        string tag = page switch
        {
            "reader" or "khatma" => "quran",
            "radio" => "listen",
            "adhkar" or "tasbih" or "hadith" or "names" or "fasting" or "zakat" or "calendar" => "worship",
            _ => page,
        };
        for (int i = 0; i < PrimaryNav.Length; i++)
            if (PrimaryNav[i].Tag == tag) return i;
        return -1;
    }

    private void UpdateNavVisuals(bool animatePill)
    {
        string activeTag = ActiveRailTag();
        int primaryIndex = -1;
        SirajVectorIcon? activeIcon = null;
        var hoverColor = ThemeService.ToColor(App.Theme.Scheme.OnSurface, 0.055f);
        var pressedColor = ThemeService.ToColor(App.Theme.Scheme.OnSurface, 0.095f);

        foreach (var (tag, item) in _navButtons)
        {
            bool active = tag == activeTag;
            if (active)
            {
                activeIcon = item.Icon;
                if (item.PrimaryIndex >= 0) primaryIndex = item.PrimaryIndex;
            }
            var fg = Kit.B(active ? "SirajPrimaryBrush" : "SirajOnSurfaceVariantBrush");
            item.Label.Foreground = fg;
            item.Label.FontWeight = active ? FontWeights.Bold : FontWeights.Medium;
            item.Icon.IconKey = active ? item.Spec.FilledIcon : item.Spec.OutlineIcon;
            item.Icon.Brush = fg;

            // Keep hover subtle and transparent on the active item so it never clashes with ActivePill.
            item.Btn.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(active ? Colors.Transparent : hoverColor);
            item.Btn.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(active ? Colors.Transparent : pressedColor);

            // Footer items carry their own tinted highlight since the sliding pill spans PrimaryNav.
            if (item.Spec.IsFooter)
            {
                item.Btn.Background = active ? Kit.Clone("SirajPrimarySoftBrush") : new SolidColorBrush(Colors.Transparent);
                item.Btn.BorderBrush = active ? Kit.Clone("SirajPrimaryBorderBrush") : new SolidColorBrush(Colors.Transparent);
                item.Btn.BorderThickness = new Thickness(active ? 1 : 0);
            }
        }

        if (animatePill && activeIcon != null)
            Kit.Pulse(activeIcon, 1.08f);

        if (primaryIndex >= 0)
        {
            ActivePill.Visibility = Visibility.Visible;
            float targetY = primaryIndex * 48f; // 44px button + 4px stack spacing
            var target = new Vector3(0, targetY, 0);
            if (!animatePill || !Kit.Motion || !_pillVisible)
            {
                var visual = ElementCompositionPreview.GetElementVisual(ActivePill);
                visual.StopAnimation("Translation");
                visual.Properties.InsertVector3("Translation", target);
                Kit.FadeTo(ActivePill, 1f, animatePill ? 80 : 0);
            }
            else
            {
                Kit.GlideTo(ActivePill, target, 160);
                Kit.FadeTo(ActivePill, 1f, 70);
            }
            _pillVisible = true;
        }
        else
        {
            if (animatePill && _pillVisible && Kit.Motion)
                Kit.FadeTo(ActivePill, 0f, 80);
            else
                ActivePill.Visibility = Visibility.Collapsed;
            _pillVisible = false;
        }
        UpdateNavLiveIndicators();
    }

    private void UpdateNavLiveIndicators()
    {
        if (_navButtons.TryGetValue("listen", out var item) && item.Eq != null)
        {
            bool playing = App.Media.IsPlaying;
            item.Eq.IsActive = playing;
            item.Eq.Visibility = playing && !_compact ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    internal void SetupFinished()
    {
        _page = "prayer";
        Kit.AnimateOut(SetupFrame, 0, -8, 0.99f, 110, () =>
        {
            Rebuild();
            Kit.AnimateIn(Shell, 0, 10, 0.992f, 170);
        });
    }

    public void Open(string? page)
    {
        if (page != null && App.Settings.Current.SetupDone) Navigate(page);
        AppWindow.Show(true);
        if (AppWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Minimized) p.Restore(true);
        Activate();
        Win32.SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    internal void Navigate(string page, bool force = false, bool drillIn = false)
    {
        if (!force && page == _page && ContentFrame.Content != null) return;
        string prev = _page;
        bool hadContent = ContentFrame.Content != null;
        _page = page;
        var target = page switch
        {
            "settings" => typeof(SettingsPage),
            "widgets" => typeof(WidgetsPage),
            "quran" => typeof(QuranPage),
            "khatma" => typeof(KhatmaPage),
            "listen" => typeof(ListenPage),
            "radio" => typeof(RadioPage),
            "worship" => typeof(WorshipPage),
            "adhkar" => typeof(AdhkarPage),
            "tasbih" => typeof(TasbihPage),
            "hadith" => typeof(HadithPage),
            "names" => typeof(NamesPage),
            "fasting" => typeof(FastingPage),
            "zakat" => typeof(ZakatPage),
            "calendar" => typeof(CalendarPage),
            _ => typeof(PrayerPage),
        };

        // Start sliding the rail pill immediately before Frame navigation so feedback is instantaneous.
        UpdateNavVisuals(animatePill: hadContent);

        PageTransitionKind kind = PageTransitionKind.None;
        if (hadContent)
        {
            if (drillIn || (!IsSubpage(prev) && IsSubpage(page)))
                kind = PageTransitionKind.DrillIn;
            else if (IsSubpage(prev) && !IsSubpage(page))
                kind = PageTransitionKind.Back;
            else
            {
                int oldIdx = PrimaryIndexOf(prev);
                int newIdx = PrimaryIndexOf(page);
                kind = oldIdx >= 0 && newIdx >= 0 && oldIdx != newIdx
                    ? (newIdx > oldIdx ? PageTransitionKind.TabDown : PageTransitionKind.TabUp)
                    : PageTransitionKind.Fade;
            }
        }

        ContentFrame.Navigate(target, null, new SuppressNavigationTransitionInfo());
        if (kind != PageTransitionKind.None && ContentFrame.Content is UIElement nextPage)
            Kit.AnimatePageIn(nextPage, kind, Loc.Current.IsRtl);

        Backdrop.Paused = false;
        MiniPlayer.SuppressForReader = false;
        MiniPlayer.Refresh();
    }

    internal void OpenReader(ReaderArgs args)
    {
        if (App.Quran.Status != QuranService.State.Ready) { Navigate("quran", force: true); return; }
        _page = "reader";
        UpdateNavVisuals(animatePill: true);
        ContentFrame.Navigate(typeof(ReaderPage), args, new SuppressNavigationTransitionInfo());
        if (ContentFrame.Content is UIElement reader)
            Kit.AnimatePageIn(reader, PageTransitionKind.DrillIn, Loc.Current.IsRtl);
        Backdrop.Paused = true;
        MiniPlayer.SuppressForReader = true;
        MiniPlayer.Refresh();
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!App.Settings.Current.CloseToTray)
        {
            args.Cancel = true;
            App.Quit();
            return;
        }
        if (!App.Settings.Current.TrayHintShown && App.Settings.Current.SetupDone)
        {
            App.Settings.Update(s => s with { TrayHintShown = true });
            Notifier.ShowText(Loc.Current["win_tray_hint_title"], Loc.Current["win_tray_hint_body"]);
        }
    }

#if DEBUG
    // --page=<name> opens at a page, --size=<w>x<h> sizes the window, --shot=<file.png> saves a snapshot and exits.
    private void DebugHooks()
    {
        string? Arg(string name) => App.StartArgs.FirstOrDefault(a => a.StartsWith(name))?[name.Length..];
        if (Arg("--size=") is { } size && size.Split('x') is [var sw, var sh] && int.TryParse(sw, out int w) && int.TryParse(sh, out int h))
        {
            double scale = Win32.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
            AppWindow.Resize(new SizeInt32((int)(w * scale), (int)(h * scale)));
        }
        if (Arg("--page=") is { } page && App.Settings.Current.SetupDone)
        {
            if (page == "reader" && App.Settings.Current.LastRead is { } last) OpenReader(new ReaderArgs(last.Page));
            else Navigate(page, force: true);
        }
        if (Arg("--demo-player=") is { } demo)
        {
            App.Media.LoadDemoPreview();
            MiniPlayer.Refresh();
            if (demo == "full") FullPlayer.Open();
        }
        if (App.StartArgs.Contains("--demo-update"))
        {
            App.Updates.LoadDemoState("available");
            RefreshUpdateBanner();
        }
        else if (Arg("--demo-update=") is { } demoUpdate)
        {
            App.Updates.LoadDemoState(demoUpdate);
            RefreshUpdateBanner();
        }
        if (Arg("--shot=") is not { } shot) return;
        double? scrollTarget = double.TryParse(Arg("--scroll="), out double sy) ? sy : null;
        _shotTimer = DispatcherQueue.CreateTimer();
        _shotTimer.Interval = TimeSpan.FromSeconds(int.TryParse(Arg("--shot-delay="), out int d) ? d : 3);
        _shotTimer.IsRepeating = false;
        _shotTimer.Tick += async (_, _) =>
        {
            Log.Info($"shot tick: {shot}");
            try
            {
                if (scrollTarget is { } offset && FindFirst<ScrollViewer>(ContentFrame) is { } sv)
                {
                    sv.ChangeView(null, offset, null, disableAnimation: true);
                    await Task.Delay(250);
                }
                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
                await bitmap.RenderAsync(Root);
                var pixels = await bitmap.GetPixelsAsync();
                if (File.Exists(shot)) File.Delete(shot);
                using var fs = File.Create(shot);
                using var stream = fs.AsRandomAccessStream();
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(pixels));
                await encoder.FlushAsync();
                Log.Info($"shot saved: {bitmap.PixelWidth}x{bitmap.PixelHeight}");
            }
            catch (Exception e) { Log.Error("shot", e); }
            App.Quit();
        };
        _shotTimer.Start();
    }

    private static T? FindFirst<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) return hit;
            if (FindFirst<T>(child) is { } nested) return nested;
        }
        return null;
    }
#endif
}
