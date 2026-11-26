using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using Siraj.Core.Prayer;

namespace Siraj.App.Controls;

internal sealed class PrayerGlyph : UserControl
{
    public static readonly DependencyProperty PrayerProperty = DependencyProperty.Register(
        nameof(Prayer), typeof(PrayerType), typeof(PrayerGlyph), new PropertyMetadata(PrayerType.Fajr, (d, _) => ((PrayerGlyph)d).Build()));

    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush), typeof(Brush), typeof(PrayerGlyph), new PropertyMetadata(null, (d, _) => ((PrayerGlyph)d).Build()));

    public PrayerType Prayer
    {
        get => (PrayerType)GetValue(PrayerProperty);
        set => SetValue(PrayerProperty, value);
    }

    public Brush? Brush
    {
        get => (Brush?)GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    public PrayerGlyph()
    {
        Width = 22;
        Height = 22;
        Build();
    }

    private void Build()
    {
        var canvas = new Canvas { Width = 24, Height = 24 };
        var brush = Brush ?? (Brush)Application.Current.Resources["SirajOnSurfaceBrush"];
        foreach (var part in PrayerIcons.ByPrayer[(int)Prayer])
        {
            var path = new Path
            {
                Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), part.Data),
                Opacity = part.Opacity,
            };
            if (part.Stroke > 0)
            {
                path.Stroke = brush;
                path.StrokeThickness = part.Stroke;
                path.StrokeStartLineCap = PenLineCap.Round;
                path.StrokeEndLineCap = PenLineCap.Round;
            }
            else
            {
                path.Fill = brush;
            }
            canvas.Children.Add(path);
        }
        Content = new Viewbox { Child = canvas };
    }
}
