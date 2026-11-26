using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Services;
using Siraj.Core.Theme;

namespace Siraj.App.Controls;

internal static class Ui
{
    private static ResourceDictionary R => Application.Current.Resources;

    public static Brush Brush(string key) => (Brush)R[key];

    public static TextBlock Text(string text, string style = "SirajBody") =>
        new() { Text = text, Style = (Style)R[style] };

    public static TextBlock Section(string title) => Text(title, "SirajSectionHeader");

    public static StackPanel SectionWithIcon(string title, string? iconKey = null, string? glyph = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(4, 22, 4, 8), VerticalAlignment = VerticalAlignment.Center };
        if (iconKey != null || glyph != null)
        {
            UIElement icon = iconKey != null
                ? SirajVectorIcon.Make(iconKey, 15, Brush("SirajPrimaryBrush"))
                : Kit.Icon(glyph!, 14, Brush("SirajPrimaryBrush"));
            row.Children.Add(new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(10),
                Background = Brush("SirajPrimarySoftBrush"),
                BorderBrush = Brush("SirajPrimaryBorderBrush"), BorderThickness = new Thickness(1),
                Child = icon,
            });
        }
        var label = Text(title, "SirajSectionHeader");
        label.Margin = new Thickness(0);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(label);
        return row;
    }

    public static Border Card(params UIElement[] children)
    {
        var stack = new StackPanel();
        for (int i = 0; i < children.Length; i++)
        {
            if (i > 0) stack.Children.Add(Hairline());
            stack.Children.Add(children[i]);
        }
        return new Border { Style = (Style)R["SirajCard"], Padding = new Thickness(0), Child = stack };
    }

    public static Border Hairline() => new()
    {
        Height = 1,
        Margin = new Thickness(20, 0, 20, 0),
        Background = Brush("SirajDividerBrush"),
    };

    public static Grid Row(string title, string? description, FrameworkElement? control, bool stack = false)
    {
        var grid = new Grid { Padding = new Thickness(20, 14, 20, 14), ColumnSpacing = 16, RowSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var texts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(Text(title));
        if (!string.IsNullOrEmpty(description)) texts.Children.Add(Text(description, "SirajCaption"));
        grid.Children.Add(texts);
        if (control != null)
        {
            if (stack)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(control, 1);
                Grid.SetColumnSpan(control, 2);
            }
            else
            {
                Grid.SetColumn(control, 1);
                control.VerticalAlignment = VerticalAlignment.Center;
            }
            AutomationProperties.SetName(control, title);
            grid.Children.Add(control);
        }
        return grid;
    }

    public static ToggleSwitch Toggle(bool on, Action<bool> changed)
    {
        var toggle = new ToggleSwitch { IsOn = on, OnContent = "", OffContent = "", MinWidth = 0 };
        toggle.Toggled += (_, _) => changed(toggle.IsOn);
        return toggle;
    }

    public static ComboBox Choice<T>(IList<(T Value, string Label)> options, T selected, Action<T> changed, double width = 240)
    {
        var box = new ComboBox { MinWidth = width, MaxWidth = 360 };
        foreach (var (_, label) in options) box.Items.Add(label);
        box.SelectedIndex = Math.Max(0, options.ToList().FindIndex(o => EqualityComparer<T>.Default.Equals(o.Value, selected)));
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex >= 0) changed(options[box.SelectedIndex].Value);
        };
        return box;
    }

    public static FrameworkElement Segments<T>(IList<(T Value, string Label)> options, T selected, Action<T> changed) =>
        new SlidingPills<T>(options, selected, changed);

    public static Grid Swatches(string selected, Action<string> changed)
    {
        var loc = Loc.Current;
        var items = new List<UIElement>(Palettes.Names.Count);
        foreach (var name in Palettes.Names)
        {
            var lightSwatch = Palettes.Swatch(name);
            var darkSwatch = Palettes.Scheme(name, true, false).Primary;
            bool chosen = Palettes.Resolve(selected) == name;
            var swatchFill = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = ThemeService.ToColor(lightSwatch), Offset = 0 },
                    new GradientStop { Color = ThemeService.ToColor(darkSwatch), Offset = 1 },
                },
            };
            var dotInner = new Grid();
            if (chosen)
            {
                dotInner.Children.Add(new FontIcon
                {
                    Glyph = "\uE73E", FontFamily = (FontFamily)R["SirajIcons"], FontSize = 13,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                });
            }
            var dot = new Border
            {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(16),
                Background = swatchFill,
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(chosen ? (byte)220 : (byte)80, 255, 255, 255)),
                BorderThickness = new Thickness(chosen ? 2 : 1),
                Child = dotInner,
            };
            var label = Text(loc[PaletteKey(name)], "SirajBody");
            label.FontSize = 13;
            label.FontWeight = chosen ? FontWeights.Bold : FontWeights.Medium;
            label.Foreground = Brush(chosen ? "SirajOnSurfaceBrush" : "SirajOnSurfaceVariantBrush");
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.TextWrapping = TextWrapping.NoWrap;
            label.VerticalAlignment = VerticalAlignment.Center;

            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(dot);
            Grid.SetColumn(label, 1);
            row.Children.Add(label);

            var button = Kit.CardButton(row, () => changed(name), loc[PaletteKey(name)],
                background: chosen ? "SirajPrimarySoftBrush" : "SirajGlassPillBrush",
                padding: new Thickness(11, 10, 11, 10));
            button.CornerRadius = new CornerRadius(16);
            button.Height = 54;
            if (chosen)
            {
                var p = Kit.Clone("SirajPrimaryBrush");
                button.BorderBrush = p;
                button.BorderThickness = new Thickness(1.8);
                button.Resources["ButtonBorderBrush"] = Kit.Clone(p);
                button.Resources["ButtonBorderBrushPointerOver"] = Kit.Clone(p);
            }
            items.Add(button);
        }
        return Kit.Columns(200, 10, items);
    }

    public static string PaletteKey(string name) => name switch
    {
        "Emerald" => "palette_emerald",
        "Lime" => "palette_lime",
        "Olive" => "palette_olive",
        "TealCaspian" => "palette_teal_caspian",
        "OceanBlue" => "palette_ocean_blue",
        "RoyalIndigo" => "palette_royal_indigo",
        "Slate" => "palette_slate",
        "Rose" => "palette_rose",
        "Burgundy" => "palette_burgundy",
        "Coffee" => "palette_coffee",
        "Sand" => "palette_sand",
        _ => "palette_gold"
    };

    public static Button Button(string text, Action click, bool primary = false)
    {
        var button = new Button { Content = text, Style = (Style)R[primary ? "SirajPrimaryButton" : "SirajButton"] };
        button.Click += (_, _) => click();
        return button;
    }
}
