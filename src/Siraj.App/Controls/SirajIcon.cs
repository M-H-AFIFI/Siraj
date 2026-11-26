using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Siraj.App.Services;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Siraj.App.Controls;

internal sealed class SirajVectorIcon : UserControl
{
    public static readonly DependencyProperty IconKeyProperty = DependencyProperty.Register(
        nameof(IconKey), typeof(string), typeof(SirajVectorIcon), new PropertyMetadata("", (d, _) => ((SirajVectorIcon)d).Build()));

    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush), typeof(Brush), typeof(SirajVectorIcon), new PropertyMetadata(null, (d, _) => ((SirajVectorIcon)d).Build()));

    public string IconKey
    {
        get => (string)GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public Brush? Brush
    {
        get => (Brush?)GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    public SirajVectorIcon()
    {
        Width = 22;
        Height = 22;
    }

    public static SirajVectorIcon Make(string key, double size = 22, Brush? brush = null) => new()
    {
        IconKey = key, Width = size, Height = size, Brush = brush ?? Kit.B("SirajPrimaryBrush"),
    };

    private void Build()
    {
        if (!SirajIcons.All.TryGetValue(IconKey ?? "", out var data)) { Content = null; return; }
        var canvas = new Canvas { Width = data.Width, Height = data.Height };
        var brush = Brush ?? Kit.B("SirajOnSurfaceBrush");
        foreach (var part in data.Parts)
        {
            try
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
                    path.StrokeLineJoin = PenLineJoin.Round;
                }
                else
                {
                    path.Fill = brush;
                }
                canvas.Children.Add(path);
            }
            catch (Exception e) { Log.Error($"icon {IconKey}", e); }
        }
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
    }
}

internal static class PortraitAvatar
{
    private static readonly Dictionary<(string Path, int DecodeWidth), BitmapImage> Cache = new();
    private static readonly Dictionary<string, bool> ExistsCache = new(StringComparer.OrdinalIgnoreCase);

    public static Border Create(string? assetRelativePath, string fallbackName, double size = 48, double radius = -1, bool activeRing = false)
    {
        double r = radius < 0 ? size / 2 : radius;
        var border = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(r),
            Background = Kit.B("SirajPrimaryContainerBrush"),
            BorderBrush = Kit.B(activeRing ? "SirajPrimaryBrush" : "SirajGlassBorderBrush"),
            BorderThickness = new Thickness(activeRing ? 2.2 : 1),
        };
        if (!string.IsNullOrEmpty(assetRelativePath))
        {
            if (!ExistsCache.TryGetValue(assetRelativePath, out bool exists))
            {
                var full = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", assetRelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                exists = File.Exists(full);
                ExistsCache[assetRelativePath] = exists;
            }
            if (exists)
            {
                int decode = Math.Max(48, ((int)Math.Ceiling(size * 2) + 15) / 16 * 16);
                var key = (assetRelativePath, decode);
                if (!Cache.TryGetValue(key, out var bmp))
                {
                    bmp = new BitmapImage(new Uri($"ms-appx:///Assets/{assetRelativePath.Replace('\\', '/')}"))
                    {
                        DecodePixelWidth = decode,
                        DecodePixelType = DecodePixelType.Logical,
                    };
                    Cache[key] = bmp;
                }
                border.FlowDirection = FlowDirection.LeftToRight;
                border.Child = new Border
                {
                    CornerRadius = new CornerRadius(Math.Max(0, r - 1)),
                    Background = new ImageBrush
                    {
                        ImageSource = bmp,
                        Stretch = Stretch.UniformToFill,
                        AlignmentX = AlignmentX.Center,
                        AlignmentY = AlignmentY.Center,
                    },
                };
                return border;
            }
        }
        string initial = string.IsNullOrWhiteSpace(fallbackName) ? "س"
            : fallbackName.Trim().Replace("الشيخ ", "").Replace("Sheikh ", "").Trim()[0].ToString().ToUpperInvariant();
        border.Child = new TextBlock
        {
            Text = initial, FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
            FontSize = Math.Max(14, size * 0.38), FontWeight = FontWeights.SemiBold,
            Foreground = Kit.B("SirajOnPrimaryContainerBrush"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        return border;
    }
}
