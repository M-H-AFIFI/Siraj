using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Siraj.App.Native;
using Siraj.App.Services;
using Siraj.App.Theme;
using Windows.UI;

namespace Siraj.App.Controls;

internal enum PageTransitionKind { None, Fade, TabDown, TabUp, DrillIn, Back }

internal static class Kit
{
    private static ResourceDictionary R => Application.Current.Resources;

    public static Brush B(string key) => (Brush)R[key];

    public static Brush Clone(Brush brush)
    {
        if (brush is SirajGlassBrush g) return g.Clone();
        if (brush is SolidColorBrush s) return new SolidColorBrush(s.Color) { Opacity = s.Opacity };
        if (brush is LinearGradientBrush lg)
        {
            var copy = new LinearGradientBrush { StartPoint = lg.StartPoint, EndPoint = lg.EndPoint, Opacity = lg.Opacity };
            foreach (var stop in lg.GradientStops) copy.GradientStops.Add(new GradientStop { Color = stop.Color, Offset = stop.Offset });
            return copy;
        }
        return brush;
    }

    public static Brush Clone(string key) => Clone(B(key));

    public static bool Motion => !App.Settings.Current.ReduceMotion && Win32.AnimationsEnabled();

    public static (ScrollViewer Root, StackPanel Body) Page(string title, string? subtitle, double maxWidth = 1100, Action? back = null)
    {
        var body = new StackPanel { Spacing = 16 };
        var column = new StackPanel { MaxWidth = maxWidth, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(28, 12, 28, 96), Spacing = 16 };
        var head = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        head.Children.Add(Ui.Text(title, "SirajTitle"));
        if (!string.IsNullOrEmpty(subtitle)) head.Children.Add(Ui.Text(subtitle, "SirajCaption"));
        if (back != null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 0, 4, 4) };
            row.Children.Add(Back(back));
            row.Children.Add(head);
            column.Children.Add(row);
        }
        else
        {
            head.Margin = new Thickness(4, 0, 4, 4);
            column.Children.Add(head);
        }
        column.Children.Add(body);
        var wrapper = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        wrapper.Children.Add(column);
        return (new ScrollViewer
        {
            Content = wrapper,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
        }, body);
    }

    public static void PopIn(UIElement element, int order = 0)
    {
        if (!Motion) return;
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var c = visual.Compositor;
        var ease = c.CreateCubicBezierEasingFunction(new Vector2(0.05f, 0.7f), new Vector2(0.1f, 1f));
        var delay = TimeSpan.FromMilliseconds(Math.Min(order, 2) * 14);
        var dur = TimeSpan.FromMilliseconds(120);

        var move = c.CreateVector3KeyFrameAnimation();
        move.InsertKeyFrame(0, new Vector3(0, 6, 0));
        move.InsertKeyFrame(1, Vector3.Zero, ease);
        move.Duration = dur;
        move.DelayTime = delay;
        move.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1, ease);
        fade.Duration = TimeSpan.FromMilliseconds(95);
        fade.DelayTime = delay;
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Translation", move);
        visual.StartAnimation("Opacity", fade);
    }

    public static void GlideTo(UIElement element, Vector3 target, int ms = 160)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        if (!Motion || ms <= 0)
        {
            visual.StopAnimation("Translation");
            visual.Properties.InsertVector3("Translation", target);
            return;
        }
        var c = visual.Compositor;
        var ease = c.CreateCubicBezierEasingFunction(new Vector2(0.05f, 0.7f), new Vector2(0.1f, 1f));
        var anim = c.CreateVector3KeyFrameAnimation();
        anim.InsertKeyFrame(1f, target, ease);
        anim.Duration = TimeSpan.FromMilliseconds(ms);
        visual.StartAnimation("Translation", anim);
    }

    public static void FadeTo(UIElement element, float opacity, int ms = 90)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        if (!Motion || ms <= 0)
        {
            visual.StopAnimation("Opacity");
            visual.Opacity = opacity;
            return;
        }
        var c = visual.Compositor;
        var anim = c.CreateScalarKeyFrameAnimation();
        anim.InsertKeyFrame(1f, opacity, c.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
        anim.Duration = TimeSpan.FromMilliseconds(ms);
        visual.StartAnimation("Opacity", anim);
    }

    public static void AnimatePageIn(UIElement? page, PageTransitionKind kind, bool isRtl)
    {
        if (page == null || kind == PageTransitionKind.None) return;
        float dir = isRtl ? -1f : 1f;
        switch (kind)
        {
            case PageTransitionKind.TabDown:
                AnimateIn(page, 0, 7, 1f, 120);
                break;
            case PageTransitionKind.TabUp:
                AnimateIn(page, 0, -7, 1f, 120);
                break;
            case PageTransitionKind.DrillIn:
                AnimateIn(page, 14 * dir, 0, 0.994f, 135);
                break;
            case PageTransitionKind.Back:
                AnimateIn(page, -12 * dir, 0, 1f, 120);
                break;
            default:
                AnimateIn(page, 0, 5, 1f, 110);
                break;
        }
    }

    public static void AnimateIn(UIElement element, float fromDx = 0, float fromDy = 8, float fromScale = 1f, int ms = 125, int delayMs = 0)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        if (!Motion || ms <= 0)
        {
            visual.StopAnimation("Translation");
            visual.StopAnimation("Scale");
            visual.StopAnimation("Opacity");
            visual.Properties.InsertVector3("Translation", Vector3.Zero);
            visual.Scale = Vector3.One;
            visual.Opacity = 1f;
            return;
        }

        if (element is FrameworkElement fe && MathF.Abs(fromScale - 1f) > 0.001f)
        {
            if (fe.ActualWidth > 0 && fe.ActualHeight > 0)
                visual.CenterPoint = new Vector3((float)fe.ActualWidth / 2f, (float)fe.ActualHeight / 2f, 0);
            else
                fe.SizeChanged += SetCenterOnce;
        }

        var c = visual.Compositor;
        var ease = c.CreateCubicBezierEasingFunction(new Vector2(0.05f, 0.7f), new Vector2(0.1f, 1f));
        var delay = TimeSpan.FromMilliseconds(delayMs);

        var move = c.CreateVector3KeyFrameAnimation();
        move.InsertKeyFrame(0f, new Vector3(fromDx, fromDy, 0));
        move.InsertKeyFrame(1f, Vector3.Zero, ease);
        move.Duration = TimeSpan.FromMilliseconds(ms);
        move.DelayTime = delay;
        move.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0f);
        fade.InsertKeyFrame(1f, 1f, ease);
        fade.Duration = TimeSpan.FromMilliseconds(Math.Max(70, (int)(ms * 0.75)));
        fade.DelayTime = delay;
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Translation", move);
        visual.StartAnimation("Opacity", fade);

        if (MathF.Abs(fromScale - 1f) > 0.001f)
        {
            var scale = c.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0f, new Vector3(fromScale, fromScale, 1f));
            scale.InsertKeyFrame(1f, Vector3.One, ease);
            scale.Duration = TimeSpan.FromMilliseconds(ms);
            scale.DelayTime = delay;
            scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            visual.StartAnimation("Scale", scale);
        }

        static void SetCenterOnce(object s, SizeChangedEventArgs e)
        {
            if (s is FrameworkElement f)
            {
                f.SizeChanged -= SetCenterOnce;
                ElementCompositionPreview.GetElementVisual(f).CenterPoint = new Vector3((float)e.NewSize.Width / 2f, (float)e.NewSize.Height / 2f, 0);
            }
        }
    }

    public static void AnimateOut(UIElement element, float toDx, float toDy, float toScale, int ms, Action onDone)
    {
        if (!Motion || ms <= 0)
        {
            onDone();
            return;
        }
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        if (element is FrameworkElement fe && fe.ActualWidth > 0 && fe.ActualHeight > 0)
            visual.CenterPoint = new Vector3((float)fe.ActualWidth / 2f, (float)fe.ActualHeight / 2f, 0);

        var c = visual.Compositor;
        var ease = c.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0f), new Vector2(1f, 1f));
        var batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);

        var move = c.CreateVector3KeyFrameAnimation();
        move.InsertKeyFrame(1f, new Vector3(toDx, toDy, 0), ease);
        move.Duration = TimeSpan.FromMilliseconds(ms);

        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1f, 0f, ease);
        fade.Duration = TimeSpan.FromMilliseconds(ms);

        visual.StartAnimation("Translation", move);
        visual.StartAnimation("Opacity", fade);

        if (MathF.Abs(toScale - 1f) > 0.001f)
        {
            var scale = c.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(1f, new Vector3(toScale, toScale, 1f), ease);
            scale.Duration = TimeSpan.FromMilliseconds(ms);
            visual.StartAnimation("Scale", scale);
        }

        batch.End();
        batch.Completed += (_, _) =>
        {
            onDone();
            batch.Dispose();
        };
    }

    public static Border IconBadge(string iconKey, double size = 40, double iconSize = 20, string bgKey = "SirajPrimarySoftBrush", string fgKey = "SirajPrimaryBrush", double radius = 13) => new()
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(radius),
        Background = B(bgKey),
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
        Child = SirajVectorIcon.Make(iconKey, iconSize, B(fgKey)),
    };

    public static Border OccasionBadge(string occasionId, double size = 44, bool highlighted = false)
    {
        var scheme = App.Theme.Scheme;
        var gold = Core.Sky.Argb.Hex(0xFFE5B558);
        Brush bg = highlighted
            ? B("SirajPrimaryBrush")
            : new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = ThemeService.ToColor(scheme.Primary, 0.16f), Offset = 0 },
                    new GradientStop { Color = ThemeService.ToColor(gold, 0.14f), Offset = 1 },
                },
            };
        Brush border = highlighted
            ? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            : new SolidColorBrush(ThemeService.ToColor(gold, 0.32f));
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.32),
            Background = bg,
            BorderBrush = border,
            BorderThickness = new Thickness(highlighted ? 0 : 1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = SirajVectorIcon.Make(
                SirajVectorIcon.OccasionIconKey(occasionId),
                Math.Round(size * 0.50),
                B(highlighted ? "SirajOnPrimaryBrush" : "SirajPrimaryBrush")),
        };
    }

    public static TextBlock Text(string text, string style = "SirajBody", double? size = null, bool bold = false, Brush? brush = null)
    {
        var t = Ui.Text(text, style);
        if (size is { } s) t.FontSize = s;
        if (bold) t.FontWeight = FontWeights.SemiBold;
        if (brush != null) t.Foreground = brush;
        return t;
    }

    public static TextBlock Arabic(string text, double size = 24, Brush? brush = null, TextAlignment align = TextAlignment.Right) => new()
    {
        Text = text,
        FontFamily = (FontFamily)R["SirajArabicFont"],
        FontSize = size,
        LineHeight = size * 1.9,
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        TextWrapping = TextWrapping.Wrap,
        FlowDirection = FlowDirection.RightToLeft,
        TextAlignment = align == TextAlignment.Right ? TextAlignment.Left : align == TextAlignment.Left ? TextAlignment.Right : align,
        Foreground = brush ?? B("SirajOnSurfaceBrush"),
        IsTextSelectionEnabled = false,
    };

    public static SirajVectorIcon Icon(string iconKey, double size = 18, Brush? brush = null) =>
        SirajVectorIcon.Make(iconKey, size, brush ?? B("SirajPrimaryBrush"));

    public static Border Card(UIElement child, Thickness? padding = null, string background = "SirajGlassCardBrush") => new()
    {
        Style = (Style)R["SirajCard"], Child = child, Background = B(background), BorderBrush = B("SirajGlassBorderBrush"),
        Padding = padding ?? new Thickness(20),
    };

    public static Border Accent(UIElement child, Thickness? padding = null)
    {
        var scheme = App.Theme.Scheme;
        bool dark = scheme.IsDark;
        bool glass = App.Theme.IsGlass;
        var c0 = Core.Sky.Argb.Lerp(scheme.PrimaryContainer, scheme.Primary, dark ? 0.14f : 0.08f);
        var c1 = Core.Sky.Argb.Lerp(scheme.PrimaryContainer, scheme.Surface, dark ? 0.34f : 0.28f);
        var bg = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = ThemeService.ToColor(c0, glass ? (dark ? 0.84f : 0.86f) : 1f), Offset = 0 },
                new GradientStop { Color = ThemeService.ToColor(c1, glass ? (dark ? 0.72f : 0.76f) : 1f), Offset = 1 },
            },
        };
        var border = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = ThemeService.ToColor(Core.Sky.Argb.Hex(0xFFFFFFFF), dark ? 0.32f : 0.82f), Offset = 0 },
                new GradientStop { Color = ThemeService.ToColor(scheme.Primary, dark ? 0.38f : 0.24f), Offset = 1 },
            },
        };
        return new Border
        {
            Style = (Style)R["SirajCard"], Child = child, Background = bg,
            BorderBrush = border, BorderThickness = new Thickness(1),
            Padding = padding ?? new Thickness(24),
        };
    }

    public static Button CardButton(UIElement content, Action click, string? name = null, string background = "SirajGlassCardBrush",
        Thickness? padding = null)
    {
        var bg = Clone(background);
        var border = Clone("SirajGlassBorderBrush");
        var button = new Button
        {
            Content = content, Background = bg, BorderBrush = border, BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)R["SirajCardRadius"], Padding = padding ?? new Thickness(18, 16, 18, 16),
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
            FocusVisualPrimaryBrush = Clone("SirajPrimaryBrush"),
            FocusVisualSecondaryBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        };
        button.Resources["ButtonBackground"] = Clone(bg);
        button.Resources["ButtonBackgroundPointerOver"] = Clone("SirajGlassCardHoverBrush");
        button.Resources["ButtonBackgroundPressed"] = Clone("SirajPrimarySoftBrush");
        button.Resources["ButtonBorderBrush"] = Clone(border);
        button.Resources["ButtonBorderBrushPointerOver"] = Clone("SirajPrimaryBorderBrush");
        button.Resources["ButtonBorderBrushPressed"] = Clone("SirajPrimaryBrush");
        button.Resources["ButtonForegroundPointerOver"] = Clone("SirajOnSurfaceBrush");
        button.Resources["ButtonForegroundPressed"] = Clone("SirajOnSurfaceBrush");
        if (name != null) AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        Lift(button);
        return button;
    }

    public static void Lift(UIElement card)
    {
        if (!Motion) return;
        card.TranslationTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(100) };
        card.ScaleTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(65) };
        if (card is FrameworkElement fe)
            fe.SizeChanged += (_, e) => card.CenterPoint = new Vector3((float)e.NewSize.Width / 2, (float)e.NewSize.Height / 2, 0);
        card.PointerEntered += (_, _) => card.Translation = new Vector3(0, -2f, 0);
        card.PointerExited += (_, _) =>
        {
            card.Translation = Vector3.Zero;
            card.Scale = Vector3.One;
        };
        card.PointerCanceled += (_, _) =>
        {
            card.Translation = Vector3.Zero;
            card.Scale = Vector3.One;
        };
        card.PointerCaptureLost += (_, _) => card.Scale = Vector3.One;
        // Subscribe with handledEventsToo because Button marks pointer press and release handled.
        card.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => card.Scale = new Vector3(0.985f, 0.985f, 1)), true);
        card.AddHandler(UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => card.Scale = Vector3.One), true);
    }

    public static Border Pill(string text, string background = "SirajSecondaryContainerBrush", string foreground = "SirajOnSecondaryContainerBrush") => new()
    {
        Background = B(background), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 3, 10, 4),
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, FontFamily = (FontFamily)R["SirajFont"],
            Foreground = B(foreground),
        },
    };

    public static ProgressBar Bar(double value, double max, string? brush = null)
    {
        var bar = new ProgressBar
        {
            Minimum = 0, Maximum = Math.Max(1, max), Value = value, Height = 6, MinHeight = 6, CornerRadius = new CornerRadius(3),
            Background = B("SirajSurfaceContainerHighBrush"),
        };
        if (brush != null) bar.Foreground = B(brush);
        return bar;
    }

    public static TextBox Search(string placeholder, Action<string> changed)
    {
        var box = new TextBox
        {
            PlaceholderText = placeholder, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 9, 14, 9),
            FontFamily = (FontFamily)R["SirajFont"], FontSize = 14,
        };
        AutomationProperties.SetName(box, placeholder);
        box.TextChanged += (_, _) => changed(box.Text);
        return box;
    }

    public static Grid Columns(double minWidth, double spacing, IList<UIElement> items)
    {
        var grid = new Grid { ColumnSpacing = spacing, RowSpacing = spacing };
        int lastColumns = -1;
        void Layout(double width)
        {
            int columns = Math.Max(1, (int)((width + spacing) / (minWidth + spacing)));
            if (columns == lastColumns) return;
            lastColumns = columns;
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            for (int c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int rows = (items.Count + columns - 1) / columns;
            for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < items.Count; i++)
            {
                Grid.SetColumn((FrameworkElement)items[i], i % columns);
                Grid.SetRow((FrameworkElement)items[i], i / columns);
            }
        }
        foreach (var item in items) grid.Children.Add(item);
        grid.SizeChanged += (_, e) => Layout(e.NewSize.Width);
        Layout(minWidth * 2 + spacing);
        return grid;
    }

    public static async Task<bool> Confirm(XamlRoot root, string title, string message, string yes)
    {
        var loc = Loc.Current;
        var dialog = new ContentDialog
        {
            XamlRoot = root, Title = title, Content = message, PrimaryButtonText = yes, CloseButtonText = loc["adhkar_cancel"],
            DefaultButton = ContentDialogButton.Close,
            FlowDirection = loc.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public static Button Back(Action click)
    {
        var button = new Button
        {
            Content = Icon(Loc.Current.IsRtl ? "arrow_forward" : "arrow_back", 18, B("SirajOnSurfaceBrush")),
            Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(button, Loc.Current["close"]);
        button.Click += (_, _) => click();
        return button;
    }

    public static void Pulse(UIElement element, float to = 1.04f)
    {
        if (!Motion) return;
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        if (element is FrameworkElement fe)
            visual.CenterPoint = new Vector3((float)fe.ActualWidth / 2, (float)fe.ActualHeight / 2, 0);
        var animation = compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(0.35f, new Vector3(to, to, 1));
        animation.InsertKeyFrame(1f, Vector3.One);
        animation.Duration = TimeSpan.FromMilliseconds(150);
        visual.StartAnimation("Scale", animation);
    }

    public static void Rise(UIElement element, int delayMs = 0, float from = 10, int ms = 240)
    {
        if (!Motion) return;
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var move = compositor.CreateVector3KeyFrameAnimation();
        move.InsertKeyFrame(0, new Vector3(0, from, 0));
        move.InsertKeyFrame(1, Vector3.Zero, compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1)));
        var fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1, compositor.CreateCubicBezierEasingFunction(new Vector2(0.3f, 0), new Vector2(0.7f, 1)));
        foreach (KeyFrameAnimation animation in new KeyFrameAnimation[] { move, fade })
        {
            animation.Duration = TimeSpan.FromMilliseconds(ms);
            animation.DelayTime = TimeSpan.FromMilliseconds(delayMs);
            animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        }
        visual.StartAnimation("Translation", move);
        visual.StartAnimation("Opacity", fade);
    }
}

