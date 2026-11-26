using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Devotions;
using Windows.ApplicationModel.DataTransfer;

namespace Siraj.App.Views;

public sealed partial class HadithPage : Page
{
    private readonly MasterDetail _view;
    private readonly StackPanel _list = new() { Spacing = 6 };
    private string _query = "";
    private string? _category;
    private int _selected;
    private readonly Dictionary<int, Button> _buttons = [];

    public HadithPage()
    {
        InitializeComponent();
        var loc = Loc.Current;
        var header = new StackPanel { Spacing = 12 };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 0, 4, 0) };
        titleRow.Children.Add(Kit.Back(() => App.MainWindowInstance?.Navigate("worship")));
        var titles = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(Ui.Text(loc["hub_hadith_title"], "SirajTitle"));
        titles.Children.Add(Ui.Text(loc["hub_hadith_desc"], "SirajCaption"));
        titleRow.Children.Add(titles);
        header.Children.Add(titleRow);
        var tools = new Grid { ColumnSpacing = 10 };
        tools.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tools.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tools.Children.Add(Kit.Search(loc["hadith_search_hint"], q => { _query = q; Fill(); }));
        var categories = new List<(string?, string)> { (null, loc.Format("hadith_filter_all", loc.Number(Hadiths.All.Count))) };
        categories.AddRange(Hadiths.Categories.Select(c => ((string?)c.En, Hadiths.Category(c.En, c.Ar, loc.Language))));
        var picker = Ui.Choice(categories, null, c => { _category = c; Fill(); }, 200);
        Grid.SetColumn(picker, 1);
        tools.Children.Add(picker);
        header.Children.Add(tools);

        _view = new MasterDetail(header) { ListWidth = 400 };
        _view.List = _list;
        Kit.Entrance(_list);
        Host.Children.Add(_view);
        var today = Hadiths.ForDay(App.Clock.Place?.Today(App.Clock.Now) ?? DateOnly.FromDateTime(App.Clock.Now.LocalDateTime));
        _selected = today.Id;
        Fill();
        _view.Show(Detail(today, isToday: true), open: false);
    }

    private void Fill()
    {
        var loc = Loc.Current;
        _list.Children.Clear();
        var items = Hadiths.All.Where(h => (_category == null || h.CategoryEn == _category) && h.Matches(_query)).ToList();
        if (items.Count == 0)
        {
            var empty = new StackPanel { Spacing = 4, Margin = new Thickness(4, 12, 4, 12) };
            empty.Children.Add(Kit.Text(loc["tools_hadith_empty_title"], "SirajBody", bold: true));
            empty.Children.Add(Kit.Text(loc["tools_hadith_empty_body"], "SirajCaption"));
            _list.Children.Add(empty);
            return;
        }
        _buttons.Clear();
        foreach (var h in items) _list.Children.Add(Row(h));
    }

    private Button Row(Hadith h)
    {
        var loc = Loc.Current;
        var stack = new StackPanel { Spacing = 3 };
        var title = Kit.Text(h.Title(loc.Language), "SirajBody", bold: true);
        title.MaxLines = 2;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        stack.Children.Add(title);
        stack.Children.Add(Kit.Text(h.Category(loc.Language) + loc.Sep + h.Reference(loc.Language).Split(" - ")[0], "SirajCaption"));
        bool chosen = h.Id == _selected;
        var button = Kit.CardButton(stack, () =>
        {
            Choose(h.Id);
            _view.Show(Detail(h, isToday: false));
        }, h.Title(loc.Language), padding: new Thickness(16, 12, 16, 12));
        _buttons[h.Id] = button;
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

    private UIElement Detail(Hadith h, bool isToday)
    {
        var loc = Loc.Current;
        var lang = loc.Language;
        var stack = new StackPanel { Spacing = 14 };
        var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (isToday) badges.Children.Add(Kit.Pill(loc["hadith_of_the_day"], "SirajPrimaryBrush", "SirajOnPrimaryBrush"));
        badges.Children.Add(Kit.Pill(h.Category(lang)));
        stack.Children.Add(badges);
        stack.Children.Add(Kit.Text(h.Title(lang), "SirajTitle", 24));
        stack.Children.Add(Kit.Text(h.Narrator(lang), "SirajCaption"));

        var matn = Kit.Arabic(h.MatnAr, 25);
        matn.IsTextSelectionEnabled = true;
        stack.Children.Add(new Border
        {
            Background = Kit.B("SirajSurfaceContainerLowBrush"), CornerRadius = new CornerRadius(16), Padding = new Thickness(20, 14, 20, 16),
            Child = matn,
        });
        if (h.Translation(lang) is { Length: > 0 } translation)
        {
            var t = Kit.Text(translation, "SirajBody", 16);
            t.IsTextSelectionEnabled = true;
            t.LineHeight = 26;
            stack.Children.Add(t);
            if (!h.HasOwnTranslation(lang)) stack.Children.Add(Kit.Text(loc["translation_pending_notice"], "SirajCaption"));
        }
        var source = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        source.Children.Add(Kit.Icon("menu_book", 16, Kit.B("SirajOnSurfaceVariantBrush")));
        source.Children.Add(Kit.Text(h.Reference(lang), "SirajCaption"));
        stack.Children.Add(source);

        if (h.Explanation(lang) is { Length: > 0 } explanation)
        {
            var box = new StackPanel { Spacing = 6 };
            box.Children.Add(Kit.Text(loc["hadith_explanation_heading"], "SirajHeading", brush: Kit.B("SirajOnPrimaryContainerBrush")));
            box.Children.Add(Kit.Text(explanation, "SirajBody", brush: Kit.B("SirajOnPrimaryContainerBrush")));
            stack.Children.Add(Kit.Accent(box, new Thickness(20, 16, 20, 18)));
        }

        var copied = Kit.Text(loc["hadith_copied"], "SirajCaption", brush: Kit.B("SirajPrimaryBrush"));
        copied.Visibility = Visibility.Collapsed;
        copied.VerticalAlignment = VerticalAlignment.Center;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        actions.Children.Add(Ui.Button(loc["quran_action_copy"], () =>
        {
            var text = string.Join("\n\n", new[] { h.Title(lang), h.MatnAr, h.Translation(lang), h.Narrator(lang), h.Reference(lang), loc["tools_hadith_shared_via"] }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            copied.Visibility = Visibility.Visible;
        }));
        actions.Children.Add(copied);
        stack.Children.Add(actions);
        Kit.Entrance(stack);
        return Kit.Card(stack, new Thickness(28, 24, 28, 24));
    }
}
