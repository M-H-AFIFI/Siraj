using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Hijri;
using Siraj.Core.Sky;
using Siraj.Core.Text;

namespace Siraj.App.Views;

public sealed partial class CalendarPage : Page
{
    private readonly StackPanel _body;
    private int _viewYear;
    private int _viewMonth;
    private DateOnly? _selectedDate;

    public CalendarPage()
    {
        InitializeComponent();
        var today = Today;
        var h = HijriCalendars.Current.ToHijri(today);
        _viewYear = h.Year;
        _viewMonth = h.Month;
        _selectedDate = today;

        var (root, body) = Kit.Page(Loc.Current["calendar_title"], null, 1080,
            back: () => App.MainWindowInstance?.Navigate("worship"));
        _body = body;
        Content = root;

        Loaded += (_, _) => { App.Theme.Changed += Refresh; App.Clock.DayChanged += Refresh; Refresh(); };
        Unloaded += (_, _) => { App.Theme.Changed -= Refresh; App.Clock.DayChanged -= Refresh; };
    }

    private static DateOnly Today => App.Clock.Place?.Today(App.Clock.Now) ?? DateOnly.FromDateTime(App.Clock.Now.LocalDateTime);

    private void Refresh()
    {
        if (!IsLoaded) return;
        _body.Children.Clear();
        var loc = Loc.Current;
        var today = Today;
        var cal = HijriCalendars.Current;
        bool eastern = Digits.UseEastern(App.Settings.Current.NumeralSystem, loc.Language);
        var hijriToday = cal.ToHijri(today);
        var upcoming = IslamicOccasions.Upcoming(today, cal);

        // 1. Today + Moon Tonight hero card
        var hero = BuildHeroCard(today, hijriToday, upcoming.FirstOrDefault(), eastern);
        Kit.PopIn(hero, 0);
        _body.Children.Add(hero);

        // 2. Monthly Hijri Calendar Grid
        var monthCard = BuildMonthCard(today, cal, eastern);
        Kit.PopIn(monthCard, 1);
        _body.Children.Add(monthCard);

        // 3. Upcoming Islamic Occasions
        _body.Children.Add(Ui.SectionWithIcon(loc["calendar_events_tab"], "lantern"));
        var eventCards = upcoming.Select(o => OccasionCard(o, eastern)).ToList();
        var grid = Kit.Columns(310, 12, eventCards);
        Kit.PopIn(grid, 2);
        _body.Children.Add(grid);
    }

    private UIElement BuildHeroCard(DateOnly today, HijriDay hijriToday, DatedOccasion? next, bool eastern)
    {
        var loc = Loc.Current;
        var phase = MoonPhase.At(App.Clock.Now);

        var grid = new Grid { ColumnSpacing = 20 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new MoonDisc { Width = 78, Height = 78, Phase = phase, VerticalAlignment = VerticalAlignment.Center });

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
        info.Children.Add(Kit.Pill(loc["calendar_today_title"], "SirajPrimarySoftBrush", "SirajPrimaryBrush"));
        info.Children.Add(Kit.Text(HijriText.Format(hijriToday, loc.Language, eastern), "SirajTitle", 22, bold: true));
        info.Children.Add(Kit.Text(HijriText.Gregorian(today, loc.Language, eastern), "SirajCaption", 13.5));
        info.Children.Add(Kit.Text($"{loc[MoonDisc.NameKey(phase.Name)]}{loc.Sep}{MoonDisc.Line(loc, phase, today, eastern)}", "SirajCaption", 12));
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        if (next != null)
        {
            var nextBox = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            nextBox.Children.Add(Kit.Text(loc["calendar_next_occasion"], "SirajCaption", 11.5));
            nextBox.Children.Add(Kit.Text(next.Occasion.Title(loc.Language), "SirajHeading", 15, bold: true));
            string daysLabel = next.DaysUntil == 0 ? loc["calendar_countdown_today"]
                : next.DaysUntil == 1 ? loc["calendar_countdown_tomorrow"]
                : loc.Plural("calendar_countdown_days_left", next.DaysUntil, loc.Number(next.DaysUntil));
            nextBox.Children.Add(Kit.Pill(daysLabel, "SirajGoldSoftBrush", "SirajGoldBrush"));
            Grid.SetColumn(nextBox, 2);
            grid.Children.Add(nextBox);
        }

        return Kit.Accent(grid, new Thickness(24, 20, 24, 20));
    }

