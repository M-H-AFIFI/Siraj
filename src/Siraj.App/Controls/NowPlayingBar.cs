using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Services;

namespace Siraj.App.Controls;

public sealed class NowPlayingBar : UserControl
{
    private readonly Border _shell;
    private readonly ContentControl _artSlot = new();
    private readonly TextBlock _title = new() { FontWeight = FontWeights.Bold, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, MaxLines = 1 };
    private readonly TextBlock _subtitle = new() { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, MaxLines = 1 };
    private readonly EqualizerBars _eq = new(13);
    private readonly Border _progressTrack;
    private readonly Border _progressFill;
    private readonly Button _prevBtn;
    private readonly Button _playBtn;
    private readonly SirajVectorIcon _playIcon;
    private readonly Button _nextBtn;
    private readonly Button _closeBtn;
    private bool _visible;
    private int _animToken;

    public event Action? ExpandRequested;

    public NowPlayingBar()
    {
        Visibility = Visibility.Collapsed;
        HorizontalAlignment = HorizontalAlignment.Center;
        MaxWidth = 680;
        Margin = new Thickness(20, 0, 20, 14);

        var font = (FontFamily)Application.Current.Resources["SirajFont"];
        _title.FontFamily = font;
        _title.Foreground = Kit.B("SirajOnSurfaceBrush");
        _subtitle.FontFamily = font;
        _subtitle.Foreground = Kit.B("SirajOnSurfaceVariantBrush");

        _prevBtn = MiniIconBtn("skip_previous", () =>
        {
            if (App.Media.HasMedia) App.Media.Previous();
            else App.QuranPlayer.Previous();
        });
        _playIcon = Kit.Icon("play", 18, Kit.B("SirajOnPrimaryBrush"));
        _playBtn = new Button
        {
            Content = _playIcon, Width = 38, Height = 38, Padding = new Thickness(0), CornerRadius = new CornerRadius(19),
            Background = Kit.B("SirajPrimaryBrush"), BorderThickness = new Thickness(0),
            UseSystemFocusVisuals = false,
        };
        _playBtn.Click += (_, _) =>
        {
            if (App.Media.HasMedia) App.Media.TogglePlayPause();
            else if (App.QuranPlayer.IsPlaying) App.QuranPlayer.Pause();
            else App.QuranPlayer.Resume();
        };
        Kit.Lift(_playBtn);
        _nextBtn = MiniIconBtn("skip_next", () =>
        {
            if (App.Media.HasMedia) App.Media.Next();
            else App.QuranPlayer.Next();
        });
        _closeBtn = MiniIconBtn("close", () =>
        {
            if (App.Media.HasMedia) App.Media.Stop();
            else App.QuranPlayer.Stop();
        });

        var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(12, 9, 12, 9) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(_artSlot);

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        var titleRow = new Grid { ColumnSpacing = 8 };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleRow.Children.Add(_title);
        _eq.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_eq, 1);
        titleRow.Children.Add(_eq);
        texts.Children.Add(titleRow);
        texts.Children.Add(_subtitle);

