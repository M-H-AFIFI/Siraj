using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Audio;
using Siraj.Core.Quran;

namespace Siraj.App.Views;

public sealed partial class ListenPage : Page
{
    private enum ListenFilter { All, Murattal, Mujawwad, Riwayah }

    private ListenFilter _filter = ListenFilter.All;
    private string _query = "";
    private string _surahQuery = "";
    private Reciter? _selectedReciter;
    private RareCollection? _selectedRare;
    private StackPanel? _hubListHost;
    private StackPanel? _surahListHost;
    private TextBlock? _ambientLabel;
    private Button? _ambientBtn;
    private string _builtStateKey = "";

    public ListenPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Loaded += (_, _) =>
        {
            App.Media.Changed += OnMediaChanged;
            App.Theme.Changed += OnThemeChanged;
            string stateKey = CurrentStateKey();
            if (Content == null || stateKey != _builtStateKey)
                RebuildView();
            else
                UpdateAmbientChip();
        };
        Unloaded += (_, _) =>
        {
            App.Media.Changed -= OnMediaChanged;
            App.Theme.Changed -= OnThemeChanged;
        };
    }

    private string CurrentStateKey() =>
        $"{App.Theme.Scheme.Primary}|{App.Theme.IsDark}|{App.Theme.IsGlass}|{App.Media.Kind}|{App.Media.CurrentTrack?.Id}|{App.Media.IsPlaying}|{App.Media.ActiveAmbientId}|{_selectedReciter?.Id}|{_selectedRare?.Id}";

    private void OnThemeChanged() { _builtStateKey = ""; RebuildView(); }

    private void OnMediaChanged()
    {
        if (!IsLoaded) return;
        _builtStateKey = CurrentStateKey();
        UpdateAmbientChip();
        if (_selectedReciter != null) FillReciterSurahs(_selectedReciter);
        else if (_selectedRare != null) ShowRareDetail(_selectedRare);
        else FillHubReciters();
    }

    private void RebuildView()
    {
        if (!IsLoaded) return;
        _builtStateKey = CurrentStateKey();
        if (_selectedReciter != null) ShowReciterDetail(_selectedReciter);
        else if (_selectedRare != null) ShowRareDetail(_selectedRare);
        else ShowHub();
    }

    private void UpdateAmbientChip()
    {
        if (_ambientLabel == null || _ambientBtn == null) return;
        var loc = Loc.Current;
        _ambientLabel.Text = App.Media.ActiveAmbientId is { } aid && ListeningLibrary.AmbientSounds.FirstOrDefault(a => a.Id == aid) is { } amb
            ? $"{loc["ambient_title"]}: {loc[amb.TitleKey]}"
            : loc["ambient_title"];
        _ambientBtn.BorderBrush = Kit.B(App.Media.ActiveAmbientId != null ? "SirajPrimaryBrush" : "SirajGlassBorderBrush");
    }

    private void ShowHub()
    {
        var loc = Loc.Current;
        _surahListHost = null;
        var (root, body) = Kit.Page(loc["listen_title"], loc["win_listen_subtitle"], 1140);

        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var ambientInner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        ambientInner.Children.Add(SirajVectorIcon.Make("ambient_fountain", 16, Kit.B("SirajPrimaryBrush")));
        _ambientLabel = Kit.Text(loc["ambient_title"], "SirajBody", 13.5, bold: true);
        ambientInner.Children.Add(_ambientLabel);
        _ambientBtn = new Button
        {
            Content = ambientInner, Style = (Style)Application.Current.Resources["SirajButton"],
            Flyout = FullPlayerOverlay.BuildAmbientFlyout(),
        };
        UpdateAmbientChip();
        actionsRow.Children.Add(_ambientBtn);

        var radioInner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        radioInner.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 229, 57, 53)), VerticalAlignment = VerticalAlignment.Center });
        radioInner.Children.Add(Kit.Text(loc["radio_stations_title"], "SirajBody", 13.5, bold: true));
        var radioBtn = new Button { Content = radioInner, Style = (Style)Application.Current.Resources["SirajButton"] };
        radioBtn.Click += (_, _) => App.MainWindowInstance?.Navigate("radio", drillIn: true);
        actionsRow.Children.Add(radioBtn);
        body.Children.Add(actionsRow);

        var searchBox = Kit.Search(loc["listen_search_hint"], q => { _query = q; FillHubReciters(); });
        searchBox.Text = _query;
        body.Children.Add(searchBox);

        body.Children.Add(Ui.Segments<ListenFilter>(
        [
            (ListenFilter.All, loc["listen_filter_all"]),
            (ListenFilter.Murattal, loc["listen_filter_murattal"]),
            (ListenFilter.Mujawwad, loc["listen_filter_mujawwad"]),
            (ListenFilter.Riwayah, $"{loc["listen_riwayah_warsh_short"]} / {loc["listen_riwayah_qalun_short"]}"),
        ], _filter, f => { _filter = f; FillHubReciters(); }));

        _hubListHost = new StackPanel { Spacing = 14 };
        body.Children.Add(_hubListHost);
        FillHubReciters();
        Content = root;
    }

    private void FillHubReciters()
    {
        if (_hubListHost == null) return;
        _hubListHost.Children.Clear();
        var loc = Loc.Current;

        if (_filter == ListenFilter.All)
        {
            var matchingRare = ListeningLibrary.RareCollections.Where(r =>
                string.IsNullOrWhiteSpace(_query) ||
                r.NameAr.Contains(_query, StringComparison.OrdinalIgnoreCase) ||
                r.NameEn.Contains(_query, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchingRare.Count > 0)
            {
                _hubListHost.Children.Add(Ui.SectionWithIcon(loc["listen_rare_title"], "library_music"));
                foreach (var rare in matchingRare)
                {
                    var grid = new Grid { ColumnSpacing = 16 };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    grid.Children.Add(PortraitAvatar.Create(rare.PortraitFile, rare.Name(loc.Language), 64, 18));

                    var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
                    var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    top.Children.Add(Kit.Text(rare.Name(loc.Language), "SirajHeading", 17, bold: true));
                    top.Children.Add(Kit.Pill(loc["listen_rare_badge"], "SirajGoldSoftBrush", "SirajGoldBrush"));
                    txt.Children.Add(top);
                    txt.Children.Add(Kit.Text(loc["listen_zaher_bio"], "SirajCaption", 12.5));
                    Grid.SetColumn(txt, 1);
                    grid.Children.Add(txt);

                    var playRare = new Button
                    {
                        Content = Kit.Icon("play", 20, Kit.B("SirajOnPrimaryBrush")),
                        Width = 44, Height = 44, Padding = new Thickness(0), CornerRadius = new CornerRadius(22),
                        Background = Kit.B("SirajPrimaryBrush"), BorderThickness = new Thickness(0),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    var col = rare;
                    playRare.Click += (_, _) => App.Media.PlayRare(col, 0);
                    Grid.SetColumn(playRare, 2);
                    grid.Children.Add(playRare);

                    _hubListHost.Children.Add(Kit.CardButton(grid, () => { _selectedRare = col; RebuildView(); },
                        name: rare.Name(loc.Language), padding: new Thickness(18, 16, 18, 16)));
                }
            }
        }

        var reciters = ListeningLibrary.AllReciters.Where(MatchesFilter).ToList();
        _hubListHost.Children.Add(Ui.SectionWithIcon($"{loc["listen_reciters_title"]} ({loc.Number(reciters.Count)})", "quick_listen"));

        if (reciters.Count == 0)
        {
            _hubListHost.Children.Add(Kit.Card(Kit.Text(loc["listen_no_results"], "SirajCaption")));
            return;
        }

        var cards = new List<UIElement>(reciters.Count);
        foreach (var reciter in reciters)
        {
            bool activeHere = App.Media.Kind == MediaKind.Listen && App.Media.CurrentTrack?.ReciterId == reciter.Id;
            bool playingHere = activeHere && App.Media.IsPlaying;
            string? art = ListeningLibrary.PortraitFiles.TryGetValue(reciter.Id, out var f) ? $"Portraits/{f}" : null;

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(PortraitAvatar.Create(art, reciter.Name(loc.Language), 50, 25, activeRing: playingHere));

            var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
            var nameRow = new Grid { ColumnSpacing = 6 };
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var nameTb = Kit.Text(reciter.Name(loc.Language), "SirajHeading", 14.5, bold: true);
            nameTb.MaxLines = 1;
            nameTb.TextWrapping = TextWrapping.NoWrap;
            nameTb.TextTrimming = TextTrimming.CharacterEllipsis;
            nameRow.Children.Add(nameTb);
            if (playingHere)
            {
                var eq = new EqualizerBars(12) { IsActive = true, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(eq, 1);
                nameRow.Children.Add(eq);
            }
            txt.Children.Add(nameRow);

            string badge = !string.IsNullOrEmpty(reciter.Riwayah) && !reciter.Riwayah.Contains("حفص")
                ? reciter.Riwayah
                : reciter.StyleName(loc.Language) ?? loc["listen_filter_murattal"];
            txt.Children.Add(Kit.Pill(badge, activeHere ? "SirajPrimarySoftBrush" : "SirajGlassPillBrush",
                activeHere ? "SirajPrimaryBrush" : "SirajOnSurfaceVariantBrush"));
            Grid.SetColumn(txt, 1);
            grid.Children.Add(txt);

            var play = new Button
            {
                Content = Kit.Icon(playingHere ? "pause" : "play", 16,
                    Kit.B(playingHere ? "SirajOnPrimaryBrush" : "SirajPrimaryBrush")),
                Width = 36, Height = 36, Padding = new Thickness(0), CornerRadius = new CornerRadius(18),
                Background = Kit.B(playingHere ? "SirajPrimaryBrush" : "SirajPrimarySoftBrush"),
                BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center,
            };
            var r = reciter;
            play.Click += (_, _) =>
            {
                if (activeHere) App.Media.TogglePlayPause();
                else App.Media.PlayReciterSurah(r, 1);
            };
            Grid.SetColumn(play, 2);
            grid.Children.Add(play);

            var card = Kit.CardButton(grid, () => { _selectedReciter = r; _surahQuery = ""; RebuildView(); },
                name: reciter.Name(loc.Language), padding: new Thickness(14, 12, 14, 12));
            if (activeHere) card.BorderBrush = Kit.B("SirajPrimaryBrush");
            cards.Add(card);
        }
        _hubListHost.Children.Add(Kit.Columns(310, 12, cards));
    }

    private void ShowReciterDetail(Reciter reciter)
    {
        var loc = Loc.Current;
        _hubListHost = null;
        var (root, body) = Kit.Page(reciter.Name(loc.Language), reciter.Riwayah ?? loc["listen_riwayah"], 980,
            back: () => { _selectedReciter = null; RebuildView(); });

        string? art = ListeningLibrary.PortraitFiles.TryGetValue(reciter.Id, out var f) ? $"Portraits/{f}" : null;
        bool playingReciter = App.Media.Kind == MediaKind.Listen && App.Media.CurrentTrack?.ReciterId == reciter.Id && App.Media.IsPlaying;

        var hero = new Grid { ColumnSpacing = 20 };
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        hero.Children.Add(PortraitAvatar.Create(art, reciter.Name(loc.Language), 96, 26, activeRing: playingReciter));

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 8 };
        info.Children.Add(Kit.Text(reciter.Name(loc.Language), "SirajTitle", 24, bold: true));
        var pills = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (reciter.StyleName(loc.Language) is { } style)
            pills.Children.Add(Kit.Pill(style, "SirajPrimarySoftBrush", "SirajPrimaryBrush"));
        pills.Children.Add(Kit.Pill(reciter.Riwayah ?? loc["listen_riwayah"], "SirajGlassPillBrush", "SirajOnSurfaceVariantBrush"));
        info.Children.Add(pills);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 4, 0, 0) };
        btns.Children.Add(Ui.Button(loc["play"], () => App.Media.PlayReciterSurah(reciter, 1), primary: true));
        btns.Children.Add(Ui.Button(loc["listen_shuffle"], () =>
        {
            if (!App.Media.Shuffle) App.Media.ToggleShuffle();
            App.Media.PlayReciterSurah(reciter, Random.Shared.Next(1, 115));
        }));
        info.Children.Add(btns);
        Grid.SetColumn(info, 1);
        hero.Children.Add(info);
        body.Children.Add(Kit.Card(hero, new Thickness(24)));

        var search = Kit.Search(loc["listen_search_surah_hint"], q => { _surahQuery = q; FillReciterSurahs(reciter); });
        search.Text = _surahQuery;
        body.Children.Add(search);

        _surahListHost = new StackPanel { Spacing = 8 };
        body.Children.Add(_surahListHost);
        FillReciterSurahs(reciter);
        Content = root;
    }

    private void FillReciterSurahs(Reciter reciter)
    {
        if (_surahListHost == null) return;
        _surahListHost.Children.Clear();
        var loc = Loc.Current;
        bool ready = App.Quran.Status == QuranService.State.Ready;

        for (int n = 1; n <= 114; n++)
        {
            string title = MediaHubService.SurahTitle(n, loc.Language);
            string secondary = ready && !loc.IsRtl ? App.Quran.Database.Surah(n).Name : "";
            if (!string.IsNullOrWhiteSpace(_surahQuery))
            {
                bool match = title.Contains(_surahQuery, StringComparison.OrdinalIgnoreCase) ||
                             secondary.Contains(_surahQuery, StringComparison.OrdinalIgnoreCase) ||
                             n.ToString().Contains(_surahQuery.Trim());
                if (!match) continue;
            }

            bool isThis = App.Media.Kind == MediaKind.Listen &&
                          App.Media.CurrentTrack?.ReciterId == reciter.Id &&
                          App.Media.CurrentTrack?.SurahNumber == n;
            bool playingThis = isThis && App.Media.IsPlaying;

            var row = new Grid { ColumnSpacing = 14 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var num = new Border
            {
                Width = 36, Height = 36, CornerRadius = new CornerRadius(11),
                Background = Kit.B(isThis ? "SirajPrimaryBrush" : "SirajPrimarySoftBrush"),
                Child = Kit.Text(loc.Number(n), "SirajCaption", 12.5, bold: true,
                    brush: Kit.B(isThis ? "SirajOnPrimaryBrush" : "SirajPrimaryBrush")),
            };
            ((TextBlock)num.Child).HorizontalAlignment = HorizontalAlignment.Center;
            ((TextBlock)num.Child).VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(num);

            var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
            txt.Children.Add(Kit.Text(title, "SirajHeading", 15, bold: true,
                brush: Kit.B(isThis ? "SirajPrimaryBrush" : "SirajOnSurfaceBrush")));
            if (!string.IsNullOrEmpty(secondary)) txt.Children.Add(Kit.Text(secondary, "SirajCaption", 12.5));
            Grid.SetColumn(txt, 1);
            row.Children.Add(txt);

            var trailing = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
            if (playingThis) trailing.Children.Add(new EqualizerBars(14) { IsActive = true });
            trailing.Children.Add(Kit.Icon(playingThis ? "pause" : "play", 17, Kit.B("SirajPrimaryBrush")));
            Grid.SetColumn(trailing, 2);
            row.Children.Add(trailing);

            int surahNum = n;
            var card = Kit.CardButton(row, () =>
            {
                if (isThis) App.Media.TogglePlayPause();
                else App.Media.PlayReciterSurah(reciter, surahNum);
            }, name: title, padding: new Thickness(14, 10, 16, 10));
            if (isThis) card.BorderBrush = Kit.B("SirajPrimaryBrush");
            _surahListHost.Children.Add(card);
        }
    }

    private void ShowRareDetail(RareCollection collection)
    {
        var loc = Loc.Current;
        _hubListHost = null;
        _surahListHost = null;
        var (root, body) = Kit.Page(collection.Name(loc.Language), loc["listen_zaher_bio"], 980,
            back: () => { _selectedRare = null; RebuildView(); });

        var hero = new Grid { ColumnSpacing = 20 };
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hero.Children.Add(PortraitAvatar.Create(collection.PortraitFile, collection.Name(loc.Language), 96, 26));
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 8 };
        info.Children.Add(Kit.Pill(loc["listen_rare_label"], "SirajGoldSoftBrush", "SirajGoldBrush"));
        info.Children.Add(Kit.Text(loc["listen_zaher_bio"], "SirajBody"));
        var playAll = Ui.Button(loc["play"], () => App.Media.PlayRare(collection, 0), primary: true);
        playAll.HorizontalAlignment = HorizontalAlignment.Left;
        info.Children.Add(playAll);
        Grid.SetColumn(info, 1);
        hero.Children.Add(info);
        body.Children.Add(Kit.Card(hero, new Thickness(24)));

        var list = new StackPanel { Spacing = 8 };
        for (int i = 0; i < collection.Tracks.Count; i++)
        {
            var tr = collection.Tracks[i];
            string id = $"{collection.Id}:{i}";
            bool isThis = App.Media.Kind == MediaKind.Listen && App.Media.CurrentTrack?.Id == id;
            bool playingThis = isThis && App.Media.IsPlaying;
            string title = string.Join(" · ", tr.Surahs.Select(s => MediaHubService.SurahTitle(s, loc.Language)));
            string dur = loc.Format("listen_duration_m", loc.Number(Math.Max(1, tr.DurationSec / 60)));

            var row = new Grid { ColumnSpacing = 14 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var txt = new StackPanel { Spacing = 3 };
            txt.Children.Add(Kit.Text(title, "SirajHeading", 15.5, bold: true,
                brush: Kit.B(isThis ? "SirajPrimaryBrush" : "SirajOnSurfaceBrush")));
            var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            meta.Children.Add(Kit.Text(dur, "SirajCaption", 12));
            if (tr.WholeSurah) meta.Children.Add(Kit.Pill(loc["listen_rare_whole_badge"], "SirajPrimarySoftBrush", "SirajPrimaryBrush"));
            txt.Children.Add(meta);
            row.Children.Add(txt);

            var trailing = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
            if (playingThis) trailing.Children.Add(new EqualizerBars(14) { IsActive = true });
            trailing.Children.Add(Kit.Icon(playingThis ? "pause" : "play", 18, Kit.B("SirajPrimaryBrush")));
            Grid.SetColumn(trailing, 1);
            row.Children.Add(trailing);

            int idx = i;
            var btn = Kit.CardButton(row, () =>
            {
                if (isThis) App.Media.TogglePlayPause();
                else App.Media.PlayRare(collection, idx);
            }, name: title, padding: new Thickness(18, 12, 18, 12));
            if (isThis) btn.BorderBrush = Kit.B("SirajPrimaryBrush");
            list.Children.Add(btn);
        }
        body.Children.Add(list);
        Content = root;
    }

    private bool MatchesFilter(Reciter r)
    {
        if (!string.IsNullOrWhiteSpace(_query))
        {
            var q = _query.Trim();
            if (!r.NameAr.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !r.NameEn.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !(r.NameTr?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) &&
                !(r.NameUr?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                return false;
        }
        return _filter switch
        {
            ListenFilter.Murattal => string.Equals(r.Style, "Murattal", StringComparison.OrdinalIgnoreCase) && (r.Riwayah == null || r.Riwayah.Contains("حفص")),
            ListenFilter.Mujawwad => string.Equals(r.Style, "Mujawwad", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Style, "Muallim", StringComparison.OrdinalIgnoreCase),
            ListenFilter.Riwayah => r.Riwayah != null && !r.Riwayah.Contains("حفص"),
            _ => true,
        };
    }
}
