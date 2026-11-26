using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Siraj.App.Services;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Siraj.App.Controls;

public sealed class SirajVectorIcon : UserControl
{
    public static readonly DependencyProperty IconKeyProperty = DependencyProperty.Register(
        nameof(IconKey), typeof(string), typeof(SirajVectorIcon),
        new PropertyMetadata("", (d, _) => ((SirajVectorIcon)d).UpdateIcon()));

    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush), typeof(Brush), typeof(SirajVectorIcon),
        new PropertyMetadata(null, (d, _) => ((SirajVectorIcon)d).UpdateBrush()));

    private readonly List<(Path Path, bool IsStroke)> _paths = [];
    private string _builtKey = "";

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
        IsHitTestVisible = false;
        IsTabStop = false;
        FlowDirection = FlowDirection.LeftToRight;
    }

    public static SirajVectorIcon Make(string key, double size = 22, Brush? brush = null) => new()
    {
        IconKey = key, Width = size, Height = size, Brush = brush ?? Kit.B("SirajPrimaryBrush"),
    };

    public static string OccasionIconKey(string occasionId) => occasionId switch
    {
        "islamic_new_year" => "occasion_islamic_new_year",
        "ashura" => "occasion_ashura",
        "isra_miraj" => "occasion_isra_miraj",
        "mid_shaban" => "occasion_mid_shaban",
        "ramadan" or "ramadan_start" => "occasion_ramadan",
        "last_ten_ramadan" => "occasion_laylat_qadr",
        "eid_al_fitr" => "occasion_eid_al_fitr",
        "day_of_arafah" => "occasion_day_of_arafah",
        "eid_al_adha" => "occasion_eid_al_adha",
        _ => "occasion_default",
    };

    public static PathIcon MakePathIcon(string key)
    {
        var icon = new PathIcon();
        if (SirajIcons.All.TryGetValue(key, out var data) && data.Parts.Length > 0)
        {
            try
            {
                icon.Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), data.Parts[0].Data);
            }
            catch (Exception e) { Log.Error($"pathicon {key}", e); }
        }
        return icon;
    }

    private void UpdateIcon()
    {
        string key = IconKey ?? "";
        if (key == _builtKey && _paths.Count > 0)
        {
            UpdateBrush();
            return;
        }
        _builtKey = key;
        _paths.Clear();
        if (!SirajIcons.All.TryGetValue(key, out var data))
        {
            Content = null;
            return;
        }
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
                bool isStroke = part.Stroke > 0;
                if (isStroke)
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
                _paths.Add((path, isStroke));
                canvas.Children.Add(path);
            }
            catch (Exception e) { Log.Error($"icon {key}", e); }
        }
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
    }

    private void UpdateBrush()
    {
        if (_paths.Count == 0)
        {
            if (!string.IsNullOrEmpty(IconKey)) UpdateIcon();
            return;
        }
        var brush = Brush ?? Kit.B("SirajOnSurfaceBrush");
        foreach (var (path, isStroke) in _paths)
        {
            if (isStroke) path.Stroke = brush;
            else path.Fill = brush;
        }
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
