using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Devotions;
using Siraj.Core.Hijri;
using Siraj.Core.Prayer;
using Siraj.Core.Text;
using HijriCalendar = Siraj.Core.Hijri.HijriCalendar;

namespace Siraj.App.Views;

public sealed partial class FastingPage : Page
{
    private readonly ContentControl _today = Slot(), _stats = Slot(), _calendar = Slot();
    private DateOnly _month;

    private static ContentControl Slot() => new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
        IsTabStop = false,
    };

    private static DateOnly Today => App.Clock.Place?.Today(App.Clock.Now) ?? DateOnly.FromDateTime(App.Clock.Now.LocalDateTime);

    private static HijriCalendar Calendar => HijriCalendars.Current;

    private static bool Eastern => Digits.UseEastern(App.Settings.Current.NumeralSystem, Loc.Current.Language);

    private static Dictionary<string, string> Log => App.Journal.Current.Fasts;

    public FastingPage()
    {
        InitializeComponent();
        var loc = Loc.Current;
        var (root, body) = Kit.Page(loc["hub_fasting_title"], loc["fasting_header_sub"],
            back: () => App.MainWindowInstance?.Navigate("worship"));
        Host.Children.Add(root);
        var today = Today;
        _month = new DateOnly(today.Year, today.Month, 1);
        body.Children.Add(Kit.Columns(360, 16, [_today, _stats]));
        body.Children.Add(Kit.Columns(360, 16, [_calendar, Upcoming(today)]));
        Refresh();
    }

    private void Refresh()
    {
        FillToday();
        FillStats();
        FillCalendar();
    }

    private void Toggle(DateOnly date)
    {
        string key = Fasting.Key(date);
        App.Journal.Update(j =>
        {
            if (!j.Fasts.Remove(key)) j.Fasts[key] = Fasting.TypeOf(date, Calendar);
        });
        Refresh();
    }

    private void FillToday()
    {
        var loc = Loc.Current;
        var today = Today;
        var on = Kit.B("SirajOnPrimaryContainerBrush");
        var stack = new StackPanel { Spacing = 14 };
        var head = new StackPanel { Spacing = 2 };
        head.Children.Add(Kit.Text(loc["fasting_imsakiah_title"], "SirajHeading", brush: on));
        head.Children.Add(Kit.Text(HijriText.Format(Calendar.ToHijri(today), loc.Language, Eastern), "SirajCaption", brush: on));
        stack.Children.Add(head);

        if (App.Clock.Day is { } day && App.Clock.Place != null)
        {
            var times = new Grid { ColumnSpacing = 12 };
            times.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            times.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            times.Children.Add(TimeTile("", loc["tools_fasting_suhoor"], loc.Time(day[PrayerType.Fajr].Time)));
            var iftar = TimeTile("", loc["tools_fasting_iftar"], loc.Time(day[PrayerType.Maghrib].Time));
            Grid.SetColumn(iftar, 1);
            times.Children.Add(iftar);
            stack.Children.Add(times);
        }
        else stack.Children.Add(Kit.Text(loc["tools_fasting_times_unavailable"], "SirajBody", brush: on));

        bool fasting = Log.ContainsKey(Fasting.Key(today));
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        label.Children.Add(Kit.Icon(fasting ? "" : "", 16, Kit.B(fasting ? "SirajOnPrimaryBrush" : "SirajOnSurfaceBrush")));
        label.Children.Add(Kit.Text(fasting ? loc["fasting_today_yes"] : loc["fasting_today_question"], "SirajBody", bold: true,
            brush: Kit.B(fasting ? "SirajOnPrimaryBrush" : "SirajOnSurfaceBrush")));
        var toggle = new ToggleButton
        {
            Content = label, IsChecked = fasting, HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(16, 12, 16, 12),
            CornerRadius = new CornerRadius(22),
        };
        if (!fasting)
        {
            toggle.Background = Kit.Clone("SirajSurfaceBrush");
            toggle.Resources["ToggleButtonBackground"] = Kit.Clone("SirajSurfaceBrush");
        }
        AutomationProperties.SetName(toggle, loc["fasting_action_fasting"]);
        toggle.Click += (_, _) => Toggle(today);
        stack.Children.Add(toggle);
        _today.Content = Kit.Accent(stack, new Thickness(24, 20, 24, 22));
    }

    private static Border TimeTile(string glyph, string label, string time)
    {
        var stack = new StackPanel { Spacing = 4 };
        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        top.Children.Add(Kit.Icon(glyph, 14, Kit.B("SirajPrimaryBrush")));
        top.Children.Add(Kit.Text(label, "SirajCaption"));
        stack.Children.Add(top);
        stack.Children.Add(Kit.Text(time, "SirajTitle", 26));
        return new Border
        {
            Background = Kit.B("SirajSurfaceBrush"), CornerRadius = new CornerRadius(16), Padding = new Thickness(16, 12, 16, 14), Child = stack,
        };
    }

    private void FillStats()
    {
        var loc = Loc.Current;
        int streak = Fasting.Streak(Log, Today);
        int ramadan = Log.Values.Count(v => v == "ramadan");
        int sunnah = Log.Count - ramadan;
        var grid = new Grid { ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var stats = new (string Glyph, int Value, string Label)[]
        {
            ("", streak, loc["fasting_stat_streak"]),
            ("", ramadan, loc["fasting_stat_ramadan"]),
            ("", sunnah, loc["fasting_stat_sunnah"]),
        };
        for (int i = 0; i < stats.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var s = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            var icon = Kit.Icon(stats[i].Glyph, 18, Kit.B("SirajGoldBrush"));
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            s.Children.Add(icon);
            var n = Kit.Text(loc.Number(stats[i].Value), "SirajTitle", 34, brush: Kit.B("SirajPrimaryBrush"));
            n.HorizontalAlignment = HorizontalAlignment.Center;
            s.Children.Add(n);
            var l = Kit.Text(stats[i].Label, "SirajCaption");
            l.HorizontalAlignment = HorizontalAlignment.Center;
            l.HorizontalTextAlignment = TextAlignment.Center;
            s.Children.Add(l);
            Grid.SetColumn(s, i);
            grid.Children.Add(s);
        }
        _stats.Content = Kit.Card(grid, new Thickness(16, 20, 16, 20));
    }

    private static UIElement Upcoming(DateOnly today)
    {
        var loc = Loc.Current;
        var lang = loc.Language;
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(Ui.Section(loc["fasting_upcoming_header"]));
        foreach (var u in Fasting.Upcoming(today, Calendar))
        {
            var row = new Grid { ColumnSpacing = 14, Padding = new Thickness(0, 8, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var badge = new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Background = Kit.B("SirajSecondaryContainerBrush"),
                Child = new TextBlock
                {
                    Text = loc.Number(u.HijriDay), FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = Kit.B("SirajOnSecondaryContainerBrush"), HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            row.Children.Add(badge);
            var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Kit.Text(loc[u.Occasion switch
            {
                FastOccasion.Monday => "fasting_monday_sunnah",
                FastOccasion.Thursday => "fasting_thursday_sunnah",
                FastOccasion.WhiteDays => "fasting_white_days",
                FastOccasion.Ashura => "fasting_ashura",
                FastOccasion.Arafah => "fasting_arafah",
                _ => "fasting_type_ramadan",
            }], "SirajBody", bold: true));
            text.Children.Add(Kit.Text(
                HijriText.Gregorian(u.Date, lang, Eastern, "dddd d MMMM") + loc.Sep + loc.Number(u.HijriDay) + " " + HijriText.MonthName(u.HijriMonth, lang),
                "SirajCaption"));
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            string when = u.DaysAway switch
            {
                0 => loc["tools_fasting_today"],
                1 => loc["tools_fasting_tomorrow"],
                _ => loc.Plural("tools_fasting_in_days", u.DaysAway),
            };
            var pill = u.DaysAway == 0 ? Kit.Pill(when, "SirajPrimaryBrush", "SirajOnPrimaryBrush") : Kit.Pill(when);
            pill.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(pill, 2);
            row.Children.Add(pill);
            stack.Children.Add(row);
        }
        return Kit.Card(stack, new Thickness(24, 18, 24, 18));
    }

    private void FillCalendar()
    {
        var loc = Loc.Current;
        var lang = loc.Language;
        var today = Today;
        var stack = new StackPanel { Spacing = 10 };

        var head = new Grid { ColumnSpacing = 8 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(Step(false, loc["calendar_prev_month"], () => { _month = _month.AddMonths(-1); FillCalendar(); }));
        var title = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        var month = Kit.Text(HijriText.Gregorian(_month, lang, Eastern, "MMMM yyyy"), "SirajHeading");
        month.HorizontalAlignment = HorizontalAlignment.Center;
        title.Children.Add(month);
        var last = _month.AddMonths(1).AddDays(-1);
        HijriDay h1 = Calendar.ToHijri(_month), h2 = Calendar.ToHijri(last);
        string hijri = h1.Month == h2.Month
            ? HijriText.MonthName(h1.Month, lang) + " " + HijriText.Year(h1.Year, lang, Eastern)
            : HijriText.MonthName(h1.Month, lang) + " – " + HijriText.MonthName(h2.Month, lang) + " " + HijriText.Year(h2.Year, lang, Eastern);
        var hijriText = Kit.Text(hijri, "SirajCaption", brush: Kit.B("SirajPrimaryBrush"));
        hijriText.HorizontalAlignment = HorizontalAlignment.Center;
        title.Children.Add(hijriText);
        Grid.SetColumn(title, 1);
        head.Children.Add(title);
        var next = Step(true, loc["calendar_next_month"], () => { _month = _month.AddMonths(1); FillCalendar(); });
        Grid.SetColumn(next, 2);
        head.Children.Add(next);
        stack.Children.Add(head);

        var culture = CultureInfo.GetCultureInfo(lang switch { "ar" => "ar-EG", "ur" => "ur-PK", "tr" => "tr-TR", _ => "en-GB" });
        var firstDay = lang == "en" ? DayOfWeek.Sunday : culture.DateTimeFormat.FirstDayOfWeek;
        var grid = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
        for (int c = 0; c < 7; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < 7; c++)
        {
            var name = Kit.Text(culture.DateTimeFormat.AbbreviatedDayNames[((int)firstDay + c) % 7], "SirajCaption", 12);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(name, c);
            grid.Children.Add(name);
        }
        int offset = ((int)_month.DayOfWeek - (int)firstDay + 7) % 7;
        int days = DateTime.DaysInMonth(_month.Year, _month.Month);
        int rows = (offset + days + 6) / 7;
        for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
        for (int d = 0; d < days; d++)
        {
            var date = _month.AddDays(d);
            var cell = Cell(date, today);
            Grid.SetRow(cell, 1 + (offset + d) / 7);
            Grid.SetColumn(cell, (offset + d) % 7);
            grid.Children.Add(cell);
        }
        stack.Children.Add(grid);

        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        legend.Children.Add(Key("SirajGoldBrush", loc["calendar_sunnah_fast_badge"]));
        legend.Children.Add(Key("SirajPrimaryBrush", loc["calendar_legend_white_days"]));
        stack.Children.Add(legend);
        stack.Children.Add(Kit.Text(loc["win_fasting_tap_day"], "SirajCaption"));
        _calendar.Content = Kit.Card(stack, new Thickness(20, 18, 20, 18));
    }

    private static Button Step(bool forward, string name, Action click)
    {
        // The arrows follow the reading direction: "next" points the way the text runs.
        bool right = forward != Loc.Current.IsRtl;
        var button = new Button
        {
            Content = Kit.Icon(right ? "" : "", 14), Width = 36, Height = 36, Padding = new Thickness(0),
            CornerRadius = new CornerRadius(18), Background = Kit.B("SirajSurfaceContainerBrush"), BorderThickness = new Thickness(0),
        };
        AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    private static StackPanel Key(string brush, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = Kit.B(brush), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(Kit.Text(label, "SirajCaption", 12));
        return row;
    }

    private Button Cell(DateOnly date, DateOnly today)
    {
        var loc = Loc.Current;
        var hijri = Calendar.ToHijri(date);
        bool fasted = Log.ContainsKey(Fasting.Key(date));
        bool isToday = date == today, future = date > today;
        bool ramadan = hijri.Month == 9;
        bool white = hijri.Day is >= 13 and <= 15 && !ramadan && !(hijri.Month == 12 && hijri.Day == 13);
        bool weekly = date.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Thursday;

        string fg = fasted ? "SirajOnPrimaryBrush" : "SirajOnSurfaceBrush";
        var content = new Grid();
        var numbers = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var day = Kit.Text(loc.Number(date.Day), "SirajBody", 15, bold: isToday, brush: Kit.B(fg));
        day.HorizontalAlignment = HorizontalAlignment.Center;
        numbers.Children.Add(day);
        var h = Kit.Text(loc.Number(hijri.Day), "SirajCaption", 10, brush: Kit.B(fasted ? "SirajOnPrimaryBrush" : "SirajOnSurfaceVariantBrush"));
        h.HorizontalAlignment = HorizontalAlignment.Center;
        numbers.Children.Add(h);
        content.Children.Add(numbers);
        if (!fasted && (weekly || white))
        {
            var dots = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, -2),
            };
            if (weekly) dots.Children.Add(new Ellipse { Width = 5, Height = 5, Fill = Kit.B("SirajGoldBrush") });
            if (white) dots.Children.Add(new Ellipse { Width = 5, Height = 5, Fill = Kit.B("SirajPrimaryBrush") });
            content.Children.Add(dots);
        }

        string background = fasted ? "SirajPrimaryBrush" : ramadan ? "SirajSecondaryContainerBrush" : "SirajSurfaceContainerLowBrush";
        var cell = Kit.CardButton(content, () => Toggle(date),
            HijriText.Gregorian(date, loc.Language, Eastern, "dddd d MMMM") + (fasted ? loc.Sep + loc["tools_fasting_logged_a11y"] : ""),
            background, new Thickness(0, 4, 0, 6));
        cell.HorizontalAlignment = HorizontalAlignment.Stretch;
        cell.VerticalAlignment = VerticalAlignment.Stretch;
        cell.CornerRadius = new CornerRadius(12);
        cell.BorderThickness = new Thickness(isToday ? 2 : 0);
        cell.BorderBrush = Kit.Clone("SirajPrimaryBrush");
        cell.Resources["ButtonBorderBrushPointerOver"] = Kit.Clone("SirajPrimaryBrush");
        if (future)
        {
            cell.IsEnabled = false;
            cell.Opacity = 0.45;
        }
        return cell;
    }
}
