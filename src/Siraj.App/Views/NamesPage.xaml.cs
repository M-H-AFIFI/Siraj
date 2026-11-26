using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Devotions;

namespace Siraj.App.Views;

public sealed partial class NamesPage : Page
{
    private readonly MasterDetail _view;
    private readonly ContentControl _grid = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private string _query = "";
    private int _selected;
    private readonly Dictionary<int, Button> _buttons = [];

    public NamesPage()
    {
        InitializeComponent();
        var loc = Loc.Current;
        var header = new StackPanel { Spacing = 12 };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 0, 4, 0) };
        titleRow.Children.Add(Kit.Back(() => App.MainWindowInstance?.Navigate("worship")));
        var titles = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(Ui.Text(loc["names_of_allah_title"], "SirajTitle"));
        titles.Children.Add(Ui.Text(loc["hub_names_desc"], "SirajCaption"));
        titleRow.Children.Add(titles);
        header.Children.Add(titleRow);
        header.Children.Add(Kit.Search(loc["names_of_allah_search_hint"], q => { _query = q; Fill(); }));

        _view = new MasterDetail(header) { ListWidth = 460 };
        _view.List = _grid;
        Host.Children.Add(_view);
        var today = AllahNames.ForDay(App.Clock.Place?.Today(App.Clock.Now) ?? DateOnly.FromDateTime(App.Clock.Now.LocalDateTime));
        _selected = today.Number;
        Fill();
        _view.Show(Detail(today, isToday: true), open: false);
    }

    private void Fill()
    {
        var loc = Loc.Current;
        var names = AllahNames.All.Where(n => n.Matches(_query)).ToList();
        if (names.Count == 0)
        {
            var empty = new StackPanel { Spacing = 4, Margin = new Thickness(4, 12, 4, 12) };
            empty.Children.Add(Kit.Text(loc["tools_names_empty_title"], "SirajBody", bold: true));
            empty.Children.Add(Kit.Text(loc["tools_names_empty_body"], "SirajCaption"));
            _grid.Content = empty;
            return;
        }
        _buttons.Clear();
        _grid.Content = Kit.Columns(136, 8, names.Select(n => (UIElement)Tile(n)).ToList());
    }

    private Button Tile(AllahName name)
    {
        var loc = Loc.Current;
        var stack = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Stretch };
        var number = Kit.Text(loc.Number(name.Number), "SirajCaption", 11);
        number.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(number);
        var ar = Kit.Arabic(name.NameAr, 24, Kit.B("SirajPrimaryBrush"), TextAlignment.Center);
        ar.HorizontalAlignment = HorizontalAlignment.Center;
        ar.LineHeight = 44;
        stack.Children.Add(ar);
        // The Arabic "meaning" is the name again without its marks: in Arabic the name says it all.
        if (loc.Language != "ar")
        {
            var tr = Kit.Text(name.Transliteration, "SirajCaption", 12);
            tr.HorizontalAlignment = HorizontalAlignment.Center;
            tr.HorizontalTextAlignment = TextAlignment.Center;
            tr.MaxLines = 1;
            tr.TextTrimming = TextTrimming.CharacterEllipsis;
            stack.Children.Add(tr);
        }
        bool chosen = name.Number == _selected;
        var button = Kit.CardButton(stack, () =>
        {
            Choose(name.Number);
            _view.Show(Detail(name, isToday: false));
        }, name.Transliteration, padding: new Thickness(8, 10, 8, 10));
        _buttons[name.Number] = button;
        Mark(button, chosen);
        return button;
    }

    private void Choose(int id)
    {
        if (_buttons.TryGetValue(_selected, out var old)) Mark(old, false);
        _selected = id;
        if (_buttons.TryGetValue(id, out var now)) Mark(now, true);
    }

    private static void Mark(Button button, bool chosen)
    {
        var bg = Kit.Clone(chosen ? "SirajSecondaryContainerBrush" : "SirajGlassCardBrush");
        button.Background = bg;
        button.Resources["ButtonBackground"] = Kit.Clone(bg);
        button.BorderBrush = Kit.Clone(chosen ? "SirajPrimaryBrush" : "SirajGlassBorderBrush");
    }

    private static UIElement Detail(AllahName name, bool isToday)
    {
        var loc = Loc.Current;
        var lang = loc.Language;
        var stack = new StackPanel { Spacing = 12 };

        var hero = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        if (isToday) badges.Children.Add(Kit.Pill(loc["names_day_badge"], "SirajPrimaryBrush", "SirajOnPrimaryBrush"));
        badges.Children.Add(Kit.Pill(loc.Number(name.Number), "SirajSurfaceBrush", "SirajOnSurfaceVariantBrush"));
        hero.Children.Add(badges);
        var ar = Kit.Arabic(name.NameAr, 64, Kit.B("SirajOnPrimaryContainerBrush"), TextAlignment.Center);
        ar.HorizontalAlignment = HorizontalAlignment.Center;
        ar.LineHeight = 112;
        hero.Children.Add(ar);
        if (lang != "ar")
        {
            var tr = Kit.Text(name.Transliteration, "SirajTitle", 24, brush: Kit.B("SirajOnPrimaryContainerBrush"));
            tr.HorizontalAlignment = HorizontalAlignment.Center;
            hero.Children.Add(tr);
        }
        if (lang != "ar")
        {
            var meaning = Kit.Text(name.Meaning(lang), "SirajHeading", 18, brush: Kit.B("SirajOnPrimaryContainerBrush"));
            meaning.HorizontalAlignment = HorizontalAlignment.Center;
            meaning.HorizontalTextAlignment = TextAlignment.Center;
            hero.Children.Add(meaning);
        }
        stack.Children.Add(Kit.Accent(hero, new Thickness(24, 20, 24, 26)));

        var about = Kit.Text(name.Description(lang), "SirajBody", 16);
        about.LineHeight = 26;
        about.IsTextSelectionEnabled = true;
        stack.Children.Add(Kit.Card(about, new Thickness(24, 20, 24, 22)));

        var quote = new StackPanel { Spacing = 6 };
        var text = Kit.Text(loc["names_hadith_text"], "SirajBody");
        // Arabic has no italic: a slanted copy of the letters only looks broken.
        if (!loc.IsRtl) text.FontStyle = Windows.UI.Text.FontStyle.Italic;
        quote.Children.Add(text);
        quote.Children.Add(Kit.Text(loc["names_hadith_source"], "SirajCaption"));
        stack.Children.Add(new Border
        {
            BorderBrush = Kit.B("SirajGoldBrush"), BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(4, 4, 4, 12), Child = quote,
        });
        Kit.Entrance(stack);
        return stack;
    }
}