    private UIElement BuildMonthCard(DateOnly today, HijriCalendar cal, bool eastern)
    {
        var loc = Loc.Current;
        var stack = new StackPanel { Spacing = 14 };

        // Month header + navigation arrows + Today button
        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var prevBtn = new Button
        {
            Content = Kit.Icon(loc.IsRtl ? "\uE76C" : "\uE76B", 14, Kit.B("SirajOnSurfaceBrush")),
            Width = 38, Height = 38, Padding = new Thickness(0), CornerRadius = new CornerRadius(19),
            Background = Kit.B("SirajGlassPillBrush"), BorderBrush = Kit.B("SirajGlassBorderBrush"), BorderThickness = new Thickness(1),
        };
        AutomationProperties.SetName(prevBtn, loc["calendar_prev_month"]);
        prevBtn.Click += (_, _) => StepMonth(-1);
        header.Children.Add(prevBtn);

        var titleStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        var monthLabel = Kit.Text($"{HijriText.MonthName(_viewMonth, loc.Language)} {HijriText.Year(_viewYear, loc.Language, eastern)}", "SirajHeading", 19, bold: true);
        monthLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleStack.Children.Add(monthLabel);

        var firstGreg = cal.FirstDayOf(_viewYear, _viewMonth);
        int daysInMonth = cal.LengthOfMonth(_viewYear, _viewMonth);
        var lastGreg = firstGreg.AddDays(daysInMonth - 1);
        string gregSpan = $"{HijriText.Gregorian(firstGreg, loc.Language, eastern, "MMM yyyy")} – {HijriText.Gregorian(lastGreg, loc.Language, eastern, "MMM yyyy")}";
        var gregLabel = Kit.Text(gregSpan, "SirajCaption", 12);
        gregLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleStack.Children.Add(gregLabel);
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        var rightNav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var todayH = cal.ToHijri(today);
        if (_viewYear != todayH.Year || _viewMonth != todayH.Month)
        {
            var backToday = Ui.Button(loc["calendar_back_to_today"], () =>
            {
                _viewYear = todayH.Year;
                _viewMonth = todayH.Month;
                _selectedDate = today;
                Refresh();
            });
            backToday.Padding = new Thickness(12, 5, 12, 6);
            rightNav.Children.Add(backToday);
        }
        var nextBtn = new Button
        {
            Content = Kit.Icon(loc.IsRtl ? "\uE76B" : "\uE76C", 14, Kit.B("SirajOnSurfaceBrush")),
            Width = 38, Height = 38, Padding = new Thickness(0), CornerRadius = new CornerRadius(19),
            Background = Kit.B("SirajGlassPillBrush"), BorderBrush = Kit.B("SirajGlassBorderBrush"), BorderThickness = new Thickness(1),
        };
        AutomationProperties.SetName(nextBtn, loc["calendar_next_month"]);
        nextBtn.Click += (_, _) => StepMonth(1);
        rightNav.Children.Add(nextBtn);
        Grid.SetColumn(rightNav, 2);
        header.Children.Add(rightNav);
        stack.Children.Add(header);

        // Weekday headers (Sun..Sat)
        var grid = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
        for (int c = 0; c < 7; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Reference Sunday 2026-11-22
        var refSun = new DateOnly(2026, 11, 22);
        for (int c = 0; c < 7; c++)
        {
            var wd = Kit.Text(HijriText.Gregorian(refSun.AddDays(c), loc.Language, eastern, "ddd"), "SirajCaption", 12, bold: true);
            wd.HorizontalAlignment = HorizontalAlignment.Center;
            wd.Margin = new Thickness(0, 4, 0, 6);
            Grid.SetColumn(wd, c);
            grid.Children.Add(wd);
        }

        // Occasions in this Hijri month
        var monthOccasions = IslamicOccasions.All.Where(o => o.HijriMonth == _viewMonth).ToDictionary(o => o.HijriDay);

        int startCol = (int)firstGreg.DayOfWeek; // 0 = Sunday
        int totalRows = (startCol + daysInMonth + 6) / 7;
        for (int r = 0; r < totalRows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int d = 1; d <= daysInMonth; d++)
        {
            var gDate = firstGreg.AddDays(d - 1);
            int cell = startCol + d - 1;
            int col = cell % 7;
            int row = cell / 7 + 1;

            bool isToday = gDate == today;
            bool isSelected = _selectedDate == gDate;
            bool isWhiteDay = d is 13 or 14 or 15;
            bool hasOccasion = monthOccasions.ContainsKey(d);
            bool isFriday = gDate.DayOfWeek == DayOfWeek.Friday;

            var inner = new StackPanel { Spacing = 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var hNum = Kit.Text(loc.Number(d), "SirajHeading", 16, bold: true,
                brush: Kit.B(isToday ? "SirajOnPrimaryBrush" : isFriday ? "SirajPrimaryBrush" : "SirajOnSurfaceBrush"));
            hNum.HorizontalAlignment = HorizontalAlignment.Center;
            inner.Children.Add(hNum);

            var gNum = Kit.Text(loc.Number(gDate.Day), "SirajCaption", 10.5,
                brush: Kit.B(isToday ? "SirajOnPrimaryBrush" : "SirajOnSurfaceVariantBrush"));
            gNum.HorizontalAlignment = HorizontalAlignment.Center;
            gNum.Opacity = isToday ? 0.85 : 0.72;
            inner.Children.Add(gNum);

            if (hasOccasion || isWhiteDay)
            {
                var dot = new Ellipse
                {
                    Width = 5, Height = 5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0),
                    Fill = Kit.B(isToday ? "SirajOnPrimaryBrush" : hasOccasion ? "SirajPrimaryBrush" : "SirajGoldBrush"),
                };
                inner.Children.Add(dot);
            }

            string bg = isToday ? "SirajPrimaryBrush"
                : isSelected ? "SirajPrimarySoftBrush"
                : isWhiteDay ? "SirajGoldSoftBrush"
                : "SirajGlassCardBrush";
            var btn = new Button
            {
                Content = inner, HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 56,
                Padding = new Thickness(4, 6, 4, 6), CornerRadius = new CornerRadius(14),
                Background = Kit.B(bg),
                BorderBrush = Kit.B(isSelected && !isToday ? "SirajPrimaryBrush" : "SirajGlassBorderBrush"),
                BorderThickness = new Thickness(isSelected && !isToday ? 1.5 : 1),
            };
            var captured = gDate;
            btn.Click += (_, _) => { _selectedDate = captured; Refresh(); };
            Grid.SetColumn(btn, col);
            Grid.SetRow(btn, row);
            grid.Children.Add(btn);
        }
        stack.Children.Add(grid);

        // Selected day details strip
        if (_selectedDate is { } sel)
        {
            var sh = cal.ToHijri(sel);
            var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (sel.DayOfWeek == DayOfWeek.Friday)
                badges.Children.Add(Kit.Pill(loc["calendar_badge_jumuah"], "SirajPrimarySoftBrush", "SirajPrimaryBrush"));
            if (sh.Day is 13 or 14 or 15)
                badges.Children.Add(Kit.Pill(loc["calendar_white_days_badge"], "SirajGoldSoftBrush", "SirajGoldBrush"));
            if (sel.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Thursday)
                badges.Children.Add(Kit.Pill(loc["calendar_sunnah_fast_badge"], "SirajSecondaryContainerBrush", "SirajOnSecondaryContainerBrush"));
            if (IslamicOccasions.All.FirstOrDefault(o => o.HijriMonth == sh.Month && o.HijriDay == sh.Day) is { } occ)
                badges.Children.Add(Kit.Pill(occ.Title(loc.Language), "SirajPrimaryBrush", "SirajOnPrimaryBrush"));

            var detail = new StackPanel { Spacing = 6 };
            detail.Children.Add(Kit.Text($"{HijriText.Format(sh, loc.Language, eastern)}{loc.Sep}{HijriText.Gregorian(sel, loc.Language, eastern)}", "SirajBody", 14, bold: true));
            if (badges.Children.Count > 0) detail.Children.Add(badges);
            stack.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10, 14, 10),
                Background = Kit.B("SirajGlassPillBrush"), BorderBrush = Kit.B("SirajGlassBorderBrush"), BorderThickness = new Thickness(1),
                Child = detail,
            });
        }

        // Legend row
        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Margin = new Thickness(4, 2, 4, 0) };
        legend.Children.Add(LegendDot("SirajPrimaryBrush", loc["calendar_legend_occasion"]));
        legend.Children.Add(LegendDot("SirajGoldBrush", loc["calendar_legend_white_days"]));
        stack.Children.Add(legend);

        return Kit.Card(stack, new Thickness(20));
    }

    private static StackPanel LegendDot(string brushKey, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = Kit.B(brushKey), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(Kit.Text(label, "SirajCaption", 12));
        return row;
    }

    private UIElement OccasionCard(DatedOccasion dated, bool eastern)
    {
        var loc = Loc.Current;
        string iconKey = dated.Occasion.Id switch
        {
            "ramadan_start" => "lantern",
            "last_ten_ramadan" => "night_of_power",
            "eid_al_fitr" or "eid_al_adha" => "eid_mosque",
            "day_of_arafah" => "arafah",
            "isra_miraj" => "dome_of_rock",
            "ashura" => "parted_sea",
            "mid_shaban" => "full_moon",
            _ => "crescent_star",
        };
        string daysLabel = dated.DaysUntil == 0 ? loc["calendar_countdown_today"]
            : dated.DaysUntil == 1 ? loc["calendar_countdown_tomorrow"]
            : loc.Plural("calendar_countdown_days_left", dated.DaysUntil, loc.Number(dated.DaysUntil));

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(Kit.IconBadge(iconKey, 42, 21,
            dated.IsToday ? "SirajPrimaryBrush" : "SirajPrimarySoftBrush",
            dated.IsToday ? "SirajOnPrimaryBrush" : "SirajPrimaryBrush"));

        var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        txt.Children.Add(Kit.Text(dated.Occasion.Title(loc.Language), "SirajHeading", 14.5, bold: true));
        string hStr = $"{loc.Number(dated.Occasion.HijriDay)} {HijriText.MonthName(dated.Occasion.HijriMonth, loc.Language)} {HijriText.Year(dated.HijriYear, loc.Language, eastern)}";
        string gStr = HijriText.Gregorian(dated.Date, loc.Language, eastern, "d MMM yyyy");
        txt.Children.Add(Kit.Text($"{hStr}{loc.Sep}{gStr}", "SirajCaption", 11.5));
        Grid.SetColumn(txt, 1);
        grid.Children.Add(txt);

        var pill = Kit.Pill(daysLabel,
            dated.IsToday ? "SirajPrimaryBrush" : "SirajGoldSoftBrush",
            dated.IsToday ? "SirajOnPrimaryBrush" : "SirajGoldBrush");
        Grid.SetColumn(pill, 2);
        grid.Children.Add(pill);

        return Kit.CardButton(grid, () =>
        {
            _viewYear = dated.HijriYear;
            _viewMonth = dated.Occasion.HijriMonth;
            _selectedDate = dated.Date;
            Refresh();
        }, name: dated.Occasion.Title(loc.Language), padding: new Thickness(14, 12, 14, 12));
    }

    private void StepMonth(int delta)
    {
        _viewMonth += delta;
        if (_viewMonth > 12) { _viewMonth = 1; _viewYear++; }
        else if (_viewMonth < 1) { _viewMonth = 12; _viewYear--; }
        Refresh();
    }
}
