using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Quran;
using Siraj.Core.Text;

namespace Siraj.App.Views;

public sealed partial class KhatmaPage : Page
{
    private readonly StackPanel _body;
    private int _chosenDays = 30;
    private bool _startFromLastRead;
    private bool _showAllDays;

    public KhatmaPage()
    {
        InitializeComponent();
        var (root, body) = Kit.Page(Loc.Current["kh_title"], Loc.Current["kh_setup_sub"], 940,
            back: () => App.MainWindowInstance?.Navigate("quran"));
        _body = body;
        Content = root;
        Loaded += (_, _) =>
        {
            App.Journal.Changed += Refresh;
            App.Theme.Changed += Refresh;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            App.Journal.Changed -= Refresh;
            App.Theme.Changed -= Refresh;
        };
    }

    private void Refresh()
    {
        if (!IsLoaded) return;
        _body.Children.Clear();
        var state = App.Journal.Current.Khatma;
        if (state == null) BuildSetup();
        else BuildActive(state);
    }

    private void BuildSetup()
    {
        var loc = Loc.Current;
        var today = DateOnly.FromDateTime(DateTime.Now);
        bool eastern = Digits.UseEastern(App.Settings.Current.NumeralSystem, loc.Language);
        int lastPage = App.Settings.Current.LastRead?.Page ?? 1;
        int startPage = _startFromLastRead && lastPage > 1 ? lastPage : 1;

        var hero = new StackPanel { Spacing = 14 };
        var headRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        headRow.Children.Add(Kit.IconBadge("khatma", 52, 26));
        var headText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 3 };
        headText.Children.Add(Kit.Text(loc["kh_setup_headline"], "SirajHeading", 20, bold: true));
        headText.Children.Add(Kit.Text(loc["kh_setup_sub"], "SirajCaption"));
        headRow.Children.Add(headText);
        hero.Children.Add(headRow);

        var presets = new List<UIElement>();
        foreach (int days in KhatmaPlanner.Presets)
        {
            bool selected = _chosenDays == days;
            int ppd = KhatmaPlanner.PagesPerDay(days, startPage);
            var inner = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            var num = Kit.Text(loc.Number(days), "SirajTitle", 26, bold: true,
                brush: Kit.B(selected ? "SirajPrimaryBrush" : "SirajOnSurfaceBrush"));
            num.HorizontalAlignment = HorizontalAlignment.Center;
            inner.Children.Add(num);
            var perDay = Kit.Text(loc.Format("kh_page_n", loc.Number(ppd)) + " / " + loc.Format("kh_day_n", loc.Number(1)).Split(' ')[0], "SirajCaption", 11.5);
            perDay.HorizontalAlignment = HorizontalAlignment.Center;
            inner.Children.Add(perDay);

            int d = days;
            var btn = Kit.CardButton(inner, () =>
            {
                _chosenDays = d;
                Refresh();
            },
                background: selected ? "SirajPrimarySoftBrush" : "SirajGlassCardBrush",
                padding: new Thickness(14, 14, 14, 14));
            if (selected) btn.BorderBrush = Kit.B("SirajPrimaryBrush");
            presets.Add(btn);
        }
        hero.Children.Add(Kit.Columns(150, 10, presets));

        var customBox = new NumberBox
        {
            Minimum = 1, Maximum = 365, Value = _chosenDays, Width = 140,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };
        LocalNumbers.Apply(customBox);
        customBox.ValueChanged += (_, e) =>
        {
            if (double.IsNaN(e.NewValue)) return;
            int v = Math.Clamp((int)e.NewValue, 1, 365);
            if (v != _chosenDays)
            {
                _chosenDays = v;
                Refresh();
            }
        };
        hero.Children.Add(Ui.Card(
            Ui.Row(loc["kh_custom_days"], loc.Format("kh_finishes_on", HijriText.Gregorian(today.AddDays(_chosenDays), loc.Language, eastern, "d MMMM yyyy")), customBox),
            Ui.Row(loc["kh_start_from"],
                lastPage > 1 ? loc.Format("kh_from_last_read", loc.Number(lastPage)) : loc["kh_from_beginning"],
                Ui.Segments<bool>(
                [
                    (false, loc["kh_from_beginning"]),
                    (true, loc.Format("kh_from_last_read", loc.Number(lastPage))),
                ], _startFromLastRead, v =>
                {
                    _startFromLastRead = v;
                    Refresh();
                }), stack: true)));

        var summaryLine = Kit.Text(
            $"{loc.Format("kh_pages_range", loc.Number(startPage), loc.Number(QuranDatabase.PageCount))}{loc.Sep}{loc.Format("kh_finishes_on", HijriText.Gregorian(today.AddDays(_chosenDays), loc.Language, eastern, "d MMMM yyyy"))}",
            "SirajCaption");
        hero.Children.Add(summaryLine);

