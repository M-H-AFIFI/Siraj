using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Devotions;
using Siraj.Core.Prayer;

namespace Siraj.App.Views;

public sealed partial class AdhkarPage : Page
{
    internal static AdhkarGroup? PendingGroup { get; set; }

    private AdhkarTab _tab = AdhkarTab.All;
    private string _query = "";
    private StackPanel? _groupsHost;
    private StackPanel? _chaptersHost;
    private (string Key, string Title, string? Subtitle, IReadOnlyList<Dhikr> Items)? _open;

    public AdhkarPage()
    {
        InitializeComponent();
        Loaded += (_, _) => App.Settings.Changed += OnSettings;
        Unloaded += (_, _) => App.Settings.Changed -= OnSettings;
        if (PendingGroup is { } pending)
        {
            PendingGroup = null;
            OpenGroup(pending);
        }
        else ShowHub();
    }

    private void OnSettings(Core.Settings.AppSettings old, Core.Settings.AppSettings updated) => DispatcherQueue.TryEnqueue(() =>
    {
        if (old.AdhkarTranslation == updated.AdhkarTranslation && old.AdhkarTransliteration == updated.AdhkarTransliteration &&
            old.AdhkarVirtues == updated.AdhkarVirtues) return;
        if (_open is { } set) ShowSet(set.Key, set.Title, set.Subtitle, set.Items);
    });

    private static DateOnly Today => App.Clock.Place?.Today(App.Clock.Now) ?? DateOnly.FromDateTime(App.Clock.Now.LocalDateTime);

    private static (int Done, int Total) Progress(string key, IReadOnlyList<Dhikr> items)
    {
        var counts = App.Journal.AdhkarCounts(key, Today);
        return (items.Count(d => counts.GetValueOrDefault(d.Id) >= d.Count), items.Count);
    }

    private void ShowHub()
    {
        _open = null;
        var loc = Loc.Current;
        var (root, body) = Kit.Page(loc["win_nav_adhkar"], loc["azkar_screen_verse"],
            back: () => App.MainWindowInstance?.Navigate("worship"));
        body.Children.Add(Hero());
        body.Children.Add(Kit.Search(loc["azkar_search_hint"], q => { _query = q; FillGroups(); FillChapters(); }));
        var tabs = Ui.Segments(Enum.GetValues<AdhkarTab>().Select(t => (t, t.Label(loc.Language))).ToList(), _tab, t =>
        {
            _tab = t;
            FillGroups();
            FillChapters();
        });
        body.Children.Add(new ScrollViewer
        {
            Content = tabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled,
        });
        _groupsHost = new StackPanel();
        body.Children.Add(_groupsHost);
        _chaptersHost = new StackPanel { Spacing = 12 };
        body.Children.Add(_chaptersHost);
        FillGroups();
        FillChapters();
        bool animateBack = Host.Children.Count > 0;
        Host.Children.Clear();
        Host.Children.Add(root);
        if (animateBack) Kit.AnimatePageIn(root, PageTransitionKind.Back, loc.IsRtl);
    }

