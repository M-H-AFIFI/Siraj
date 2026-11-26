using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Audio;
using Siraj.Core.Devotions;
using Siraj.Core.Quran;

namespace Siraj.App.Views;

public sealed partial class WorshipPage : Page
{
    private readonly StackPanel _body;
    private bool _entered;
    private string _builtKey = "";

    public WorshipPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        var (root, body) = Kit.Page(Loc.Current["azkar_screen_title"], Loc.Current["azkar_screen_verse"], 1120);
        _body = body;
        Content = root;
        Loaded += (_, _) =>
        {
            App.Journal.Changed += OnDirty;
            App.Media.Changed += OnDirty;
            App.Theme.Changed += OnDirty;
            App.Quran.Changed += OnDirty;
            string key = CurrentKey();
            if (_body.Children.Count == 0 || key != _builtKey) Refresh();
        };
        Unloaded += (_, _) =>
        {
            App.Journal.Changed -= OnDirty;
            App.Media.Changed -= OnDirty;
            App.Theme.Changed -= OnDirty;
            App.Quran.Changed -= OnDirty;
        };
    }

    private string CurrentKey() =>
        $"{Today}|{AdhkarGroups.Suggested(TimeOnly.FromDateTime(App.Clock.Now.DateTime))}|{App.Media.Kind}:{App.Media.IsPlaying}:{App.Media.CurrentStation?.Id}:{App.Media.CurrentTrack?.Id}|{App.Theme.Scheme.Primary}|{App.Theme.IsDark}|{App.Theme.IsGlass}|{App.Journal.Current.Tasbih.Today}";

    private void OnDirty() { _builtKey = ""; Refresh(); }

    private static DateOnly Today => App.Clock.Place?.Today(App.Clock.Now) ?? DateOnly.FromDateTime(App.Clock.Now.LocalDateTime);

    private void Refresh()
    {
        if (!IsLoaded) return;
        _builtKey = CurrentKey();
        _body.Children.Clear();
        bool animate = !_entered;
        _entered = true;
        int order = 0;

        var hero = BuildAdhkarHero();
        if (animate) Kit.PopIn(hero, order++);
        _body.Children.Add(hero);

        _body.Children.Add(Ui.SectionWithIcon(Loc.Current["hub_section_title"], "nav_worship"));

        var audioCards = Kit.Columns(400, 14, [BuildListenCard(), BuildRadioCard()]);
        if (animate) Kit.PopIn(audioCards, order++);
        _body.Children.Add(audioCards);

        var tools = BuildToolsGrid();
        if (animate) Kit.PopIn(tools, order++);
        _body.Children.Add(tools);

        _body.Children.Add(Ui.SectionWithIcon(Loc.Current["azkar_chapters_title"], "dua_hands"));
        var adhkar = BuildDailyAdhkarRow();
        if (animate) Kit.PopIn(adhkar, order++);
        _body.Children.Add(adhkar);
    }

    private UIElement BuildAdhkarHero()
    {
        var loc = Loc.Current;
        var group = AdhkarGroups.Suggested(TimeOnly.FromDateTime(App.Clock.Now.DateTime));
        var items = AdhkarBook.Of(group);
        var counts = App.Journal.AdhkarCounts(group.Key(), Today);
        int done = items.Count(d => counts.GetValueOrDefault(d.Id) >= d.Count);
        int total = items.Count;

        string tag = group switch
        {
            AdhkarGroup.Morning => "morning",
            AdhkarGroup.Evening => "evening",
            AdhkarGroup.AfterPrayer => "after_prayer",
            _ => "sleep",
        };
        string badge = loc[$"azkar_card_{tag}_badge"];
        string desc = loc[$"azkar_card_{tag}_desc"];
        string iconKey = group switch
        {
            AdhkarGroup.Morning => "wb_sunny",
            AdhkarGroup.Evening => "nights_stay",
            AdhkarGroup.AfterPrayer => "azkar_carpet",
            _ => "bedtime",
        };

        var onAccent = Kit.B("SirajOnPrimaryContainerBrush");

        var root = new Grid();
        var watermark = SirajVectorIcon.Make(iconKey, 130, Kit.B("SirajPrimaryBrush"));
        watermark.Opacity = 0.10;
        watermark.HorizontalAlignment = HorizontalAlignment.Right;
        watermark.VerticalAlignment = VerticalAlignment.Center;
        watermark.Margin = new Thickness(0, -10, -8, -10);
        watermark.IsHitTestVisible = false;
        root.Children.Add(watermark);

        var content = new StackPanel { Spacing = 10 };
        var topRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        topRow.Children.Add(Kit.Pill(badge, "SirajPrimaryBrush", "SirajOnPrimaryBrush"));
        if (total > 0)
            topRow.Children.Add(Kit.Pill($"{loc.Number(done)} / {loc.Number(total)}", "SirajGlassPillBrush", "SirajOnPrimaryContainerBrush"));
        content.Children.Add(topRow);

        content.Children.Add(Kit.Text(group.Title(loc.Language), "SirajTitle", 26, bold: true, brush: onAccent));
        content.Children.Add(Kit.Text(desc, "SirajBody", 14, brush: onAccent));

        var progress = Kit.Bar(done, Math.Max(1, total), "SirajPrimaryBrush");
        progress.MaxWidth = 360;
        progress.HorizontalAlignment = HorizontalAlignment.Left;
        progress.Margin = new Thickness(0, 4, 0, 2);
        content.Children.Add(progress);

        var cta = Ui.Button(
            done >= total && total > 0 ? loc["azkar_hero_done"] : done > 0 ? loc["azkar_hero_continue"] : loc["azkar_start_reading"],
            () => OpenAdhkarGroup(group),
            primary: true);
        cta.HorizontalAlignment = HorizontalAlignment.Left;
        cta.Margin = new Thickness(0, 4, 0, 0);
        content.Children.Add(cta);

        root.Children.Add(content);
        return Kit.Accent(root, new Thickness(26, 22, 26, 22));
    }

    private UIElement BuildListenCard()
    {
        var loc = Loc.Current;
        var s = App.Settings.Current;
        var reciter = ListeningLibrary.FindReciter(s.LastListenReciterId);
        int surahNum = Math.Clamp(s.LastListenSurah ?? 1, 1, 114);
        bool playing = App.Media.Kind == MediaKind.Listen && App.Media.IsPlaying;
        string? art = ListeningLibrary.PortraitFiles.TryGetValue(reciter.Id, out var f) ? $"Portraits/{f}" : null;

        var outer = new Grid();
        var wm = SirajVectorIcon.Make("quick_listen", 84, Kit.B("SirajPrimaryBrush"));
        wm.Opacity = 0.045;
        wm.HorizontalAlignment = HorizontalAlignment.Right;
        wm.VerticalAlignment = VerticalAlignment.Bottom;
        wm.Margin = new Thickness(0, 0, 56, -12);
        wm.IsHitTestVisible = false;
        outer.Children.Add(wm);

        var stack = new StackPanel { Spacing = 14 };
        var topRow = new Grid();
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        badgeRow.Children.Add(Kit.Pill(loc["listen_card_badge"], "SirajPrimarySoftBrush", "SirajPrimaryBrush"));
        if (playing) badgeRow.Children.Add(new EqualizerBars(14) { IsActive = true });
        topRow.Children.Add(badgeRow);

        var allBtn = new HyperlinkButton
        {
            Content = $"{loc["listen_card_all"]} {(loc.IsRtl ? "‹" : "›")}",
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(8, 2, 8, 2),
        };
        allBtn.Click += (_, _) => App.MainWindowInstance?.Navigate("listen");
        topRow.Children.Add(allBtn);
        stack.Children.Add(topRow);

        var mid = new Grid { ColumnSpacing = 14 };
        mid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        mid.Children.Add(PortraitAvatar.Create(art, reciter.Name(loc.Language), 56, 28, activeRing: playing));

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        info.Children.Add(Kit.Text(loc["listen_continue"], "SirajCaption", 11.5));
        info.Children.Add(Kit.Text(MediaHubService.SurahTitle(surahNum, loc.Language), "SirajHeading", 17, bold: true));
        info.Children.Add(Kit.Text(reciter.Name(loc.Language), "SirajCaption", 12.5));
        Grid.SetColumn(info, 1);
        mid.Children.Add(info);

        var playBtn = new Button
        {
            Content = Kit.Icon(playing ? "pause" : "play", 20, Kit.B("SirajOnPrimaryBrush")),
            Width = 44, Height = 44, Padding = new Thickness(0), CornerRadius = new CornerRadius(22),
            Background = Kit.B("SirajPrimaryBrush"), BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(playBtn, loc[playing ? "pause" : "play"]);
        playBtn.Click += (_, _) =>
        {
            if (App.Media.Kind == MediaKind.Listen && App.Media.HasMedia) App.Media.TogglePlayPause();
            else App.Media.PlayReciterSurah(reciter, surahNum);
        };
        Kit.Lift(playBtn);
        Grid.SetColumn(playBtn, 2);
        mid.Children.Add(playBtn);

        stack.Children.Add(mid);
        outer.Children.Add(stack);
        return Kit.Card(outer, new Thickness(20, 18, 20, 18));
    }

    private UIElement BuildRadioCard()
    {
        var loc = Loc.Current;
        var station = RadioStations.Find(App.Settings.Current.LastRadioStationId) ?? RadioStations.All[0];
        bool liveHere = App.Media.Kind == MediaKind.Radio && App.Media.IsPlaying;
        var activeStation = liveHere && App.Media.CurrentStation != null ? App.Media.CurrentStation : station;

        var stack = new StackPanel { Spacing = 14 };
        var topRow = new Grid();
        var liveRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 229, 57, 53)), VerticalAlignment = VerticalAlignment.Center };
        liveRow.Children.Add(dot);
        liveRow.Children.Add(Kit.Text(loc["radio_live_badge"], "SirajCaption", 12, bold: true, brush: new SolidColorBrush(ColorHelper.FromArgb(255, 229, 57, 53))));
        if (liveHere) liveRow.Children.Add(new EqualizerBars(13) { IsActive = true });
        topRow.Children.Add(liveRow);

        var allBtn = new HyperlinkButton
        {
            Content = $"{loc["radio_more_stations_chip"]} {(loc.IsRtl ? "‹" : "›")}",
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(8, 2, 8, 2),
        };
        allBtn.Click += (_, _) => App.MainWindowInstance?.Navigate("radio", drillIn: true);
        topRow.Children.Add(allBtn);
        stack.Children.Add(topRow);

        var mid = new Grid { ColumnSpacing = 14 };
        mid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        mid.Children.Add(PortraitAvatar.Create(activeStation.ArtworkFile ?? "Radio/radio_quran.png", activeStation.Name(loc.Language), 56, 16, activeRing: liveHere));

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        info.Children.Add(Kit.Text(loc["radio_live_title"], "SirajCaption", 11.5));
        info.Children.Add(Kit.Text(activeStation.Name(loc.Language), "SirajHeading", 16.5, bold: true));
        info.Children.Add(Kit.Text(activeStation.HasPlace ? activeStation.Location(loc.Language) : loc["radio_hero_subtitle"], "SirajCaption", 12));
        Grid.SetColumn(info, 1);
        mid.Children.Add(info);

        var playBtn = new Button
        {
            Content = Kit.Icon(liveHere ? "pause" : "play", 20, Kit.B("SirajOnPrimaryBrush")),
            Width = 44, Height = 44, Padding = new Thickness(0), CornerRadius = new CornerRadius(22),
            Background = Kit.B("SirajPrimaryBrush"), BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(playBtn, loc[liveHere ? "pause" : "play"]);
        playBtn.Click += (_, _) =>
        {
            if (liveHere) App.Media.Pause();
            else App.Media.PlayRadio(activeStation);
        };
        Kit.Lift(playBtn);
        Grid.SetColumn(playBtn, 2);
        mid.Children.Add(playBtn);

        stack.Children.Add(mid);
        return Kit.Card(stack, new Thickness(20, 18, 20, 18));
    }

    private UIElement BuildToolsGrid()
    {
        var loc = Loc.Current;
        var items = new (string Page, string Icon, string Title, string Desc)[]
        {
            ("tasbih", "quick_tasbih", loc["nav_tasbih"], loc["hub_tasbih_desc"]),
            ("calendar", "hijri_calendar", loc["hub_calendar_title"], loc["hub_calendar_desc"]),
            ("names", "allah_word", loc["hub_names_title"], loc["hub_names_desc"]),
            ("hadith", "hadith", loc["hub_hadith_title"], loc["hub_hadith_desc"]),
            ("fasting", "fasting", loc["hub_fasting_title"], loc["hub_fasting_desc"]),
            ("zakat", "zakat", loc["hub_zakat_title"], loc["hub_zakat_desc"]),
        };

        var cards = new List<UIElement>(items.Length);
        foreach (var item in items)
        {
            var stack = new StackPanel { Spacing = 10 };
            var head = new Grid();
            head.Children.Add(Kit.IconBadge(item.Icon, 44, 22));
            var arrow = Kit.Icon(loc.IsRtl ? "chevron_left" : "chevron_right", 18, Kit.B("SirajOnSurfaceVariantBrush"));
            arrow.HorizontalAlignment = HorizontalAlignment.Right;
            arrow.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(arrow);
            stack.Children.Add(head);

            stack.Children.Add(Kit.Text(item.Title, "SirajHeading", 15.5, bold: true));
            var desc = Kit.Text(item.Desc, "SirajCaption", 12.5);
            desc.MaxLines = 2;
            stack.Children.Add(desc);

            string target = item.Page;
            cards.Add(Kit.CardButton(stack, () => App.MainWindowInstance?.Navigate(target, drillIn: true), name: item.Title));
        }
        return Kit.Columns(260, 12, cards);
    }

    private UIElement BuildDailyAdhkarRow()
    {
        var loc = Loc.Current;
        var coreGroups = new[] { AdhkarGroup.Morning, AdhkarGroup.Evening, AdhkarGroup.AfterPrayer, AdhkarGroup.Sleep };
        var cards = new List<UIElement>(coreGroups.Length + 1);

        foreach (var group in coreGroups)
        {
            var items = AdhkarBook.Of(group);
            var counts = App.Journal.AdhkarCounts(group.Key(), Today);
            int done = items.Count(d => counts.GetValueOrDefault(d.Id) >= d.Count);
            int total = items.Count;
            string iconKey = group switch
            {
                AdhkarGroup.Morning => "wb_sunny",
                AdhkarGroup.Evening => "nights_stay",
                AdhkarGroup.AfterPrayer => "azkar_carpet",
                _ => "bedtime",
            };

            var stack = new StackPanel { Spacing = 8 };
            var head = new Grid();
            head.Children.Add(Kit.IconBadge(iconKey, 38, 19));
            var countPill = Kit.Pill($"{loc.Number(done)}/{loc.Number(total)}",
                done >= total && total > 0 ? "SirajPrimaryBrush" : "SirajGlassPillBrush",
                done >= total && total > 0 ? "SirajOnPrimaryBrush" : "SirajOnSurfaceVariantBrush");
            countPill.HorizontalAlignment = HorizontalAlignment.Right;
            head.Children.Add(countPill);
            stack.Children.Add(head);

            stack.Children.Add(Kit.Text(group.ShortTitle(loc.Language), "SirajHeading", 15, bold: true));
            stack.Children.Add(Kit.Bar(done, Math.Max(1, total)));

            var g = group;
            cards.Add(Kit.CardButton(stack, () => OpenAdhkarGroup(g), name: group.Title(loc.Language)));
        }

        // Full Hisn al-Muslim / Adhkar browser card
        var allStack = new StackPanel { Spacing = 8 };
        allStack.Children.Add(Kit.IconBadge("dua_hands", 38, 19));
        allStack.Children.Add(Kit.Text(loc["azkar_chapters_title"], "SirajHeading", 15, bold: true));
        allStack.Children.Add(Kit.Text(loc.Format("azkar_chapters_count", loc.Number(AdhkarBook.Chapters.Count)), "SirajCaption", 12));
        cards.Add(Kit.CardButton(allStack, () => App.MainWindowInstance?.Navigate("adhkar", drillIn: true), name: loc["azkar_chapters_title"]));

        return Kit.Columns(210, 12, cards);
    }

    private static void OpenAdhkarGroup(AdhkarGroup group)
    {
        AdhkarPage.PendingGroup = group;
        App.MainWindowInstance?.Navigate("adhkar", force: true, drillIn: true);
    }
}