        var beginBtn = Ui.Button(loc["kh_begin"], () =>
        {
            var created = KhatmaPlanner.Create(today, _chosenDays, startPage);
            App.Journal.Update(j => j.Khatma = created);
        }, primary: true);
        beginBtn.HorizontalAlignment = HorizontalAlignment.Left;
        hero.Children.Add(beginBtn);

        var card = Kit.Card(hero, new Thickness(24));
        Kit.PopIn(card, 0);
        _body.Children.Add(card);
    }

    private void BuildActive(KhatmaState state)
    {
        var loc = Loc.Current;
        var today = DateOnly.FromDateTime(DateTime.Now);
        bool eastern = Digits.UseEastern(App.Settings.Current.NumeralSystem, loc.Language);
        var summary = KhatmaPlanner.Summarize(state, today);

        var topGrid = new Grid { ColumnSpacing = 24 };
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var ringHost = new Grid { Width = 128, Height = 128, VerticalAlignment = VerticalAlignment.Center };
        ringHost.Children.Add(new Ring { Thickness = 11, Fraction = summary.Progress });
        var pctStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var pct = Kit.Text($"{loc.Number((int)Math.Round(summary.Progress * 100))}%", "SirajTitle", 26, bold: true, brush: Kit.B("SirajPrimaryBrush"));
        pct.HorizontalAlignment = HorizontalAlignment.Center;
        pctStack.Children.Add(pct);
        var ratio = Kit.Text($"{loc.Number(summary.CompletedDays)}/{loc.Number(summary.TargetDays)}", "SirajCaption", 12);
        ratio.HorizontalAlignment = HorizontalAlignment.Center;
        pctStack.Children.Add(ratio);
        ringHost.Children.Add(pctStack);
        topGrid.Children.Add(ringHost);

        var stats = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        stats.Children.Add(Kit.Text(summary.IsFinished ? loc["kh_finished_title"] : loc["kh_on_track"], "SirajHeading", 20, bold: true));
        stats.Children.Add(Kit.Text(loc.Format("kh_progress", loc.Number(summary.PagesRead), loc.Number(summary.TotalPages)), "SirajBody"));
        var pills = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        pills.Children.Add(Kit.Pill(loc.Format("kh_finishes_on", HijriText.Gregorian(summary.ProjectedEnd, loc.Language, eastern, "d MMM")),
            "SirajPrimarySoftBrush", "SirajPrimaryBrush"));
        if (summary.Streak > 0)
            pills.Children.Add(Kit.Pill($"{loc["fasting_stat_streak"]}: {loc.Number(summary.Streak)}", "SirajGoldSoftBrush", "SirajGoldBrush"));
        stats.Children.Add(pills);
        Grid.SetColumn(stats, 1);
        topGrid.Children.Add(stats);

        var overviewCard = Kit.Card(topGrid, new Thickness(24));
        Kit.PopIn(overviewCard, 0);
        _body.Children.Add(overviewCard);

        var portion = summary.TodayPortion;
        var portionStack = new StackPanel { Spacing = 12 };
        var portionHeader = new Grid();
        portionHeader.Children.Add(Kit.Text(summary.IsFinished ? loc["kh_finished_title"] : loc["kh_today"], "SirajHeading", 17, bold: true));
        var dayBadge = Kit.Pill(loc.Format("kh_day_n", loc.Number(portion.DayNumber)), "SirajPrimarySoftBrush", "SirajPrimaryBrush");
        dayBadge.HorizontalAlignment = HorizontalAlignment.Right;
        portionHeader.Children.Add(dayBadge);
        portionStack.Children.Add(portionHeader);

        if (summary.IsFinished)
        {
            portionStack.Children.Add(Kit.Text(loc["kh_finished_body"], "SirajBody"));
            var newBtn = Ui.Button(loc["kh_start_new"], () => App.Journal.Update(j => j.Khatma = null), primary: true);
            newBtn.HorizontalAlignment = HorizontalAlignment.Left;
            portionStack.Children.Add(newBtn);
        }
        else
        {
            string range = loc.Format("kh_pages_range", loc.Number(portion.FromPage), loc.Number(portion.ToPage));
            string surahHint = SurahSpanHint(portion.FromPage, portion.ToPage);
            portionStack.Children.Add(Kit.Text(string.IsNullOrEmpty(surahHint) ? range : $"{range}{loc.Sep}{surahHint}", "SirajBody", 16, bold: true));

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            actions.Children.Add(Ui.Button(loc["kh_read_now"], () =>
                App.MainWindowInstance?.OpenReader(new ReaderArgs(portion.FromPage)), primary: true));
            actions.Children.Add(Ui.Button(portion.Completed ? loc["kh_undo"] : loc["kh_mark_done"], () =>
                ToggleDay(portion.DayNumber)));
            portionStack.Children.Add(actions);
        }

        var todayCard = Kit.Accent(portionStack, new Thickness(22));
        Kit.PopIn(todayCard, 1);
        _body.Children.Add(todayCard);

        var schedHeader = new Grid { Margin = new Thickness(4, 6, 4, 0) };
        schedHeader.Children.Add(Kit.Text(loc["kh_schedule"], "SirajHeading", 16, bold: true));
        if (summary.Portions.Count > 15)
        {
            var toggleBtn = new HyperlinkButton
            {
                Content = _showAllDays ? loc["kh_schedule_show_less"] : loc.Format("kh_schedule_show_all", loc.Number(summary.Portions.Count)),
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            toggleBtn.Click += (_, _) =>
            {
                _showAllDays = !_showAllDays;
                Refresh();
            };
            schedHeader.Children.Add(toggleBtn);
        }
        _body.Children.Add(schedHeader);

        var shown = _showAllDays ? summary.Portions : summary.Portions.Take(15).ToList();
        var tiles = new List<UIElement>(shown.Count);
        foreach (var p in shown)
        {
            var tileGrid = new Grid { ColumnSpacing = 10 };
            tileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new Border
            {
                Width = 30, Height = 30, CornerRadius = new CornerRadius(15),
                Background = Kit.B(p.Completed ? "SirajPrimaryBrush" : p.IsToday ? "SirajPrimarySoftBrush" : "SirajSurfaceContainerHighBrush"),
                Child = p.Completed
                    ? Kit.Icon("check", 15, Kit.B("SirajOnPrimaryBrush"))
                    : Kit.Text(loc.Number(p.DayNumber), "SirajCaption", 12, bold: true,
                        brush: Kit.B(p.IsToday ? "SirajPrimaryBrush" : "SirajOnSurfaceVariantBrush")),
            };
            if (!p.Completed && check.Child is TextBlock tb)
            {
                tb.HorizontalAlignment = HorizontalAlignment.Center;
                tb.VerticalAlignment = VerticalAlignment.Center;
            }
            tileGrid.Children.Add(check);

            var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
            txt.Children.Add(Kit.Text(loc.Format("kh_day_n", loc.Number(p.DayNumber)), "SirajBody", 13.5, bold: true));
            txt.Children.Add(Kit.Text(loc.Format("kh_pages_range", loc.Number(p.FromPage), loc.Number(p.ToPage)), "SirajCaption", 11.5));
            Grid.SetColumn(txt, 1);
            tileGrid.Children.Add(txt);

            if (p.IsToday && !p.Completed)
            {
                var due = Kit.Pill(loc["kh_due_today"], "SirajGoldSoftBrush", "SirajGoldBrush");
                Grid.SetColumn(due, 2);
                tileGrid.Children.Add(due);
            }

            int dayNum = p.DayNumber;
            var btn = Kit.CardButton(tileGrid, () => ToggleDay(dayNum),
                background: p.Completed ? "SirajPrimarySoftBrush" : "SirajGlassCardBrush",
                padding: new Thickness(12, 10, 12, 10));
            if (p.IsToday) btn.BorderBrush = Kit.B("SirajPrimaryBrush");
            tiles.Add(btn);
        }
        var grid = Kit.Columns(220, 10, tiles);
        Kit.PopIn(grid, 2);
        _body.Children.Add(grid);

        var endBtn = Ui.Button(loc["kh_end"], async () =>
        {
            if (await Kit.Confirm(XamlRoot, loc["kh_end"], loc["kh_end_confirm"], loc["kh_end_action"]))
                App.Journal.Update(j => j.Khatma = null);
        });
        endBtn.HorizontalAlignment = HorizontalAlignment.Left;
        endBtn.Margin = new Thickness(0, 8, 0, 0);
        _body.Children.Add(endBtn);
    }

    private void ToggleDay(int dayNumber)
    {
        App.Journal.Update(j =>
        {
            if (j.Khatma == null) return;
            if (!j.Khatma.CompletedDays.Remove(dayNumber)) j.Khatma.CompletedDays.Add(dayNumber);
        });
    }

    private static string SurahSpanHint(int fromPage, int toPage)
    {
        if (App.Quran.Status != QuranService.State.Ready) return "";
        var db = App.Quran.Database;
        var loc = Loc.Current;
        var ayahs = db.Ayahs();
        var a1 = ayahs.FirstOrDefault(a => a.Page >= fromPage);
        var a2 = ayahs.LastOrDefault(a => a.Page <= toPage);
        if (a1 == null || a2 == null) return "";
        var s1 = db.Surah(a1.Surah);
        var s2 = db.Surah(a2.Surah);
        string n1 = loc.IsRtl ? s1.Name : s1.EnglishName;
        string n2 = loc.IsRtl ? s2.Name : s2.EnglishName;
        return a1.Surah == a2.Surah ? n1 : $"{n1} → {n2}";
    }
}