    private UIElement Hero()
    {
        var loc = Loc.Current;
        var group = AdhkarGroups.Suggested(TimeOnly.FromDateTime(App.Clock.Now.DateTime));
        var items = AdhkarBook.Of(group);
        var (done, total) = Progress(group.Key(), items);
        string tag = group switch
        {
            AdhkarGroup.Morning => "morning", AdhkarGroup.Evening => "evening", AdhkarGroup.AfterPrayer => "after_prayer", _ => "sleep",
        };
        string badge = group == AdhkarGroup.AfterPrayer ? loc["azkar_card_after_prayer_badge"] : loc[$"azkar_card_{tag}_badge"];

        var onAccent = Kit.B("SirajOnPrimaryContainerBrush");
        var texts = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(Kit.Pill(badge, "SirajPrimaryBrush", "SirajOnPrimaryBrush"));
        texts.Children.Add(Kit.Text(group.Title(loc.Language), "SirajTitle", 26, brush: onAccent));
        texts.Children.Add(Kit.Text(loc[$"azkar_card_{tag}_desc"], "SirajBody", brush: onAccent));
        var action = Ui.Button(done == total ? loc["azkar_hero_done"] : done > 0 ? loc["azkar_hero_continue"] : loc["azkar_start_reading"],
            () => OpenGroup(group), primary: true);
        action.Margin = new Thickness(0, 8, 0, 0);
        texts.Children.Add(action);

        var ring = new Ring { Width = 112, Height = 112, Thickness = 9, Smooth = true, Fraction = total == 0 ? 0 : (double)done / total, Track = Kit.B("SirajSurfaceBrush") };
        var ringText = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        ringText.Children.Add(Kit.Text(loc.Number(done), "SirajTitle", 28, brush: onAccent));
        var of = Kit.Text(loc.Format("azkar_progress_of", "", loc.Number(total)).Trim(), "SirajCaption", brush: onAccent);
        of.HorizontalAlignment = HorizontalAlignment.Center;
        ringText.Children.Add(of);
        ring.Children.Add(ringText);
        ((TextBlock)ringText.Children[0]).HorizontalAlignment = HorizontalAlignment.Center;

        var grid = new Grid { ColumnSpacing = 24 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(texts);
        Grid.SetColumn(ring, 1);
        grid.Children.Add(ring);
        return Kit.Accent(grid, new Thickness(28, 24, 28, 24));
    }

    private void FillGroups()
    {
        if (_groupsHost == null) return;
        var loc = Loc.Current;
        _groupsHost.Children.Clear();
        var groups = _tab.Groups().Where(g => Matches(g, _query)).ToList();
        if (groups.Count == 0) return;
        var cards = groups.Select(g => (UIElement)GroupCard(g)).ToList();
        _groupsHost.Children.Add(Kit.Columns(250, 12, cards));
    }

    private static bool Matches(AdhkarGroup g, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var q = query.Trim();
        return Loc.Languages.Any(l => g.Title(l).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                      g.ShortTitle(l).Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private Button GroupCard(AdhkarGroup group)
    {
        var loc = Loc.Current;
        var items = AdhkarBook.Of(group);
        var (done, total) = Progress(group.Key(), items);
        var stack = new StackPanel { Spacing = 6 };
        var top = new Grid();
        top.Children.Add(GroupIcon(group));
        if (done == total && total > 0)
        {
            var check = Kit.Icon("check", 16);
            check.HorizontalAlignment = HorizontalAlignment.Right;
            top.Children.Add(check);
        }
        stack.Children.Add(top);
        var title = Kit.Text(group.ShortTitle(loc.Language), "SirajHeading");
        title.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(title);
        stack.Children.Add(Kit.Text(loc[PurposeKey(group)], "SirajCaption"));
        var progress = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 8, 0, 0) };
        progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        progress.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var bar = Kit.Bar(done, total);
        bar.VerticalAlignment = VerticalAlignment.Center;
        progress.Children.Add(bar);
        var count = Kit.Text(loc.Format("azkar_progress_of", loc.Number(done), loc.Number(total)), "SirajCaption", 12);
        count.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(count, 1);
        progress.Children.Add(count);
        stack.Children.Add(progress);
        return Kit.CardButton(stack, () => OpenGroup(group), group.ShortTitle(loc.Language));
    }

    private static FrameworkElement GroupIcon(AdhkarGroup group)
    {
        var fg = Kit.B("SirajPrimaryBrush");
        string key = group switch
        {
            AdhkarGroup.Morning => "wb_sunny",
            AdhkarGroup.Evening => "nights_stay",
            AdhkarGroup.Sleep => "bedtime",
            AdhkarGroup.WakeUp => "wb_twilight",
            AdhkarGroup.AfterPrayer => "azkar_carpet",
            AdhkarGroup.Mosque => "eid_mosque",
            AdhkarGroup.Ruqiya => "shield",
            AdhkarGroup.Grief => "azkar_grief",
            AdhkarGroup.Debt => "azkar_debt",
            AdhkarGroup.Travel => "flight",
            AdhkarGroup.Hajj => "kaaba",
            AdhkarGroup.Rabbana => "dua_hands",
            AdhkarGroup.Essential => "favorite",
            _ => "azkar_missed",
        };
        FrameworkElement icon = SirajVectorIcon.Make(key, 21, fg);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
        return new Border
        {
            Width = 42, Height = 42, CornerRadius = new CornerRadius(14), Background = Kit.B("SirajPrimarySoftBrush"),
            HorizontalAlignment = HorizontalAlignment.Left, Child = icon,
        };
    }

    private static string PurposeKey(AdhkarGroup group) => group switch
    {
        AdhkarGroup.Morning => "azkar_purpose_morning",
        AdhkarGroup.Evening => "azkar_purpose_evening",
        AdhkarGroup.Sleep => "azkar_purpose_sleep",
        AdhkarGroup.WakeUp => "azkar_purpose_wake_up",
        AdhkarGroup.AfterPrayer => "azkar_purpose_after_prayer",
        AdhkarGroup.Travel => "azkar_purpose_travel",
        AdhkarGroup.Ruqiya => "azkar_purpose_ruqiya",
        AdhkarGroup.Hajj => "azkar_purpose_hajj",
        AdhkarGroup.Essential => "azkar_purpose_essential",
        AdhkarGroup.Rabbana => "azkar_purpose_rabbana",
        AdhkarGroup.Grief => "azkar_purpose_grief",
        AdhkarGroup.Debt => "azkar_purpose_debt",
        AdhkarGroup.Mosque => "azkar_purpose_mosque",
        _ => "azkar_purpose_missed_rakah",
    };

    private void FillChapters()
    {
        if (_chaptersHost == null) return;
        var loc = Loc.Current;
        _chaptersHost.Children.Clear();
        var chapters = AdhkarBook.Chapters.Where(c => _tab.Shows(c.Title) && AdhkarBook.ChapterMatches(c.Title, _query)).ToList();
        var head = new StackPanel { Spacing = 2, Margin = new Thickness(4, 12, 4, 0) };
        head.Children.Add(Kit.Text(loc["azkar_chapters_title"], "SirajHeading"));
        head.Children.Add(Kit.Text(loc.Format("azkar_chapters_count", loc.Number(chapters.Count)), "SirajCaption"));
        _chaptersHost.Children.Add(head);
        if (chapters.Count == 0)
        {
            var empty = new StackPanel { Spacing = 4, Margin = new Thickness(4, 8, 4, 8) };
            empty.Children.Add(Kit.Text(loc.Format("azkar_no_results", _query.Trim()), "SirajBody", bold: true));
            empty.Children.Add(Kit.Text(loc["adhkar_no_results_hint"], "SirajCaption"));
            _chaptersHost.Children.Add(empty);
            return;
        }
        _chaptersHost.Children.Add(Kit.Columns(330, 10, chapters.Select(c => (UIElement)ChapterRow(c)).ToList()));
    }

    private Button ChapterRow(AdhkarChapter chapter)
    {
        var loc = Loc.Current;
        var title = AdhkarBook.ChapterTitle(chapter.Title, loc.Language);
        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var number = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Kit.B("SirajSurfaceContainerHighBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = loc.Number(chapter.Number), FontSize = 12, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Foreground = Kit.B("SirajOnSurfaceVariantBrush"),
            },
        };
        grid.Children.Add(number);
        var texts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var head = Kit.Text(title, "SirajBody", bold: true);
        head.MaxLines = 2;
        head.TextTrimming = TextTrimming.CharacterEllipsis;
        texts.Children.Add(head);
        var items = AdhkarBook.OfChapter(chapter.Title);
        var (done, total) = Progress(chapter.Title, items);
        var line = loc.Plural("azkar_chapter_duas", chapter.Count);
        if (done > 0) line += loc.Sep + loc.Format("azkar_progress_of", loc.Number(done), loc.Number(total));
        texts.Children.Add(Kit.Text(line, "SirajCaption"));
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);
        return Kit.CardButton(grid, () => ShowSet(chapter.Title, title, loc["azkar_chapter_source_badge"], items), title,
            padding: new Thickness(14, 12, 14, 12));
    }

    private void OpenGroup(AdhkarGroup group) =>
        ShowSet(group.Key(), group.Title(Loc.Current.Language), Loc.Current[PurposeKey(group)], AdhkarBook.Of(group));

    private void ShowSet(string key, string title, string? subtitle, IReadOnlyList<Dhikr> items)
    {
        _open = (key, title, subtitle, items);
        var loc = Loc.Current;
        var settings = App.Settings.Current;
        var counts = new Dictionary<int, int>(App.Journal.AdhkarCounts(key, Today));

        var body = new StackPanel { Spacing = 12 };
        var column = new StackPanel { MaxWidth = 860, Padding = new Thickness(32, 8, 32, 48), Spacing = 16 };

        // The header: back, the set's name, the options, and how far today has got.
        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(Kit.Back(ShowHub));
        var names = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(Kit.Text(title, "SirajTitle", 24));
        if (subtitle != null) names.Children.Add(Kit.Text(subtitle, "SirajCaption"));
        Grid.SetColumn(names, 1);
        header.Children.Add(names);
        var options = Options();
        Grid.SetColumn(options, 2);
        header.Children.Add(options);
        column.Children.Add(header);

        var progressRow = new Grid { ColumnSpacing = 12 };
        progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var bar = Kit.Bar(0, items.Count);
        bar.VerticalAlignment = VerticalAlignment.Center;
        progressRow.Children.Add(bar);
        var progressText = Kit.Text("", "SirajCaption");
        progressText.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(progressText, 1);
        progressRow.Children.Add(progressText);
        var reset = Ui.Button(loc["azkar_reset_all"], async () =>
        {
            if (!await Kit.Confirm(XamlRoot, loc["adhkar_reset_title"], loc["adhkar_reset_message"], loc["adhkar_reset_confirm"])) return;
            App.Journal.ResetAdhkar(key);
            ShowSet(key, title, subtitle, items);
        });
        Grid.SetColumn(reset, 2);
        progressRow.Children.Add(reset);
        column.Children.Add(progressRow);

        var done = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 4) };
        var doneCard = Kit.Accent(done, new Thickness(24, 18, 24, 18));
        done.Children.Add(Kit.Text(loc["azkar_all_done_title"], "SirajHeading", brush: Kit.B("SirajOnPrimaryContainerBrush")));
        done.Children.Add(Kit.Text(loc["azkar_all_done_desc"], "SirajBody", brush: Kit.B("SirajOnPrimaryContainerBrush")));

        void UpdateProgress()
        {
            int finished = items.Count(d => counts.GetValueOrDefault(d.Id) >= d.Count);
            bar.Value = finished;
            progressText.Text = loc.Format("azkar_progress_of", loc.Number(finished), loc.Number(items.Count));
            doneCard.Visibility = finished == items.Count && items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        column.Children.Add(Kit.Text(loc["adhkar_tap_hint"], "SirajCaption"));
        var cards = new List<Button>();
        var scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        for (int i = 0; i < items.Count; i++)
        {
            var dhikr = items[i];
            int index = i;
            var card = DhikrCard(dhikr, index + 1, settings, out var refresh);
            refresh(counts.GetValueOrDefault(dhikr.Id));
            card.Click += (_, _) =>
            {
                int count = counts.GetValueOrDefault(dhikr.Id);
                // A dhikr said once unticks on a second click; a counted one stays finished at its count.
                int next = count >= dhikr.Count ? (dhikr.Count == 1 ? 0 : count) : count + 1;
                if (next == count) return;
                counts[dhikr.Id] = next;
                App.Journal.SetAdhkarCount(key, Today, dhikr.Id, next);
                refresh(next);
                Kit.Pulse(card, 1.015f);
                UpdateProgress();
                if (next >= dhikr.Count)
                {
                    // On to the next one not yet said, below this one.
                    var after = cards.Skip(index + 1).Zip(items.Skip(index + 1)).FirstOrDefault(p => counts.GetValueOrDefault(p.Second.Id) < p.Second.Count);
                    after.First?.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = Kit.Motion, VerticalAlignmentRatio = 0.25 });
                }
            };
            cards.Add(card);
            body.Children.Add(card);
        }
        body.Children.Add(doneCard);
        UpdateProgress();
        column.Children.Add(body);
        scroller.Content = column;
        bool animateIn = Host.Children.Count > 0;
        Host.Children.Clear();
        Host.Children.Add(scroller);
        if (animateIn) Kit.AnimatePageIn(scroller, PageTransitionKind.DrillIn, loc.IsRtl);
    }

    private static Button Options()
    {
        var loc = Loc.Current;
        var s = App.Settings.Current;
        var menu = new MenuFlyout();
        void Add(string key, bool on, Func<Core.Settings.AppSettings, bool, Core.Settings.AppSettings> set)
        {
            var item = new ToggleMenuFlyoutItem { Text = loc[key], IsChecked = on };
            item.Click += (_, _) => App.Settings.Update(x => set(x, item.IsChecked));
            menu.Items.Add(item);
        }
        Add("settings_show_translation", s.AdhkarTranslation, (x, v) => x with { AdhkarTranslation = v });
        Add("settings_show_transliteration", s.AdhkarTransliteration, (x, v) => x with { AdhkarTransliteration = v });
        Add("settings_show_virtues", s.AdhkarVirtues, (x, v) => x with { AdhkarVirtues = v });
        var button = new Button
        {
            Content = Kit.Icon("more_vert", 18, Kit.B("SirajOnSurfaceBrush")), Width = 40, Height = 40, Padding = new Thickness(0),
            CornerRadius = new CornerRadius(20), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0), Flyout = menu, VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(button, loc["settings_title"]);
        return button;
    }

    private static Button DhikrCard(Dhikr dhikr, int number, Core.Settings.AppSettings settings, out Action<int> refresh)
    {
        var loc = Loc.Current;
        var lang = loc.Language;
        var stack = new StackPanel { Spacing = 10 };

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(Kit.Pill(loc.Number(number), "SirajSurfaceContainerHighBrush", "SirajOnSurfaceVariantBrush"));
        var countText = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var countIcon = Kit.Icon("check", 14, Kit.B("SirajOnPrimaryBrush"));
        var countRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        countRow.Children.Add(countIcon);
        countRow.Children.Add(countText);
        var countPill = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 4, 12, 5), Child = countRow };
        Grid.SetColumn(countPill, 1);
        top.Children.Add(countPill);
        stack.Children.Add(top);

        stack.Children.Add(Kit.Arabic(dhikr.Text, dhikr.Text.Length > 600 ? 21 : 24));
        if (settings.AdhkarTransliteration && lang != "ar" && !string.IsNullOrWhiteSpace(dhikr.TransliterationEn))
        {
            var t = Kit.Text(dhikr.TransliterationEn, "SirajCaption");
            t.FontStyle = Windows.UI.Text.FontStyle.Italic;
            stack.Children.Add(t);
        }
        if (settings.AdhkarTranslation && dhikr.Translation(lang) is { Length: > 0 } meaning)
            stack.Children.Add(Kit.Text(meaning, "SirajBody"));
        if (settings.AdhkarVirtues && dhikr.LocalizedReference(lang) is { Length: > 0 } virtue)
        {
            var v = Kit.Text(virtue, "SirajCaption");
            var box = new Border
            {
                Background = Kit.B("SirajSurfaceContainerHighBrush"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8, 12, 9),
                Child = v,
            };
            stack.Children.Add(box);
        }

        var card = Kit.CardButton(stack, () => { }, null, padding: new Thickness(22, 18, 22, 20));
        refresh = count =>
        {
            bool finished = count >= dhikr.Count;
            countText.Text = $"{loc.Number(Math.Min(count, dhikr.Count))} / {loc.Number(dhikr.Count)}";
            countIcon.Visibility = finished ? Visibility.Visible : Visibility.Collapsed;
            countPill.Background = Kit.B(finished ? "SirajPrimaryBrush" : "SirajSecondaryContainerBrush");
            countText.Foreground = Kit.B(finished ? "SirajOnPrimaryBrush" : "SirajOnSecondaryContainerBrush");
            card.BorderBrush = Kit.B(finished ? "SirajPrimaryBrush" : "SirajOutlineVariantBrush");
            card.Opacity = finished ? 0.78 : 1;
            AutomationProperties.SetName(card, (finished
                ? loc.Format("adhkar_counter_done", loc.Number(count), loc.Number(dhikr.Count))
                : loc.Format("adhkar_counter_progress", loc.Number(count), loc.Number(dhikr.Count))) + ". " + loc["adhkar_tap_hint"]);
        };
        return card;
    }
}
