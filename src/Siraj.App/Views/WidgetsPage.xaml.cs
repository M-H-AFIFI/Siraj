using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Controls;
using Siraj.App.Native;
using Siraj.App.Services;
using Siraj.App.Widgets;
using Siraj.Core.Settings;
using Siraj.Core.Theme;
using Windows.Foundation;
using Windows.UI;

namespace Siraj.App.Views;

public sealed partial class WidgetsPage : Page
{
    private TaskbarPillView? _tbPreview;
    private Grid? _previewTrack;
    private Slider? _posSlider;
    private TextBlock? _posPctLabel;
    private bool _syncingPos;

    public WidgetsPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
        var loc = Loc.Current;
        TitleText.Text = loc["win_nav_widgets"];
        DescText.Text = loc["win_widgets_desc"];
        PlacedTitle.Text = loc["win_widgets_placed"];
        GalleryTitle.Text = loc["win_widget_gallery"];
        BuildTaskbarSection();
        BuildPlaced();
        BuildGallery();
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        App.Widgets.Changed += BuildPlaced;
        App.Settings.Changed += OnSettingsChanged;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        App.Widgets.Changed -= BuildPlaced;
        App.Settings.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged(AppSettings old, AppSettings updated)
    {
        if (old.TaskbarWidget == updated.TaskbarWidget) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            _tbPreview?.Apply(updated.TaskbarWidget);
            SyncPreviewPosition(updated.TaskbarWidget);
            if (_posSlider != null && !_syncingPos && updated.TaskbarWidget.Position == "custom")
            {
                _syncingPos = true;
                _posSlider.Value = Math.Clamp(updated.TaskbarWidget.CustomPositionPercent, 0, 100);
                if (_posPctLabel != null) _posPctLabel.Text = Loc.Current.Percent(updated.TaskbarWidget.CustomPositionPercent);
                _syncingPos = false;
            }
        });
    }

    private void BuildTaskbarSection()
    {
        var loc = Loc.Current;
        var cfg = App.Settings.Current.TaskbarWidget;
        TaskbarSection.Children.Clear();
        TaskbarSection.Children.Add(Ui.SectionWithIcon(loc["win_taskbar_title"], "desktop_windows"));

        // Live preview on a miniature Windows 11 taskbar strip where the pill moves across the full width.
        _tbPreview = new TaskbarPillView(cfg, live: true) { VerticalAlignment = VerticalAlignment.Center };
        var previewStrip = BuildTaskbarPreviewStrip(_tbPreview);
        SyncPreviewPosition(cfg);

        void Mutate(Func<TaskbarWidgetConfig, TaskbarWidgetConfig> change)
        {
            App.Settings.Update(s => s with { TaskbarWidget = change(s.TaskbarWidget) });
            var cur = App.Settings.Current.TaskbarWidget;
            _tbPreview.Apply(cur);
            SyncPreviewPosition(cur);
        }

        var previewFlyoutBtn = Ui.Button(loc["win_taskbar_preview_flyout"], () => App.TaskbarWidget?.PreviewFlyout(), primary: true);
        var urgentPreviewToggle = Ui.Toggle(false, on => _tbPreview.ForceUrgent = on);

        var previewBar = new StackPanel { Spacing = 10, Padding = new Thickness(20, 16, 20, 16) };
        previewBar.Children.Add(previewStrip);
        var previewActions = new Grid { ColumnSpacing = 12 };
        previewActions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        previewActions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var urgentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        urgentRow.Children.Add(Ui.Text(loc["win_taskbar_urgent_before"], "SirajCaption"));
        urgentRow.Children.Add(urgentPreviewToggle);
        previewActions.Children.Add(urgentRow);
        Grid.SetColumn(previewFlyoutBtn, 1);
        previewActions.Children.Add(previewFlyoutBtn);
        previewBar.Children.Add(previewActions);

        var optionsStack = new StackPanel
        {
            Opacity = cfg.Enabled ? 1.0 : 0.45,
            IsHitTestVisible = cfg.Enabled,
        };

        var enableToggle = Ui.Toggle(cfg.Enabled, on =>
        {
            Mutate(c => c with { Enabled = on });
            optionsStack.Opacity = on ? 1.0 : 0.45;
            optionsStack.IsHitTestVisible = on;
        });

        var layoutSeg = Ui.Segments(
        [
            ("full", loc["win_taskbar_layout_full"]),
            ("compact", loc["win_taskbar_layout_compact"]),
            ("countdown", loc["win_taskbar_layout_countdown"]),
        ], cfg.Layout, v => Mutate(c => c with { Layout = v }));

        var styleSeg = Ui.Segments(
        [
            ("none", loc["win_taskbar_style_none"]),
            ("auto", loc["win_taskbar_style_auto"]),
            ("glass", loc["win_taskbar_style_glass"]),
            ("accent", loc["win_taskbar_style_accent"]),
            ("solid", loc["win_taskbar_style_solid"]),
        ], cfg.Style, v => Mutate(c => c with { Style = v }));

        // Color swatches: "App" + all 12 Siraj palettes.
        var colorGrid = BuildTaskbarColorSwatches(cfg.Palette, p => Mutate(c => c with { Palette = p }));

        var textColorSeg = Ui.Segments(
        [
            ("auto", loc["win_taskbar_style_auto"]),
            ("white", loc["win_taskbar_text_white"]),
            ("black", loc["win_taskbar_text_black"]),
            ("gold", loc["win_taskbar_text_gold"]),
            ("accent", loc["win_taskbar_style_accent"]),
        ], cfg.TextColor, v => Mutate(c => c with { TextColor = v }));

        var urgentStyleSeg = Ui.Segments(
        [
            ("gold", loc["win_taskbar_text_gold"]),
            ("accent", loc["win_taskbar_style_accent"]),
            ("coral", loc["win_taskbar_urgent_coral"]),
            ("none", loc["win_off"]),
        ], cfg.UrgentStyle, v => Mutate(c => c with { UrgentStyle = v }));

        var posSeg = Ui.Segments(
        [
            ("auto", loc["win_taskbar_pos_auto"]),
            ("left", loc["win_taskbar_pos_left"]),
            ("center", loc["win_taskbar_pos_center"]),
            ("right", loc["win_taskbar_pos_right"]),
            ("custom", loc["win_taskbar_pos_custom"]),
        ], cfg.Position, v => Mutate(c => c with { Position = v, OffsetX = 0 }));

        // Full-width taskbar position slider (0% far left -> 100% far right).
        _posPctLabel = Ui.Text(loc.Percent(cfg.CustomPositionPercent), "SirajCaption");
        _posPctLabel.MinWidth = 44;
        _posPctLabel.VerticalAlignment = VerticalAlignment.Center;
        _posPctLabel.TextAlignment = TextAlignment.End;
        _posSlider = new Slider
        {
            Minimum = 0, Maximum = 100, StepFrequency = 1, Value = Math.Clamp(cfg.CustomPositionPercent, 0, 100),
            Width = 220, VerticalAlignment = VerticalAlignment.Center, FlowDirection = FlowDirection.LeftToRight,
        };
        _posSlider.ValueChanged += (_, e) =>
        {
            if (_syncingPos) return;
            _syncingPos = true;
            double pct = Math.Round(e.NewValue, 1);
            _posPctLabel.Text = loc.Percent(pct);
            Mutate(c => c with { Position = "custom", CustomPositionPercent = pct, OffsetX = 0 });
            _syncingPos = false;
        };
        var resetBtn = Ui.Button(loc["win_taskbar_reset"], () => Mutate(c => c with { Position = "auto", OffsetX = 0 }));
        var posSliderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        posSliderRow.Children.Add(_posSlider);
        posSliderRow.Children.Add(_posPctLabel);
        posSliderRow.Children.Add(resetBtn);

        var cornerSeg = Ui.Segments(
        [
            (6, loc["win_taskbar_corner_sharp"]),
            (10, loc["win_taskbar_corner_round"]),
            (18, loc["win_taskbar_corner_pill"]),
        ], cfg.CornerRadius, v => Mutate(c => c with { CornerRadius = v }));

        var sizeSeg = Ui.Segments(
        [
            (0.85, loc["win_widget_size_small"]),
            (1.0, loc["win_widget_size_medium"]),
            (1.15, loc["win_widget_size_large"]),
        ], Math.Abs(cfg.Scale - 0.85) < 0.05 ? 0.85 : Math.Abs(cfg.Scale - 1.15) < 0.05 ? 1.15 : 1.0,
        v => Mutate(c => c with { Scale = v }));

        var opacityValue = Ui.Text(loc.Percent(cfg.Opacity), "SirajCaption");
        opacityValue.MinWidth = 42;
        opacityValue.VerticalAlignment = VerticalAlignment.Center;
        opacityValue.TextAlignment = TextAlignment.End;
        var opacitySlider = new Slider
        {
            Minimum = 25, Maximum = 100, StepFrequency = 5, Value = Math.Clamp(cfg.Opacity, 25, 100),
            Width = 170, VerticalAlignment = VerticalAlignment.Center,
        };
        opacitySlider.ValueChanged += (_, e) =>
        {
            int pct = (int)Math.Round(e.NewValue);
            opacityValue.Text = loc.Percent(pct);
            Mutate(c => c with { Opacity = pct });
        };
        var opacityControl = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        opacityControl.Children.Add(opacitySlider);
        opacityControl.Children.Add(opacityValue);

        var urgentChoice = Ui.Choice(
        [
            (0, loc["win_off"]),
            (5, loc.Format("win_minutes_before", loc.Number(5))),
            (10, loc.Format("win_minutes_before", loc.Number(10))),
            (15, loc.Format("win_minutes_before", loc.Number(15))),
            (30, loc.Format("win_minutes_before", loc.Number(30))),
        ], cfg.UrgentMinutes, v => Mutate(c => c with { UrgentMinutes = v }), width: 190);

        UIElement[] rows =
        [
            Ui.Row(loc["win_taskbar_style"], null, styleSeg, stack: true),
            Ui.Row(loc["win_taskbar_color"], null, colorGrid, stack: true),
            Ui.Row(loc["win_taskbar_text_color"], null, textColorSeg),
            Ui.Row(loc["win_taskbar_layout"], null, layoutSeg),
            Ui.Row(loc["win_taskbar_position"], loc["win_taskbar_pos_hint"], posSeg, stack: true),
            Ui.Row(loc["win_taskbar_pos_slider"], null, posSliderRow),
            Ui.Row(loc["win_taskbar_lock"], null, Ui.Toggle(cfg.Locked, v => Mutate(c => c with { Locked = v }))),
            Ui.Row(loc["win_taskbar_corners"], null, cornerSeg),
            Ui.Row(loc["win_widget_size"], null, sizeSeg),
            Ui.Row(loc["win_taskbar_opacity"], null, opacityControl),
            Ui.Row(loc["win_taskbar_show_border"], null, Ui.Toggle(cfg.ShowBorder, v => Mutate(c => c with { ShowBorder = v }))),
            Ui.Row(loc["win_taskbar_urgent_before"], null, urgentChoice),
            Ui.Row(loc["win_taskbar_urgent_color"], null, urgentStyleSeg),
            Ui.Row(loc["win_taskbar_show_time"], null, Ui.Toggle(cfg.ShowPrayerTime, v => Mutate(c => c with { ShowPrayerTime = v }))),
            Ui.Row(loc["win_taskbar_show_prefix"], null, Ui.Toggle(cfg.ShowPrefix, v => Mutate(c => c with { ShowPrefix = v }))),
            Ui.Row(loc["win_taskbar_show_seconds"], null, Ui.Toggle(cfg.ShowSeconds, v => Mutate(c => c with { ShowSeconds = v }))),
            Ui.Row(loc["win_taskbar_pulse"], null, Ui.Toggle(cfg.PulseOnUrgent, v => Mutate(c => c with { PulseOnUrgent = v }))),
            Ui.Row(loc["win_taskbar_flyout_hijri"], null, Ui.Toggle(cfg.ShowFlyoutHijri, v => Mutate(c => c with { ShowFlyoutHijri = v }))),
            Ui.Row(loc["win_taskbar_flyout_verse"], null, Ui.Toggle(cfg.ShowFlyoutVerse, v => Mutate(c => c with { ShowFlyoutVerse = v }))),
            Ui.Row(loc["win_taskbar_hide_fullscreen"], null, Ui.Toggle(cfg.HideInFullscreen, v => Mutate(c => c with { HideInFullscreen = v }))),
        ];

        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0) optionsStack.Children.Add(Ui.Hairline());
            optionsStack.Children.Add(rows[i]);
        }

        TaskbarSection.Children.Add(Ui.Card(
            previewBar,
            Ui.Row(loc["win_taskbar_enable"], loc["win_taskbar_desc"], enableToggle),
            optionsStack));
    }

    private FrameworkElement BuildTaskbarColorSwatches(string selected, Action<string> onPick)
    {
        var loc = Loc.Current;
        var wrap = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var choices = new List<(string Id, string Label, Color Swatch)>
        {
            ("App", loc["win_widget_colour_app"], ThemeService.ToColor(App.Theme.Scheme.Primary)),
        };
        foreach (var name in Palettes.Names)
            choices.Add((name, loc[Ui.PaletteKey(name)], ThemeService.ToColor(Palettes.Swatch(name))));

        var buttons = new List<(string Id, Button Btn, Border Ring)>();

        void RefreshSelection(string activeId)
        {
            foreach (var (id, btn, ring) in buttons)
            {
                bool on = id == activeId;
                ring.BorderBrush = on
                    ? Kit.Clone("SirajPrimaryBrush")
                    : new SolidColorBrush(Color.FromArgb(42, 128, 128, 128));
                ring.BorderThickness = new Thickness(on ? 2 : 1);
                btn.Background = on ? Kit.Clone("SirajPrimarySoftBrush") : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            }
        }

        foreach (var (id, label, col) in choices)
        {
            var dot = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(col),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            };
            var ring = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(8, 5, 10, 5),
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 7,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        dot,
                        new TextBlock
                        {
                            Text = label,
                            FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    },
                },
            };
            var btn = new Button
            {
                Content = ring,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            };
            string picked = id;
            btn.Click += (_, _) =>
            {
                RefreshSelection(picked);
                onPick(picked);
            };
            buttons.Add((id, btn, ring));
            wrap.Children.Add(btn);
        }

        RefreshSelection(selected);

        return new ScrollViewer
        {
            Content = wrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Disabled,
            Padding = new Thickness(0, 0, 0, 4),
        };
    }

    private Border BuildTaskbarPreviewStrip(TaskbarPillView pill)
    {
        bool light = Win32.IsTaskbarLightTheme();
        var fg = new SolidColorBrush(light ? Color.FromArgb(220, 24, 28, 36) : Color.FromArgb(220, 240, 244, 252));
        var textFont = (FontFamily)Application.Current.Resources["SirajFont"];

        var container = new Grid { Height = 54, FlowDirection = FlowDirection.LeftToRight };

        // Subtle colored wallpaper glow behind the taskbar strip so Glass and Text-only modes stand out clearly.
        container.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new GradientStop { Color = light ? Color.FromArgb(235, 224, 232, 244) : Color.FromArgb(245, 18, 24, 38), Offset = 0 },
                    new GradientStop { Color = light ? Color.FromArgb(235, 238, 234, 246) : Color.FromArgb(245, 24, 32, 46), Offset = 0.5 },
                    new GradientStop { Color = light ? Color.FromArgb(235, 226, 238, 240) : Color.FromArgb(245, 16, 24, 34), Offset = 1 },
                },
            },
        });

        // Mock taskbar icons on the opposite edge from the tray so Center (50%) previews over clean space.
        var chrome = new Grid { Padding = new Thickness(16, 4, 16, 4) };
        var apps = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = Loc.Current.IsRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.38,
        };
        foreach (string key in new[] { "apps", "folder", "language", "star" })
            apps.Children.Add(SirajVectorIcon.Make(key, 16, fg));
        chrome.Children.Add(apps);

        var tray = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = Loc.Current.IsRtl ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.78,
        };
        tray.Children.Add(SirajVectorIcon.Make("expand_less", 14, fg));
        tray.Children.Add(SirajVectorIcon.Make("wifi", 15, fg));
        tray.Children.Add(SirajVectorIcon.Make("volume_up", 15, fg));
        tray.Children.Add(new TextBlock
        {
            Text = Loc.Current.Time(App.Clock.Now),
            FontFamily = textFont,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = fg,
            VerticalAlignment = VerticalAlignment.Center,
        });
        chrome.Children.Add(tray);
        container.Children.Add(chrome);

        // Full-width track holding the live pill so left / center / right / 0..100% movement previews accurately.
        _previewTrack = new Grid { Padding = new Thickness(12, 6, 12, 6) };
        _previewTrack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80, GridUnitType.Star) });
        _previewTrack.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _previewTrack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Star) });
        pill.Height = 40;
        Grid.SetColumn(pill, 1);
        _previewTrack.Children.Add(pill);
        container.Children.Add(_previewTrack);

        return new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(light ? Color.FromArgb(40, 0, 0, 0) : Color.FromArgb(42, 255, 255, 255)),
            Child = container,
        };
    }

    private void SyncPreviewPosition(TaskbarWidgetConfig cfg)
    {
        if (_previewTrack == null || _previewTrack.ColumnDefinitions.Count < 3) return;
        double pct = cfg.Position switch
        {
            "left" => 2,
            "center" => 50,
            "right" => 84,
            "custom" => Math.Clamp(cfg.CustomPositionPercent, 0, 100),
            _ => Loc.Current.IsRtl ? 16 : 84,
        };
        double leftW = Math.Max(0.1, pct);
        double rightW = Math.Max(0.1, 100.0 - pct);
        _previewTrack.ColumnDefinitions[0].Width = new GridLength(leftW, GridUnitType.Star);
        _previewTrack.ColumnDefinitions[2].Width = new GridLength(rightW, GridUnitType.Star);
    }

    private void BuildPlaced()
    {
        var loc = Loc.Current;
        Placed.Children.Clear();
        var configs = App.Widgets.Configs;
        if (configs.Count == 0)
        {
            Placed.Children.Add(Ui.Text(loc["win_widgets_none"], "SirajCaption"));
            return;
        }
        var rows = configs.Select(config =>
        {
            var face = WidgetFaces.Find(config.Face);
            var row = Ui.Row(loc[face.TitleKey], loc[config.Background switch
            {
                "soft" => "widget_background_soft",
                "filled" => "widget_background_colour",
                "none" => "widget_background_none",
                _ => "widget_background_glass"
            }], Ui.Button(loc["win_widget_remove"], () => App.Widgets.Remove(config.Id)));
            return (UIElement)row;
        }).ToArray();
        Placed.Children.Add(Ui.Card(rows));
        var peek = Ui.Button(loc["win_widget_show"], App.Widgets.Peek);
        peek.Margin = new Thickness(0, 4, 0, 0);
        Placed.Children.Add(peek);
    }

    private static readonly (string Value, string Key)[] Dresses =
    [
        ("glass", "widget_background_glass"), ("soft", "widget_background_soft"),
        ("filled", "widget_background_colour"), ("none", "widget_background_none"),
    ];

    private void BuildGallery()
    {
        var loc = Loc.Current;
        foreach (var face in WidgetFaces.All)
        {
            var config = new WidgetConfig { Face = face.Id, Background = face.Dress };
            var preview = new WidgetView(config, live: false);
            var stage = Stage();
            stage.Children.Add(new Viewbox
            {
                Child = preview, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
                Margin = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            });

            var dresses = Ui.Segments(Dresses.Select(d => (d.Value, loc[d.Key])).ToList(), config.Background, value =>
            {
                config = config with { Background = value };
                preview.Apply(config);
            });
            var backgrounds = new ScrollViewer
            {
                Content = dresses, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollMode = ScrollMode.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var add = Ui.Button(loc["win_widget_add"], () => App.Widgets.Add(face.Id, config.Background), primary: true);
            add.HorizontalAlignment = HorizontalAlignment.Stretch;
            add.Margin = new Thickness(0, 4, 0, 0);

            var desc = Ui.Text(loc[face.DescriptionKey], "SirajCaption");
            desc.MaxLines = 3;
            var layout = new Grid { RowSpacing = 6 };
            foreach (var h in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto })
                layout.RowDefinitions.Add(new RowDefinition { Height = h });
            var title = Ui.Text(loc[face.TitleKey], "SirajHeading");
            title.Margin = new Thickness(2, 6, 0, 0);
            desc.Margin = new Thickness(2, 0, 0, 0);
            Grid.SetRow(title, 1);
            Grid.SetRow(desc, 2);
            Grid.SetRow(backgrounds, 3);
            Grid.SetRow(add, 4);
            layout.Children.Add(stage);
            layout.Children.Add(title);
            layout.Children.Add(desc);
            layout.Children.Add(backgrounds);
            layout.Children.Add(add);
            Gallery.Children.Add(new Border
            {
                Style = (Style)Application.Current.Resources["SirajCard"], Padding = new Thickness(12), Margin = new Thickness(0, 0, 12, 12),
                Child = layout,
            });
        }
    }

    private static Grid Stage()
    {
        static Color C(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        static Border Glow(double x, double y, double radius, uint argb) => new()
        {
            Background = new RadialGradientBrush
            {
                Center = new Point(x, y), GradientOrigin = new Point(x, y), RadiusX = radius, RadiusY = radius,
                GradientStops =
                {
                    new GradientStop { Color = C(argb), Offset = 0 },
                    new GradientStop { Color = C(argb & 0x00FFFFFF), Offset = 1 },
                },
            },
        };
        var stage = new Grid
        {
            Height = 256, CornerRadius = new CornerRadius(12),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = C(0xFF10264A), Offset = 0 },
                    new GradientStop { Color = C(0xFF2D3F75), Offset = 0.55 },
                    new GradientStop { Color = C(0xFF5B4682), Offset = 1 },
                },
            },
        };
        stage.Children.Add(Glow(0.12, 0.1, 0.75, 0xB02F8F8A));
        stage.Children.Add(Glow(0.95, 1.0, 0.7, 0xA0E09872));
        return stage;
    }
}