internal sealed class Ring : Grid
{
    private readonly Ellipse _track = new();
    private readonly Microsoft.UI.Xaml.Shapes.Path _arc = new() { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private double _target;
    private double _shown;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _anim;

    public double Thickness { get; init; } = 10;
    public bool Smooth { get; init; }

    public Ring()
    {
        FlowDirection = FlowDirection.LeftToRight;
        Children.Add(_track);
        Children.Add(_arc);
        _track.Stroke = Kit.B("SirajSurfaceContainerHighBrush");
        _arc.Stroke = Kit.B("SirajPrimaryBrush");
        SizeChanged += (_, _) => Draw(_shown);
        Loaded += (_, _) =>
        {
            _track.StrokeThickness = Thickness;
            _arc.StrokeThickness = Thickness;
            Draw(_shown);
        };
        Unloaded += (_, _) =>
        {
            _anim?.Stop();
            _anim = null;
        };
    }

    public Brush Fill { set => _arc.Stroke = value; }

    public Brush Track { set => _track.Stroke = value; }

    public double Fraction
    {
        get => _target;
        set
        {
            double clamped = Math.Clamp(value, 0, 1);
            if (!Smooth || !Kit.Motion || !IsLoaded || Math.Abs(clamped - _shown) < 0.002)
            {
                _target = _shown = clamped;
                _anim?.Stop();
                Draw(_shown);
                return;
            }
            _target = clamped;
            if (_anim == null)
            {
                _anim = DispatcherQueue.CreateTimer();
                _anim.Interval = TimeSpan.FromMilliseconds(16);
                _anim.Tick += (_, _) =>
                {
                    _shown += (_target - _shown) * 0.28;
                    if (Math.Abs(_target - _shown) < 0.002)
                    {
                        _shown = _target;
                        _anim.Stop();
                    }
                    Draw(_shown);
                };
            }
            if (!_anim.IsRunning) _anim.Start();
        }
    }

    private void Draw(double fraction)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        double r = (size - Thickness) / 2, cx = ActualWidth / 2, cy = ActualHeight / 2;
        if (fraction <= 0)
        {
            _arc.Data = null;
            return;
        }
        double f = Math.Min(fraction, 0.9999);
        double angle = f * 2 * Math.PI;
        var start = new Windows.Foundation.Point(cx, cy - r);
        var end = new Windows.Foundation.Point(cx + r * Math.Sin(angle), cy - r * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = end, Size = new Windows.Foundation.Size(r, r), IsLargeArc = f > 0.5, SweepDirection = SweepDirection.Clockwise,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        _arc.Data = geometry;
    }
}
