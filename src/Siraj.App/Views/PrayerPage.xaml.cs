using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Adhan;
using Siraj.Core.Devotions;
using Siraj.Core.Hijri;
using Siraj.Core.Prayer;
using Siraj.Core.Quran;
using Siraj.Core.Text;
using Windows.ApplicationModel.DataTransfer;

namespace Siraj.App.Views;

public sealed partial class PrayerPage : Page
{
    private sealed record RowParts(
        Border Row, Border Badge, ContentControl BadgeContent, TextBlock Name,
        TextBlock Time, TextBlock Iqama, Button Bell, SirajVectorIcon BellIcon);

    private readonly Dictionary<PrayerType, RowParts> _rows = new();
    private readonly MoonDisc _moon = new();
    private bool _entered;
    private string _lastDynamicKey = "";

    public PrayerPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        OpenQuranIcon.Content = SirajVectorIcon.Make("quick_quran", 24, Kit.B("SirajOnPrimaryBrush"));
        PlaceButton.UseSystemFocusVisuals = false;
        OpenQuranBtn.UseSystemFocusVisuals = false;
        Kit.Lift(PlaceButton);
        Kit.Lift(OpenQuranBtn);
        Kit.Lift(MoonCard);

        BuildQuickTiles();
        BuildRows();
        MoonHost.Child = _moon;