        var tapArea = new Button
        {
            Content = texts, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(2), UseSystemFocusVisuals = false,
        };
        tapArea.Click += (_, _) =>
        {
            if (App.Media.HasMedia) ExpandRequested?.Invoke();
            else if (App.QuranPlayer.Current is { } a && App.Quran.Status == QuranService.State.Ready)
                App.MainWindowInstance?.OpenReader(new Views.ReaderArgs(App.Quran.Database.PageOf(a.Surah, a.Ayah), a));
        };
        Grid.SetColumn(tapArea, 1);
        row.Children.Add(tapArea);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, FlowDirection = FlowDirection.LeftToRight };
        controls.Children.Add(_prevBtn);
        controls.Children.Add(_playBtn);
        controls.Children.Add(_nextBtn);
        controls.Children.Add(_closeBtn);
        Grid.SetColumn(controls, 2);
        row.Children.Add(controls);

        _progressFill = new Border
        {
            Height = 2.5, HorizontalAlignment = HorizontalAlignment.Left,
            Background = Kit.B("SirajPrimaryBrush"), CornerRadius = new CornerRadius(1.5), Width = 0,
        };
        _progressTrack = new Border
        {
            Height = 2.5, Margin = new Thickness(18, 0, 18, 0), CornerRadius = new CornerRadius(1.5),
            Background = Kit.B("SirajDividerBrush"), FlowDirection = FlowDirection.LeftToRight,
            Child = _progressFill, Visibility = Visibility.Collapsed,
        };
        _progressTrack.SizeChanged += (_, _) => UpdateProgress();

        var wrapper = new Grid();
        wrapper.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        wrapper.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        wrapper.Children.Add(row);
        Grid.SetRow(_progressTrack, 1);
        wrapper.Children.Add(_progressTrack);

        _shell = new Border
        {
            CornerRadius = new CornerRadius(24),
            Background = Kit.B("SirajGlassElevatedBrush"),
            BorderBrush = Kit.B("SirajGlassBorderBrush"),
            BorderThickness = new Thickness(1),
            Shadow = new ThemeShadow(),
            Translation = new Vector3(0, 0, 24),
            Child = wrapper,
            MinWidth = 440,
        };
        ElementCompositionPreview.SetIsTranslationEnabled(_shell, true);
        Content = _shell;

        Loaded += (_, _) =>
        {
            App.Media.Changed += Refresh;
            App.Media.PositionTick += UpdateProgress;
            App.QuranPlayer.Changed += Refresh;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            App.Media.Changed -= Refresh;
            App.Media.PositionTick -= UpdateProgress;
            App.QuranPlayer.Changed -= Refresh;
        };
    }

    public bool SuppressForReader { get; set; }

    public void Refresh()
    {
        var loc = Loc.Current;
        bool hasMedia = App.Media.HasMedia;
        bool hasQuran = !SuppressForReader && App.QuranPlayer.IsActive;
        bool show = hasMedia || hasQuran;

        if (!show)
        {
            if (_visible)
            {
                _visible = false;
                int token = ++_animToken;
                Kit.AnimateOut(_shell, 0, 14, 0.97f, 110, () =>
                {
                    if (_animToken == token && !_visible) Visibility = Visibility.Collapsed;
                });
            }
            return;
        }

        if (!_visible)
        {
            _visible = true;
            _animToken++;
            Visibility = Visibility.Visible;
            Kit.AnimateIn(_shell, 0, 14, 0.97f, 150);
        }

        if (hasMedia && App.Media.Kind == MediaKind.Radio && App.Media.CurrentStation is { } st)
        {
            _artSlot.Content = PortraitAvatar.Create(st.ArtworkFile, st.Name(loc.Language), 42, 12, activeRing: App.Media.IsPlaying);
            _title.Text = st.Name(loc.Language);
            _subtitle.Text = App.Media.IsBuffering ? loc["adhkar_radio_connecting"] : (st.HasPlace ? $"{loc["radio_live_badge"]}{loc.Sep}{st.Location(loc.Language)}" : loc["radio_live_badge"]);
            _eq.IsActive = App.Media.IsPlaying;
            _eq.Visibility = App.Media.IsPlaying ? Visibility.Visible : Visibility.Collapsed;
            _playIcon.IconKey = App.Media.IsPlaying ? "pause" : "play";
            _prevBtn.Visibility = Visibility.Collapsed;
            _nextBtn.Visibility = Visibility.Collapsed;
            _progressTrack.Visibility = Visibility.Collapsed;
        }
        else if (hasMedia && App.Media.CurrentTrack is { } tr)
        {
            _artSlot.Content = PortraitAvatar.Create(tr.Artwork, tr.Subtitle, 42, 21, activeRing: App.Media.IsPlaying);
            _title.Text = tr.Title;
            _subtitle.Text = App.Media.IsBuffering ? loc["adhkar_radio_connecting"] : tr.Subtitle;
            _eq.IsActive = App.Media.IsPlaying;
            _eq.Visibility = App.Media.IsPlaying ? Visibility.Visible : Visibility.Collapsed;
            _playIcon.IconKey = App.Media.IsPlaying ? "pause" : "play";
            _prevBtn.Visibility = Visibility.Visible;
            _nextBtn.Visibility = Visibility.Visible;
            _progressTrack.Visibility = Visibility.Visible;
            UpdateProgress();
        }
        else if (hasQuran && App.QuranPlayer.Current is { } ayah)
        {
            var reciter = Core.Quran.Reciters.Find(App.Settings.Current.ReciterId);
            string? art = Core.Audio.ListeningLibrary.PortraitFiles.TryGetValue(reciter.Id, out var f) ? $"Portraits/{f}" : null;
            _artSlot.Content = PortraitAvatar.Create(art, reciter.Name(loc.Language), 42, 21, activeRing: App.QuranPlayer.IsPlaying);
            _title.Text = $"{MediaHubService.SurahTitle(ayah.Surah, loc.Language)} {loc.Number(ayah.Surah)}:{loc.Number(ayah.Ayah)}";
            _subtitle.Text = reciter.Name(loc.Language);
            _eq.IsActive = App.QuranPlayer.IsPlaying;
            _eq.Visibility = App.QuranPlayer.IsPlaying ? Visibility.Visible : Visibility.Collapsed;
            _playIcon.IconKey = App.QuranPlayer.IsPlaying ? "pause" : "play";
            _prevBtn.Visibility = Visibility.Visible;
            _nextBtn.Visibility = Visibility.Visible;
            _progressTrack.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateProgress()
    {
        if (!_visible || _progressTrack.Visibility != Visibility.Visible || _progressTrack.ActualWidth <= 0) return;
        double frac = App.Media.Duration > TimeSpan.Zero
            ? Math.Clamp(App.Media.Position.TotalMilliseconds / App.Media.Duration.TotalMilliseconds, 0, 1)
            : 0;
        _progressFill.Width = _progressTrack.ActualWidth * frac;
    }

    private static Button MiniIconBtn(string iconKey, Action click)
    {
        var b = new Button
        {
            Content = Kit.Icon(iconKey, 16, Kit.B("SirajOnSurfaceBrush")),
            Width = 34, Height = 34, Padding = new Thickness(0), CornerRadius = new CornerRadius(17),
            Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0),
            UseSystemFocusVisuals = false,
        };
        b.Click += (_, _) => click();
        return b;
    }
}
