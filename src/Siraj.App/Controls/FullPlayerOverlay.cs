using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Services;
using Siraj.Core.Audio;

namespace Siraj.App.Controls;

public sealed class FullPlayerOverlay : UserControl
{
    private readonly Border _backdrop;
    private readonly Border _card;
    private readonly Border _artHalo;
    private readonly TextBlock _sourceHeading = Kit.Text("", "SirajCaption", 12);
    private readonly TextBlock _sourceName = Kit.Text("", "SirajBody", 14, bold: true);
    private readonly ContentControl _artSlot = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _title = Kit.Text("", "SirajTitle", 24);
    private readonly TextBlock _artist = Kit.Text("", "SirajBody", 15, brush: Kit.B("SirajOnSurfaceVariantBrush"));
    private readonly StackPanel _pills = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly WavyProgressBar _scrubber = new();
    private readonly TextBlock _elapsed = Kit.Text("0:00", "SirajCaption", 12);
    private readonly TextBlock _remaining = Kit.Text("--:--", "SirajCaption", 12);
    private readonly Grid _scrubberBlock = new() { RowSpacing = 4 };
    private readonly StackPanel _transport = new() { Orientation = Orientation.Horizontal, Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center, FlowDirection = FlowDirection.LeftToRight };
    private readonly Button _shuffleBtn, _prevBtn, _back10Btn, _playBtn, _fwd10Btn, _nextBtn, _repeatBtn;
    private readonly SirajVectorIcon _playIcon;
    private readonly SirajVectorIcon _repeatIcon;
    private readonly StackPanel _extrasRow = new() { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _speedBtn, _sleepBtn, _ambientBtn;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _breathe;
    private double _breathePhase;
    private bool _isOpen;
    private int _animToken;

    public event Action<bool>? OpenChanged;

    public FullPlayerOverlay()
    {
        Visibility = Visibility.Collapsed;

        _title.HorizontalTextAlignment = TextAlignment.Center;
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _artist.HorizontalTextAlignment = TextAlignment.Center;
        _artist.HorizontalAlignment = HorizontalAlignment.Center;
        _sourceHeading.HorizontalAlignment = HorizontalAlignment.Center;
        _sourceName.HorizontalAlignment = HorizontalAlignment.Center;

        var topBar = new Grid();
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var closeBtn = CircleBtn("expand_more", 40, Close);
        topBar.Children.Add(closeBtn);
        var headingStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        headingStack.Children.Add(_sourceHeading);
        headingStack.Children.Add(_sourceName);
        Grid.SetColumn(headingStack, 1);
        topBar.Children.Add(headingStack);
        _ambientBtn = CircleBtn("ambient_fountain", 40, () => { });
        _ambientBtn.Flyout = BuildAmbientFlyout();
        Grid.SetColumn(_ambientBtn, 2);
        topBar.Children.Add(_ambientBtn);

        _scrubber.SeekRequested += f =>
        {
            if (App.Media.Duration > TimeSpan.Zero)
                App.Media.Seek(TimeSpan.FromMilliseconds(f * App.Media.Duration.TotalMilliseconds));
        };
        _scrubberBlock.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _scrubberBlock.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _scrubberBlock.Children.Add(_scrubber);
        var timeRow = new Grid { FlowDirection = FlowDirection.LeftToRight };
        timeRow.Children.Add(_elapsed);
        _remaining.HorizontalAlignment = HorizontalAlignment.Right;
        timeRow.Children.Add(_remaining);
        Grid.SetRow(timeRow, 1);
        _scrubberBlock.Children.Add(timeRow);

        _shuffleBtn = CircleBtn("shuffle", 40, () => App.Media.ToggleShuffle());
        _prevBtn = CircleBtn("skip_previous", 44, () => App.Media.Previous());
        _back10Btn = CircleBtn("replay_10", 40, () => App.Media.SeekBy(-10));
        _playIcon = Kit.Icon("play", 28, Kit.B("SirajOnPrimaryBrush"));
        _playBtn = new Button
        {
            Content = _playIcon, Width = 64, Height = 64, Padding = new Thickness(0), CornerRadius = new CornerRadius(32),
            Background = Kit.B("SirajPrimaryBrush"), BorderThickness = new Thickness(0),
            UseSystemFocusVisuals = false,
        };
        _playBtn.Click += (_, _) => App.Media.TogglePlayPause();
        Kit.Lift(_playBtn);
        _fwd10Btn = CircleBtn("forward_30", 40, () => App.Media.SeekBy(30));
        _nextBtn = CircleBtn("skip_next", 44, () => App.Media.Next());
        _repeatIcon = Kit.Icon("repeat", 18, Kit.B("SirajOnSurfaceBrush"));
        _repeatBtn = new Button
        {
            Content = _repeatIcon, Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(20),
            Background = Kit.B("SirajGlassPillBrush"), BorderBrush = Kit.B("SirajGlassBorderBrush"), BorderThickness = new Thickness(1),
        };
        _repeatBtn.Click += (_, _) => App.Media.CycleRepeat();

        _transport.Children.Add(_shuffleBtn);
        _transport.Children.Add(_prevBtn);
        _transport.Children.Add(_back10Btn);
        _transport.Children.Add(_playBtn);
        _transport.Children.Add(_fwd10Btn);
        _transport.Children.Add(_nextBtn);
        _transport.Children.Add(_repeatBtn);

        _speedBtn = PillAction("1x", BuildSpeedFlyout());
        _sleepBtn = PillAction("", BuildSleepFlyout());
        _extrasRow.Children.Add(_speedBtn);
        _extrasRow.Children.Add(_sleepBtn);

        var body = new StackPanel { Spacing = 16, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Stretch };
        var grabber = new Border
        {
            Width = 42, Height = 4.5, CornerRadius = new CornerRadius(2.5),
            Background = Kit.B("SirajOutlineVariantBrush"), Opacity = 0.55,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -4, 0, -2),
        };
        body.Children.Add(grabber);
        body.Children.Add(topBar);

        _artHalo = new Border
        {
            Width = 228, Height = 228, CornerRadius = new CornerRadius(34),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Background = Kit.B("SirajPrimarySoftBrush"),
            BorderBrush = Kit.B("SirajPrimaryBorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = _artSlot,
        };
        _artHalo.CenterPoint = new Vector3(114, 114, 0);
        body.Children.Add(_artHalo);

        var info = new StackPanel { Spacing = 3 };
        info.Children.Add(_title);
        info.Children.Add(_artist);
        body.Children.Add(info);
        body.Children.Add(_pills);
        body.Children.Add(_scrubberBlock);
        body.Children.Add(_transport);
        body.Children.Add(_extrasRow);

        _card = new Border
        {
            CornerRadius = new CornerRadius(32),
            Padding = new Thickness(28, 20, 28, 26),
            Background = Kit.B("SirajGlassElevatedBrush"),
            BorderBrush = Kit.B("SirajGlassBorderBrush"),
            BorderThickness = new Thickness(1.2),
            Shadow = new ThemeShadow(),
            Translation = new Vector3(0, 0, 32),
            MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = body,
        };
        ElementCompositionPreview.SetIsTranslationEnabled(_card, true);

        _backdrop = new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(165, 4, 8, 14)),
            Padding = new Thickness(24),
            Child = _card,
        };
        _backdrop.Tapped += (s, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, _backdrop)) Close();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
        Content = _backdrop;

        Loaded += (_, _) =>
        {
            App.Media.Changed += Refresh;
            App.Media.PositionTick += UpdateProgress;
            _breathe = DispatcherQueue.CreateTimer();
            _breathe.Interval = TimeSpan.FromMilliseconds(45);
            _breathe.Tick += (_, _) => StepBreathe();
        };
        Unloaded += (_, _) =>
        {
            App.Media.Changed -= Refresh;
            App.Media.PositionTick -= UpdateProgress;
            _breathe?.Stop();
            _breathe = null;
        };
    }

    private void StepBreathe()
    {
        if (Visibility != Visibility.Visible || !App.Media.IsPlaying || !Kit.Motion)
        {
            _artHalo.Scale = Vector3.One;
            _breathe?.Stop();
            return;
        }
        _breathePhase += 0.045;
        float s = 1f + 0.018f * MathF.Sin((float)_breathePhase * 1.4f);
        _artHalo.Scale = new Vector3(s, s, 1f);
    }

    public void Open()
    {
        if (!App.Media.HasMedia) return;
        _isOpen = true;
        _animToken++;
        Visibility = Visibility.Visible;
        Refresh();
        OpenChanged?.Invoke(true);
        Kit.AnimateIn(_backdrop, 0, 0, 1f, 105);
        Kit.AnimateIn(_card, 0, 14, 0.975f, 135);
        if (App.Media.IsPlaying && Kit.Motion) _breathe?.Start();
    }

    public void Close()
    {
        if (!_isOpen && Visibility == Visibility.Collapsed) return;
        _isOpen = false;
        _breathe?.Stop();
        _artHalo.Scale = Vector3.One;
        OpenChanged?.Invoke(false);
        int token = ++_animToken;
        Kit.FadeTo(_backdrop, 0f, 95);
        Kit.AnimateOut(_card, 0, 12, 0.975f, 95, () =>
        {
            if (_animToken == token && !_isOpen) Visibility = Visibility.Collapsed;
        });
    }

    private void Refresh()
    {
        if (!App.Media.HasMedia)
        {
            Close();
            return;
        }
        var loc = Loc.Current;
        bool isRadio = App.Media.Kind == MediaKind.Radio;

        _pills.Children.Clear();
        if (isRadio && App.Media.CurrentStation is { } st)
        {
            _sourceHeading.Text = loc["radio_live_badge"];
            _sourceName.Text = st.HasPlace ? st.Location(loc.Language) : loc["radio_stations_title"];
            _artSlot.Content = PortraitAvatar.Create(st.ArtworkFile, st.Name(loc.Language), 210, 28, activeRing: App.Media.IsPlaying);
            _title.Text = st.Name(loc.Language);
            _artist.Text = st.Description(loc.Language);
            _pills.Children.Add(Kit.Pill(App.Media.IsBuffering ? loc["adhkar_radio_connecting"] : loc["radio_live_badge"], "SirajPrimarySoftBrush", "SirajPrimaryBrush"));
        }
        else if (App.Media.CurrentTrack is { } tr)
        {
            _sourceHeading.Text = loc["listen_playing_from"];
            _sourceName.Text = tr.Subtitle;
            _artSlot.Content = PortraitAvatar.Create(tr.Artwork, tr.Subtitle, 210, 28, activeRing: App.Media.IsPlaying);
            _title.Text = tr.Title;
            _artist.Text = tr.Subtitle;
            _pills.Children.Add(Kit.Pill(loc["listen_streaming"], "SirajPrimarySoftBrush", "SirajPrimaryBrush"));
        }

        if (App.Media.ActiveAmbientId is { } ambId && ListeningLibrary.AmbientSounds.FirstOrDefault(a => a.Id == ambId) is { } amb)
            _pills.Children.Add(Kit.Pill(loc[amb.TitleKey], "SirajSecondaryContainerBrush", "SirajOnSecondaryContainerBrush"));
        if (App.Media.HasSleepTimer)
        {
            string label = App.Media.SleepAtEndOfTrack
                ? loc["player_sleep_at_end"]
                : loc.Format("player_sleep_remaining", FormatClock(App.Media.SleepRemaining ?? TimeSpan.Zero));
            _pills.Children.Add(Kit.Pill(label, "SirajGoldSoftBrush", "SirajGoldBrush"));
        }

        _playIcon.IconKey = App.Media.IsPlaying ? "pause" : "play";
        _scrubber.IsPlaying = App.Media.IsPlaying;
        if (Visibility == Visibility.Visible && App.Media.IsPlaying && Kit.Motion)
        {
            _breathe?.Start();
        }
        else
        {
            _breathe?.Stop();
            _artHalo.Scale = Vector3.One;
        }
        _scrubberBlock.Visibility = isRadio ? Visibility.Collapsed : Visibility.Visible;
        _shuffleBtn.Visibility = isRadio ? Visibility.Collapsed : Visibility.Visible;
        _prevBtn.Visibility = isRadio ? Visibility.Collapsed : Visibility.Visible;
        _back10Btn.Visibility = isRadio ? Visibility.Collapsed : Visibility.Visible;
        _fwd10Btn.Visibility = isRadio ? Visibility.Collapsed : Visibility.Visible;
        _nextBtn.Visibility = isRadio ? Visibility.Collapsed : Visibility.Visible;
        _repeatBtn.Visibility = isRadio ? Visibility.Collapsed : Visibility.Visible;
        _speedBtn.Visibility = isRadio ? Visibility.Collapsed : Visibility.Visible;

        _shuffleBtn.Background = Kit.B(App.Media.Shuffle ? "SirajPrimarySoftBrush" : "SirajGlassPillBrush");
        _repeatBtn.Background = Kit.B(App.Media.Repeat != RepeatMode.Off ? "SirajPrimarySoftBrush" : "SirajGlassPillBrush");
        _repeatIcon.IconKey = App.Media.Repeat == RepeatMode.One ? "repeat_one" : "repeat";
        _ambientBtn.Background = Kit.B(App.Media.ActiveAmbientId != null ? "SirajPrimarySoftBrush" : "SirajGlassPillBrush");
        _speedBtn.Content = $"{App.Media.Speed:0.##}×";
        _sleepBtn.Content = loc["player_sleep_timer"];
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (Visibility != Visibility.Visible || App.Media.Kind != MediaKind.Listen) return;
        var pos = App.Media.Position;
        var dur = App.Media.Duration;
        _scrubber.IsPlaying = App.Media.IsPlaying;
        _scrubber.Fraction = dur > TimeSpan.Zero ? pos.TotalMilliseconds / dur.TotalMilliseconds : 0;
        _elapsed.Text = Loc.Current.Digitize(FormatClock(pos));
        _remaining.Text = dur > TimeSpan.Zero ? "-" + Loc.Current.Digitize(FormatClock(dur > pos ? dur - pos : TimeSpan.Zero)) : "--:--";
    }

    private static string FormatClock(TimeSpan ts) =>
        ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}" : $"{ts.Minutes}:{ts.Seconds:D2}";

    private static Button CircleBtn(string iconKey, double size, Action click)
    {
        var b = new Button
        {
            Content = Kit.Icon(iconKey, 18, Kit.B("SirajOnSurfaceBrush")),
            Width = size, Height = size, Padding = new Thickness(0), CornerRadius = new CornerRadius(size / 2),
            Background = Kit.B("SirajGlassPillBrush"), BorderBrush = Kit.B("SirajGlassBorderBrush"), BorderThickness = new Thickness(1),
        };
        b.Click += (_, _) => click();
        return b;
    }

    private static Button PillAction(string text, FlyoutBase flyout) => new()
    {
        Content = text, CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 7, 14, 7),
        Background = Kit.B("SirajGlassPillBrush"), BorderBrush = Kit.B("SirajGlassBorderBrush"), BorderThickness = new Thickness(1),
        FontFamily = (FontFamily)Application.Current.Resources["SirajFont"], FontSize = 12.5, FontWeight = FontWeights.SemiBold,
        Flyout = flyout,
    };

    private static MenuFlyout BuildSpeedFlyout()
    {
        var menu = new MenuFlyout();
        foreach (double sp in new[] { 0.75, 1.0, 1.25, 1.5, 2.0 })
        {
            var item = new MenuFlyoutItem { Text = $"{sp:0.##}×" };
            item.Click += (_, _) => App.Media.SetSpeed(sp);
            menu.Items.Add(item);
        }
        return menu;
    }

    private static MenuFlyout BuildSleepFlyout()
    {
        var loc = Loc.Current;
        var menu = new MenuFlyout();
        void Add(string label, int? mins, bool end = false)
        {
            var item = new MenuFlyoutItem { Text = label };
            item.Click += (_, _) => App.Media.SetSleepTimer(mins, end);
            menu.Items.Add(item);
        }
        Add(loc["player_sleep_off"], null);
        Add(loc.Format("player_sleep_minutes", loc.Number(15)), 15);
        Add(loc.Format("player_sleep_minutes", loc.Number(30)), 30);
        Add(loc.Format("player_sleep_minutes", loc.Number(45)), 45);
        Add(loc.Format("player_sleep_minutes", loc.Number(60)), 60);
        Add(loc["player_sleep_end_of_track"], null, end: true);
        return menu;
    }

    public static Flyout BuildAmbientFlyout()
    {
        var loc = Loc.Current;
        var stack = new StackPanel { Spacing = 12, Width = 320 };
        stack.Children.Add(Kit.Text(loc["ambient_title"], "SirajHeading"));
        stack.Children.Add(Kit.Text(loc["ambient_subtitle"], "SirajCaption"));

        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        for (int c = 0; c < 3; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int rows = (ListeningLibrary.AmbientSounds.Count + 2) / 3;
        for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var buttons = new Dictionary<string, Button>();
        void SyncSelection()
        {
            foreach (var (id, b) in buttons)
            {
                bool active = App.Media.ActiveAmbientId == id;
                b.Background = Kit.B(active ? "SirajPrimarySoftBrush" : "SirajGlassCardBrush");
                b.BorderBrush = Kit.B(active ? "SirajPrimaryBrush" : "SirajGlassBorderBrush");
            }
        }

        for (int i = 0; i < ListeningLibrary.AmbientSounds.Count; i++)
        {
            var sound = ListeningLibrary.AmbientSounds[i];
            var inner = new StackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center };
            inner.Children.Add(SirajVectorIcon.Make(sound.IconKey, 20, Kit.B("SirajPrimaryBrush")));
            var lbl = Kit.Text(loc[sound.TitleKey], "SirajCaption", 11);
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            lbl.TextAlignment = TextAlignment.Center;
            inner.Children.Add(lbl);
            var btn = new Button
            {
                Content = inner, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6, 10, 6, 10),
                CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1),
            };
            btn.Click += (_, _) =>
            {
                App.Media.SetAmbient(sound.Id);
                SyncSelection();
            };
            buttons[sound.Id] = btn;
            Grid.SetColumn(btn, i % 3);
            Grid.SetRow(btn, i / 3);
            grid.Children.Add(btn);
        }
        SyncSelection();
        stack.Children.Add(grid);

        var volRow = new StackPanel { Spacing = 4 };
        volRow.Children.Add(Kit.Text(loc["ambient_volume"], "SirajCaption"));
        var slider = new Slider { Minimum = 5, Maximum = 100, Value = App.Media.AmbientVolume * 100, StepFrequency = 5 };
        slider.ValueChanged += (_, e) => App.Media.SetAmbientVolume(e.NewValue / 100.0);
        volRow.Children.Add(slider);
        stack.Children.Add(volRow);

        var off = Ui.Button(loc["ambient_off"], () =>
        {
            App.Media.SetAmbient(null);
            SyncSelection();
        });
        off.HorizontalAlignment = HorizontalAlignment.Stretch;
        stack.Children.Add(off);

        return new Flyout { Content = stack };
    }
}