        Loaded += (_, _) =>
        {
            App.Clock.DayChanged += Refresh;
            App.Clock.Tick += RefreshMarks;
            App.Theme.Changed += OnThemeChanged;
            App.Settings.Changed += OnSettings;
            App.Quran.Changed += OnQuranChanged;
            Refresh();
            if (!_entered)
            {
                _entered = true;
                Kit.PopIn(QuickActions, 0);
                Kit.PopIn(PrimaryColumn, 1);
                Kit.PopIn(Side, 2);
            }
        };
        Unloaded += (_, _) =>
        {
            App.Clock.DayChanged -= Refresh;
            App.Clock.Tick -= RefreshMarks;
            App.Theme.Changed -= OnThemeChanged;
            App.Settings.Changed -= OnSettings;
            App.Quran.Changed -= OnQuranChanged;
        };
        SizeChanged += (_, e) => Arrange(e.NewSize.Width);
    }

    private void OnThemeChanged() { _lastDynamicKey = ""; Refresh(); }
    private void OnQuranChanged() { _lastDynamicKey = ""; Refresh(); }
    private void OnSettings(Core.Settings.AppSettings old, Core.Settings.AppSettings updated) => DispatcherQueue.TryEnqueue(() => { _lastDynamicKey = ""; Refresh(); });

    private void Arrange(double width)
    {
        bool wide = width >= 920;
        LeftColumn.Width = new GridLength(3, GridUnitType.Star);
        RightColumn.Width = wide ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(Side, wide ? 1 : 0);
        Grid.SetRow(Side, wide ? 0 : 1);
    }

    private void BuildQuickTiles()
    {
        var loc = Loc.Current;
        QuickTilesGrid.Children.Clear();
        (string Icon, string Label, Action Click)[] tiles =
        [
            ("quick_tasbih", loc["nav_tasbih"], () => App.MainWindowInstance?.Navigate("tasbih", drillIn: true)),
            ("quick_listen", loc["quick_listen"], () => App.MainWindowInstance?.Navigate("listen")),
            ("quick_duas", loc["duas"], () => App.MainWindowInstance?.Navigate("worship")),
        ];
        for (int i = 0; i < tiles.Length; i++)
        {
            var (icon, label, click) = tiles[i];
            var stack = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(SirajVectorIcon.Make(icon, 26, Kit.B("SirajPrimaryBrush")));
            var txt = Kit.Text(label, "SirajBody", 14.5, bold: true);
            txt.HorizontalAlignment = HorizontalAlignment.Center;
            txt.TextAlignment = TextAlignment.Center;
            stack.Children.Add(txt);
            var btn = Kit.CardButton(stack, click, label, padding: new Thickness(12, 16, 12, 16));
            btn.Height = 92;
            btn.CornerRadius = new CornerRadius(24);
            Grid.SetColumn(btn, i);
            QuickTilesGrid.Children.Add(btn);
        }
    }

    private void BuildRows()
    {
        Rows.Children.Clear();
        _rows.Clear();
        foreach (var type in Enum.GetValues<PrayerType>())
        {
            var badgeContent = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            var badge = new Border
            {
                Width = 38, Height = 38, CornerRadius = new CornerRadius(12),
                Background = Kit.B("SirajGlassPillBrush"),
                Child = badgeContent,
            };
            var name = new TextBlock { Style = (Style)Application.Current.Resources["SirajBody"], FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
            var iqama = new TextBlock { Style = (Style)Application.Current.Resources["SirajCaption"], FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            var time = new TextBlock { Style = (Style)Application.Current.Resources["SirajBody"], FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            var bellIcon = SirajVectorIcon.Make("notifications", 16, Kit.B("SirajPrimaryBrush"));
            var bell = new Button
            {
                Width = 34, Height = 34, Padding = new Thickness(0), CornerRadius = new CornerRadius(11),
                Margin = new Thickness(10, 0, 0, 0), Tag = type, Content = bellIcon,
                Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0),
            };
            bell.Click += OnBell;

            var grid = new Grid { ColumnSpacing = 12, Padding = new Thickness(12, 9, 8, 9) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(badge);
            Grid.SetColumn(name, 1); grid.Children.Add(name);
            Grid.SetColumn(iqama, 2); grid.Children.Add(iqama);
            Grid.SetColumn(time, 3); grid.Children.Add(time);
            Grid.SetColumn(bell, 4); grid.Children.Add(bell);

            var row = new Border
            {
                CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Colors.Transparent), Child = grid,
            };
            Rows.Children.Add(row);
            _rows[type] = new RowParts(row, badge, badgeContent, name, time, iqama, bell, bellIcon);
        }
    }

    private void OnBell(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PrayerType type })
            App.Settings.Update(s => s.WithAlert(type, !s.AlertOn(type)));
    }

    private void Refresh()
    {
        var loc = Loc.Current;
        var settings = App.Settings.Current;
        var clock = App.Clock;
        bool eastern = Digits.UseEastern(settings.NumeralSystem, loc.Language);

        AppTitleText.Text = loc["app_name"];
        GreetingText.Text = loc["dashboard_greeting"];
        var place = clock.Place;
        PlaceText.Text = place?.DisplayName(loc.Language) ?? (place != null
            ? loc.Digitize($"{place.Latitude:0.00}, {place.Longitude:0.00}") : loc["dashboard_location_action"]);
        OpenQuranText.Text = loc["open_quran"];

        var today = place?.Today(clock.Now) ?? DateOnly.FromDateTime(DateTime.Now);
        TimesTitle.Text = loc["today"];
        DateText.Text = HijriText.Gregorian(today, loc.Language, eastern, loc.Language == "en" ? "MMM d, yyyy" : "d MMMM yyyy");
        MethodText.Text = CalculationMethods.ById(settings.CalculationMethod)?.Name(loc.Language) ?? "";

        foreach (var (type, r) in _rows)
        {
            var prayer = clock.Day?.Times.FirstOrDefault(t => t.Type == type);
            r.Name.Text = prayer != null ? loc.PrayerName(prayer) : loc.PrayerName(type);
            r.Time.Text = prayer != null ? loc.Time(prayer.Time) : "--:--";
            r.Iqama.Text = prayer?.Iqama is { } iq ? $"{loc["iqama"]} {loc.Time(iq)}" : "";
            bool on = settings.AlertOn(type);
            r.BellIcon.IconKey = on ? "notifications" : "notifications_off";
            r.BellIcon.Brush = Kit.B(on ? "SirajPrimaryBrush" : "SirajOnSurfaceVariantBrush");
            r.Bell.Background = on ? Kit.B("SirajPrimarySoftBrush") : new SolidColorBrush(Colors.Transparent);
            r.Bell.Opacity = on ? 1.0 : 0.55;
            ToolTipService.SetToolTip(r.Bell, loc.Format("dashboard_adhan_toggle", loc.PrayerName(type)));
            AutomationProperties.SetName(r.Bell, loc.Format("dashboard_adhan_toggle", loc.PrayerName(type)));
        }

        string dynKey = $"{today}|{eastern}|{settings.ShowContinueReadingOnHome}|{settings.LastRead?.Page}:{settings.LastRead?.Surah}:{settings.LastRead?.Ayah}|{settings.Bookmarks.Count}|{App.Quran.Status}|{App.Theme.Scheme.Primary}|{App.Theme.IsDark}|{App.Theme.IsGlass}";
        if (dynKey != _lastDynamicKey)
        {
            _lastDynamicKey = dynKey;
            BuildDynamicCards(today, eastern);
        }

        var moon = MoonPhase.At(clock.Now);
        _moon.Phase = moon;
        MoonTitle.Text = loc["moon_tonight"];
        MoonPhaseText.Text = loc[MoonDisc.NameKey(moon.Name)];
        MoonText.Text = MoonDisc.Line(loc, moon, today, eastern);
        MoonChevron.IconKey = loc.IsRtl ? "chevron_left" : "chevron_right";

        WidgetsTitle.Text = loc["win_widgets_card_title"];
        WidgetsText.Text = loc["win_widgets_card_body"];
        WidgetsButton.Content = loc["win_widgets_card_action"];
        RefreshMarks();
    }

    private void BuildDynamicCards(DateOnly today, bool eastern)
    {
        var loc = Loc.Current;
        var s = App.Settings.Current;
        DynamicPrimarySlot.Children.Clear();
        DailyCardsSlot.Children.Clear();

        var upcoming = IslamicOccasions.Upcoming(today, HijriCalendars.Current).FirstOrDefault();
        if (upcoming != null && upcoming.DaysUntil <= 30)
            DynamicPrimarySlot.Children.Add(BuildOccasionBanner(upcoming, eastern));

        if (s.ShowContinueReadingOnHome)
            DynamicPrimarySlot.Children.Add(BuildContinueReadingCard());

        DailyCardsSlot.Children.Add(BuildDailyAyahCard(today));
        DailyCardsSlot.Children.Add(BuildDailyHadithCard(today));
    }

    private UIElement BuildOccasionBanner(DatedOccasion occ, bool eastern)
    {
        var loc = Loc.Current;
        var row = new Grid { ColumnSpacing = 14 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(Kit.OccasionBadge(occ.Occasion.Id, 44, occ.IsToday));

        var texts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(Kit.Text(occ.Occasion.Title(loc.Language), "SirajHeading", 15.5));
        texts.Children.Add(Kit.Text(HijriText.Gregorian(occ.Date, loc.Language, eastern, "d MMMM yyyy"), "SirajCaption", 12.5));
        Grid.SetColumn(texts, 1);
        row.Children.Add(texts);

        string badge = occ.DaysUntil switch
        {
            0 => loc["tools_fasting_today"],
            1 => loc["tools_fasting_tomorrow"],
            _ => loc.Plural("tools_fasting_in_days", occ.DaysUntil),
        };
        var pill = Kit.Pill(badge, occ.IsToday ? "SirajPrimaryBrush" : "SirajGoldSoftBrush", occ.IsToday ? "SirajOnPrimaryBrush" : "SirajGoldBrush");
        Grid.SetColumn(pill, 2);
        row.Children.Add(pill);

        return Kit.CardButton(row, () => App.MainWindowInstance?.Navigate("calendar", drillIn: true), occ.Occasion.Title(loc.Language), padding: new Thickness(16, 14, 16, 14));
    }

    private UIElement BuildContinueReadingCard()
    {
        var loc = Loc.Current;
        var last = App.Settings.Current.LastRead;
        int page = last?.Page ?? 1;
        int surahNum = last?.Surah ?? 1;
        int ayahNum = last?.Ayah ?? 1;
        int juz = QuranDatabase.JuzOfPage(page);
        string surahName = MediaHubService.SurahTitle(surahNum, loc.Language);

        var stack = new StackPanel { Spacing = 18 };
        var top = new Grid { ColumnSpacing = 12 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Spacing = 3 };
        left.Children.Add(Kit.Text(loc[last != null ? "quran_continue_reading" : "quran_reading"], "SirajCaption", 12, bold: true, brush: Kit.B("SirajPrimaryBrush")));
        left.Children.Add(Kit.Text(last != null ? surahName : loc["quran_start_journey"], "SirajHeading", 20));
        if (last != null)
            left.Children.Add(Kit.Text(loc.Format("last_read_where", loc.Number(ayahNum), loc.Number(page), loc.Number(juz)), "SirajCaption", 13));
        top.Children.Add(left);

        var arrowCircle = new Border
        {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(23),
            Background = Kit.B("SirajPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center,
            Child = Kit.Icon(loc.IsRtl ? "arrow_back" : "arrow_forward", 19, Kit.B("SirajOnPrimaryBrush")),
        };
        Grid.SetColumn(arrowCircle, 1);
        top.Children.Add(arrowCircle);
        stack.Children.Add(top);

        var gauges = new Grid { ColumnSpacing = 10 };
        for (int i = 0; i < 3; i++) gauges.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gauges.Children.Add(Gauge((double)page / QuranDatabase.PageCount, loc.Number(page), loc["last_read_page_gauge"]));
        var gJuz = Gauge(juz / 30.0, $"{loc.Number(juz)}/{loc.Number(30)}", loc["quran_juz"]);
        Grid.SetColumn(gJuz, 1); gauges.Children.Add(gJuz);
        var gSurah = Gauge(surahNum / 114.0, $"{loc.Number(surahNum)}/{loc.Number(114)}", loc["quran_surah"]);
        Grid.SetColumn(gSurah, 2); gauges.Children.Add(gSurah);
        stack.Children.Add(gauges);

        if (App.Settings.Current.Bookmarks.FirstOrDefault() is { } bm && (bm.Surah != surahNum || bm.Ayah != ayahNum))
        {
            stack.Children.Add(new Border { Height = 1, Background = Kit.B("SirajDividerBrush") });
            var bmRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            bmRow.Children.Add(Kit.Icon("bookmark", 15, Kit.B("SirajGoldBrush")));
            bmRow.Children.Add(Kit.Text(loc.Format("dashboard_reading_bookmark", MediaHubService.SurahTitle(bm.Surah, loc.Language), loc.Number(bm.Ayah)), "SirajCaption", 13, bold: true));
            var bmBtn = new Button
            {
                Content = bmRow, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0),
                Padding = new Thickness(2, 4, 2, 2), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            bmBtn.Click += (_, _) => App.MainWindowInstance?.OpenReader(new ReaderArgs(bm.Page, (bm.Surah, bm.Ayah)));
            stack.Children.Add(bmBtn);
        }

        return Kit.CardButton(stack, () =>
        {
            if (App.Quran.Status == QuranService.State.Ready && last != null)
                App.MainWindowInstance?.OpenReader(new ReaderArgs(last.Page, (last.Surah, last.Ayah)));
            else
                App.MainWindowInstance?.Navigate("quran");
        }, loc["quran_continue_reading"], padding: new Thickness(20, 18, 20, 18));
    }

    private static StackPanel Gauge(double fraction, string value, string label)
    {
        var ring = new Ring { Width = 62, Height = 62, Thickness = 5.5, Smooth = true, Fraction = fraction };
        var val = Kit.Text(value, "SirajCaption", 11.5, bold: true, brush: Kit.B("SirajOnSurfaceBrush"));
        val.HorizontalAlignment = HorizontalAlignment.Center;
        val.VerticalAlignment = VerticalAlignment.Center;
        ring.Children.Add(val);
        var stack = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(ring);
        var lbl = Kit.Text(label, "SirajCaption", 11.5, bold: true);
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(lbl);
        return stack;
    }

    private UIElement BuildDailyAyahCard(DateOnly today)
    {
        var loc = Loc.Current;
        var v = DailyAyah.ForDay(today);
        var stack = new StackPanel { Spacing = 12 };

        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(SirajVectorIcon.Make("quick_quran", 18, Kit.B("SirajPrimaryBrush")));
        titleRow.Children.Add(Kit.Text(loc["ayah_of_the_day"], "SirajCaption", 12.5, bold: true, brush: Kit.B("SirajPrimaryBrush")));
        head.Children.Add(titleRow);
        var refPill = Kit.Pill($"{v.SurahName(loc.Language)}{loc.Sep}{loc.Format("quran_ayah_label", loc.Number(v.Ayah))}", "SirajPrimarySoftBrush", "SirajPrimaryBrush");
        Grid.SetColumn(refPill, 1);
        head.Children.Add(refPill);
        stack.Children.Add(head);

        var arabic = new TextBlock
        {
            Text = DigitalKhattText.ForDisplay(v.Arabic),
            FontFamily = (FontFamily)Application.Current.Resources["SirajQuranFont"],
            FontSize = 23, LineHeight = 46, LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextWrapping = TextWrapping.Wrap, FlowDirection = FlowDirection.RightToLeft,
            Foreground = Kit.B("SirajOnSurfaceBrush"),
        };
        stack.Children.Add(arabic);

        string tr = v.Translation(loc.Language);
        if (!string.IsNullOrEmpty(tr))
            stack.Children.Add(Kit.Text(tr, "SirajCaption", 13.5));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2, 0, 0) };
        var copyBtn = Ui.Button(loc["quran_action_copy"], () =>
        {
            var pkg = new DataPackage();
            pkg.SetText($"{v.Arabic}\n{(string.IsNullOrEmpty(tr) ? "" : tr + "\n")}- {v.SurahName(loc.Language)} ({v.Surah}:{v.Ayah})");
            Clipboard.SetContent(pkg);
        });
        copyBtn.Padding = new Thickness(12, 5, 12, 5);
        actions.Children.Add(copyBtn);
        if (App.Quran.Status == QuranService.State.Ready)
        {
            var openBtn = Ui.Button(loc["open_quran"], () => App.MainWindowInstance?.OpenReader(new ReaderArgs(v.Page, (v.Surah, v.Ayah))));
            openBtn.Padding = new Thickness(12, 5, 12, 5);
            actions.Children.Add(openBtn);
        }
        stack.Children.Add(actions);

        var outer = new Grid();
        var wm = SirajVectorIcon.Make("quick_quran", 96, Kit.B("SirajPrimaryBrush"));
        wm.Opacity = 0.045;
        wm.HorizontalAlignment = HorizontalAlignment.Right;
        wm.VerticalAlignment = VerticalAlignment.Bottom;
        wm.Margin = new Thickness(0, 0, -10, -14);
        wm.IsHitTestVisible = false;
        outer.Children.Add(wm);
        outer.Children.Add(stack);

        return Kit.Card(outer, new Thickness(20, 18, 20, 16));
    }

    private UIElement BuildDailyHadithCard(DateOnly today)
    {
        var loc = Loc.Current;
        var h = Hadiths.ForDay(today);
        var stack = new StackPanel { Spacing = 10 };

        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(SirajVectorIcon.Make("hadith", 18, Kit.B("SirajGoldBrush")));
        titleRow.Children.Add(Kit.Text(loc["hadith_of_the_day"], "SirajCaption", 12.5, bold: true, brush: Kit.B("SirajGoldBrush")));
        head.Children.Add(titleRow);
        var catPill = Kit.Pill(h.Category(loc.Language), "SirajGoldSoftBrush", "SirajGoldBrush");
        Grid.SetColumn(catPill, 1);
        head.Children.Add(catPill);
        stack.Children.Add(head);

        stack.Children.Add(Kit.Text(h.Title(loc.Language), "SirajHeading", 16));
        string bodyText = loc.Language == "ar" ? h.MatnAr : (h.Translation(loc.Language) is { Length: > 0 } t ? t : h.MatnAr);
        if (bodyText.Length > 210) bodyText = bodyText[..207].TrimEnd() + "…";
        var body = loc.Language == "ar" ? Kit.Arabic(bodyText, 19) : Kit.Text(bodyText, "SirajBody", 13.5);
        stack.Children.Add(body);
        stack.Children.Add(Kit.Text(h.Reference(loc.Language), "SirajCaption", 11.5));

        var outer = new Grid();
        var wm = SirajVectorIcon.Make("hadith", 92, Kit.B("SirajGoldBrush"));
        wm.Opacity = 0.05;
        wm.HorizontalAlignment = HorizontalAlignment.Right;
        wm.VerticalAlignment = VerticalAlignment.Bottom;
        wm.Margin = new Thickness(0, 0, -10, -14);
        wm.IsHitTestVisible = false;
        outer.Children.Add(wm);
        outer.Children.Add(stack);

        return Kit.CardButton(outer, () => App.MainWindowInstance?.Navigate("hadith", drillIn: true), h.Title(loc.Language), padding: new Thickness(20, 18, 20, 16));
    }

    private void RefreshMarks()
    {
        var clock = App.Clock;
        var focus = clock.Focus;
        var now = clock.Now;
        var scheme = App.Theme.Scheme;
        foreach (var (type, r) in _rows)
        {
            bool isFocus = focus?.Type == type;
            var prayer = clock.Day?.Times.FirstOrDefault(t => t.Type == type);
            bool past = prayer != null && !isFocus && prayer.Time < now && !(type == PrayerType.Fajr && prayer.Time.Date != now.Date);

            r.Row.Background = isFocus ? Kit.B("SirajPrimarySoftBrush") : new SolidColorBrush(Colors.Transparent);
            r.Row.BorderBrush = isFocus
                ? new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0, 0),
                    EndPoint = new Windows.Foundation.Point(1, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = ThemeService.ToColor(Core.Sky.Argb.Hex(0xFFFFFFFF), scheme.IsDark ? 0.28f : 0.78f), Offset = 0 },
                        new GradientStop { Color = ThemeService.ToColor(scheme.Primary, 0.38f), Offset = 1 },
                    },
                }
                : new SolidColorBrush(Colors.Transparent);
            r.Badge.Background = isFocus ? Kit.B("SirajPrimaryBrush") : Kit.B("SirajGlassPillBrush");
            r.BadgeContent.Content = past
                ? Kit.Icon("check", 16, Kit.B("SirajPrimaryBrush"))
                : new PrayerGlyph { Prayer = type, Width = 21, Height = 21, Brush = Kit.B(isFocus ? "SirajOnPrimaryBrush" : "SirajPrimaryBrush") };

            var fg = Kit.B(isFocus ? "SirajPrimaryBrush" : "SirajOnSurfaceBrush");
            r.Name.Foreground = fg;
            r.Name.FontWeight = isFocus ? FontWeights.Bold : FontWeights.SemiBold;
            r.Time.Foreground = isFocus ? Kit.B("SirajPrimaryBrush") : Kit.B("SirajOnSurfaceVariantBrush");
            r.Time.FontWeight = isFocus ? FontWeights.Bold : FontWeights.SemiBold;
            r.Row.Opacity = past ? 0.62 : 1.0;
        }
    }

    private void OnPlace(object sender, RoutedEventArgs e) => App.ShowMainWindow("settings");
    private void OnWidgets(object sender, RoutedEventArgs e) => App.ShowMainWindow("widgets");
    private void OnCalendar(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) => App.MainWindowInstance?.Navigate("calendar", drillIn: true);

    private void OnOpenQuran(object sender, RoutedEventArgs e)
    {
        if (App.Quran.Status == QuranService.State.Ready && App.Settings.Current.LastRead is { } last)
            App.MainWindowInstance?.OpenReader(new ReaderArgs(last.Page, (last.Surah, last.Ayah)));
        else
            App.MainWindowInstance?.Navigate("quran");
    }
}
