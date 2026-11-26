using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Devotions;
using Windows.Globalization.NumberFormatting;

namespace Siraj.App.Views;

public sealed partial class ZakatPage : Page
{
    private readonly NumberBox _cash, _gold, _silver, _goldPrice, _silverPrice;
    private readonly ContentControl _result = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, IsTabStop = false };
    private bool _loading = true;

    private static ZakatInputs Inputs => App.Journal.Current.Zakat;

    public ZakatPage()
    {
        InitializeComponent();
        var loc = Loc.Current;
        var (root, body) = Kit.Page(loc["hub_zakat_title"], loc["hub_zakat_desc"],
            back: () => App.MainWindowInstance?.Navigate("worship"));
        Host.Children.Add(root);
        var currency = Zakat.Currency(Inputs.Currency);

        _cash = Number(Inputs.Cash, v => Inputs.Cash = v);
        _gold = Number(Inputs.GoldGrams, v => Inputs.GoldGrams = v);
        _silver = Number(Inputs.SilverGrams, v => Inputs.SilverGrams = v);
        _goldPrice = Number(Inputs.GoldPrice, v => Inputs.GoldPrice = v);
        _silverPrice = Number(Inputs.SilverPrice, v => Inputs.SilverPrice = v);

        var form = new StackPanel { Spacing = 14 };
        var picker = Ui.Choice(Zakat.Currencies.Select(c => (c.Code, $"{c.Name} ({c.Code})")).ToList(), currency.Code, ChangeCurrency, 320);
        picker.HorizontalAlignment = HorizontalAlignment.Stretch;
        form.Children.Add(Field(loc["zakat_select_currency"], picker));

        form.Children.Add(Ui.Section(loc["zakat_total_cash"]));
        form.Children.Add(Kit.Text(loc["zakat_cash_desc"], "SirajCaption"));
        var cashLabel = Kit.Text(loc.Format("zakat_cash_label", currency.Symbol), "SirajCaption");
        form.Children.Add(Field(cashLabel, _cash));

        form.Children.Add(Ui.Section(loc["zakat_gold_silver"]));
        form.Children.Add(Kit.Text(loc["zakat_metals_desc"], "SirajCaption"));
        form.Children.Add(Pair(Field(loc["zakat_gold_grams_label"], _gold), Field(loc["zakat_silver_grams_label"], _silver)));

        var goldLabel = Kit.Text(loc.Format("zakat_gold_price_label", currency.Symbol), "SirajCaption");
        var silverLabel = Kit.Text(loc.Format("zakat_silver_price_label", currency.Symbol), "SirajCaption");
        var prices = new Expander
        {
            Header = loc["zakat_adjust_prices"], HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = Pair(Field(goldLabel, _goldPrice), Field(silverLabel, _silverPrice)),
        };
        form.Children.Add(prices);
        _relabel = c =>
        {
            cashLabel.Text = loc.Format("zakat_cash_label", c.Symbol);
            goldLabel.Text = loc.Format("zakat_gold_price_label", c.Symbol);
            silverLabel.Text = loc.Format("zakat_silver_price_label", c.Symbol);
        };

        var side = new StackPanel { Spacing = 16 };
        side.Children.Add(_result);
        var about = new StackPanel { Spacing = 6 };
        about.Children.Add(Kit.Text(loc["tools_zakat_nisab_title"], "SirajHeading"));
        var aboutBody = Kit.Text(loc.Format("tools_zakat_nisab_body", loc.Number((long)Zakat.SilverNisabGrams), loc.Number((long)Zakat.GoldNisabGrams), RatePercent()), "SirajCaption");
        aboutBody.LineHeight = 20;
        about.Children.Add(aboutBody);
        side.Children.Add(Kit.Card(about, new Thickness(24, 18, 24, 20)));

        body.Children.Add(Kit.Columns(380, 16, [Kit.Card(form, new Thickness(24, 20, 24, 24)), side]));
        _loading = false;
        Show();
    }

    private Action<ZakatCurrency> _relabel = _ => { };

    private void ChangeCurrency(string code)
    {
        if (_loading) return;
        var c = Zakat.Currency(code);
        App.Journal.Update(j =>
        {
            j.Zakat.Currency = c.Code;
            j.Zakat.GoldPrice = c.DefaultGoldPrice;
            j.Zakat.SilverPrice = c.DefaultSilverPrice;
        });
        _loading = true;
        _goldPrice.Value = c.DefaultGoldPrice;
        _silverPrice.Value = c.DefaultSilverPrice;
        _loading = false;
        _relabel(c);
        Show();
    }

    private NumberBox Number(double value, Action<double> set)
    {
        var box = new NumberBox
        {
            Value = value, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
            HorizontalAlignment = HorizontalAlignment.Stretch, ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
            NumberFormatter = new DecimalFormatter
            {
                IsGrouped = true, FractionDigits = 0,
                NumberRounder = new IncrementNumberRounder { Increment = 0.01, RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp },
            },
        };
        box.ValueChanged += (_, e) =>
        {
            if (_loading) return;
            double v = double.IsNaN(e.NewValue) ? 0 : Math.Max(0, e.NewValue);
            App.Journal.Update(_ => set(v));
            Show();
        };
        return box;
    }

    private static StackPanel Field(string label, FrameworkElement input) => Field(Kit.Text(label, "SirajCaption"), input);

    private static StackPanel Field(TextBlock label, FrameworkElement input)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(label);
        stack.Children.Add(input);
        return stack;
    }

    private static Grid Pair(UIElement a, UIElement b)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(a);
        Grid.SetColumn((FrameworkElement)b, 1);
        grid.Children.Add(b);
        return grid;
    }

    // The left-to-right mark keeps an Arabic symbol after the amount on an English page: a text block
    // takes its direction from the first strong letter, which would otherwise be the symbol's.
    private static string Money(double value, ZakatCurrency currency)
    {
        var loc = Loc.Current;
        string amount = value.ToString("N2", CultureInfo.InvariantCulture);
        // Arabic-Indic digits take the Arabic separators (٬ and ٫), not a Latin comma and point.
        if (Core.Text.Digits.UseEastern(App.Settings.Current.NumeralSystem, loc.Language))
            amount = loc.Digitize(amount.Replace(",", "\u066C").Replace(".", "\u066B"));
        return (loc.IsRtl ? "" : "\u200E") + amount + " " + currency.Symbol;
    }

    private void Show()
    {
        var loc = Loc.Current;
        var currency = Zakat.Currency(Inputs.Currency);
        var r = Zakat.Calculate(Inputs);
        var on = Kit.B("SirajOnPrimaryContainerBrush");
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(Kit.Text(loc["zakat_total_due"], "SirajCaption", brush: on));
        stack.Children.Add(Kit.Text(Money(r.Due, currency), "SirajTitle", 40, brush: on));
        string note = r.Nisab <= 0 ? loc["tools_zakat_need_price"]
            : r.Total <= 0 ? loc["tools_zakat_need_amount"]
            : r.Eligible ? loc.Format("tools_zakat_rate_note", RatePercent())
            : loc["tools_zakat_not_due"];
        stack.Children.Add(Kit.Text(note, "SirajBody", bold: !r.Eligible && r.Total > 0, brush: on));
        if (r.Nisab > 0)
        {
            var bar = Kit.Bar(Math.Min(r.Net, r.Nisab), r.Nisab);
            bar.Margin = new Thickness(0, 4, 0, 4);
            stack.Children.Add(bar);
        }
        stack.Children.Add(Figure(loc["zakat_total_assets"], Money(r.Total, currency)));
        stack.Children.Add(Figure(loc["zakat_nisab_value"], Money(r.Nisab, currency)));
        stack.Children.Add(Figure(loc["zakat_net_wealth"], Money(r.Net, currency)));
        if (!r.Eligible && r.Total > 0 && r.Nisab > 0)
        {
            var below = Kit.Text(loc["zakat_below_nisab"], "SirajCaption", brush: on);
            below.LineHeight = 20;
            stack.Children.Add(below);
        }
        _result.Content = Kit.Accent(stack, new Thickness(24, 20, 24, 22));
    }

    private static string RatePercent()
    {
        var loc = Loc.Current;
        string sign = Core.Text.Digits.UseEastern(App.Settings.Current.NumeralSystem, loc.Language) ? "٪" : "%";
        return loc.Digitize((Zakat.Rate * 100).ToString("0.#", CultureInfo.InvariantCulture)) + sign;
    }

    private static Grid Figure(string label, string value)
    {
        var on = Kit.B("SirajOnPrimaryContainerBrush");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(Kit.Text(label, "SirajBody", brush: on));
        var v = Kit.Text(value, "SirajBody", bold: true, brush: on);
        Grid.SetColumn(v, 1);
        grid.Children.Add(v);
        return grid;
    }
}
