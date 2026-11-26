using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Audio;

namespace Siraj.App.Views;

public sealed partial class RadioPage : Page
{
    private RadioFilter _filter = RadioFilter.All;
    private string _query = "";
    private readonly StackPanel _listHost = new() { Spacing = 14 };

    public RadioPage()
    {
        InitializeComponent();
        var loc = Loc.Current;
        var (root, body) = Kit.Page(loc["radio_stations_title"], loc["radio_hero_subtitle"], 1140,
            back: () => App.MainWindowInstance?.Navigate("listen"));

        // Live badge + search bar + sliding filter pills (persistent so typing never loses focus)
        var liveHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        liveHeader.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 229, 57, 53)), VerticalAlignment = VerticalAlignment.Center });
        liveHeader.Children.Add(Kit.Pill(loc["radio_live_badge"], "SirajPrimarySoftBrush", "SirajPrimaryBrush"));
        body.Children.Add(liveHeader);

        body.Children.Add(Kit.Search(loc["radio_search_hint"], q => { _query = q; FillStations(); }));
        body.Children.Add(Ui.Segments<RadioFilter>(
        [
            (RadioFilter.All, loc["radio_filter_all"]),
            (RadioFilter.Broadcasts, loc["radio_filter_broadcasts"]),
            (RadioFilter.Reciters, loc["radio_filter_reciters"]),
            (RadioFilter.TafsirAndSunnah, loc["radio_filter_tafsir_sunnah"]),
        ], _filter, f => { _filter = f; FillStations(); }));
        body.Children.Add(_listHost);

        Content = root;
        Loaded += (_, _) =>
        {
            App.Media.Changed += FillStations;
            App.Settings.Changed += OnSettings;
            App.Theme.Changed += FillStations;
            FillStations();
        };
        Unloaded += (_, _) =>
        {
            App.Media.Changed -= FillStations;
            App.Settings.Changed -= OnSettings;
            App.Theme.Changed -= FillStations;
        };
    }

    private void OnSettings(Core.Settings.AppSettings old, Core.Settings.AppSettings updated)
    {
        if (old.PinnedRadioIds != updated.PinnedRadioIds) DispatcherQueue.TryEnqueue(FillStations);
    }

    private void FillStations()
    {
        if (!IsLoaded) return;
        _listHost.Children.Clear();
        var loc = Loc.Current;
        var pinnedIds = new HashSet<string>(App.Settings.Current.PinnedRadioIds ?? []);
        var filtered = RadioStations.All.Where(Matches).ToList();

        if (filtered.Count == 0)
        {
            _listHost.Children.Add(Kit.Card(new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    Kit.Text(loc["radio_no_results"], "SirajHeading"),
                    Kit.Text(loc["adhkar_radio_no_results_hint"], "SirajCaption"),
                },
            }));
            return;
        }

        var pinned = filtered.Where(s => pinnedIds.Contains(s.Id)).ToList();
        var unpinned = filtered.Where(s => !pinnedIds.Contains(s.Id)).ToList();

        if (pinned.Count > 0)
        {
            _listHost.Children.Add(Ui.SectionWithIcon(loc["radio_section_pinned"], glyph: "\uE718"));
            _listHost.Children.Add(Kit.Columns(330, 12, pinned.Select(s => StationCard(s, true)).ToList()));
        }

        if (_filter == RadioFilter.All)
        {
            var broadcasts = unpinned.Where(s => s.Kind == RadioKind.Broadcast).ToList();
            var channels = unpinned.Where(s => s.Kind == RadioKind.Channel).ToList();
            if (broadcasts.Count > 0)
            {
                _listHost.Children.Add(Ui.SectionWithIcon(loc["radio_section_broadcasts"], "crescent_star"));
                _listHost.Children.Add(Kit.Columns(330, 12, broadcasts.Select(s => StationCard(s, false)).ToList()));
            }
            if (channels.Count > 0)
            {
                _listHost.Children.Add(Ui.SectionWithIcon(loc["radio_section_channels"], "quick_listen"));
                _listHost.Children.Add(Kit.Columns(330, 12, channels.Select(s => StationCard(s, false)).ToList()));
            }
        }
        else if (unpinned.Count > 0)
        {
            _listHost.Children.Add(Kit.Columns(330, 12, unpinned.Select(s => StationCard(s, false)).ToList()));
        }
    }

    private UIElement StationCard(RadioStation station, bool pinned)
    {
        var loc = Loc.Current;
        bool active = App.Media.Kind == MediaKind.Radio && App.Media.CurrentStation?.Id == station.Id;
        bool playing = active && App.Media.IsPlaying;
        bool buffering = active && App.Media.IsBuffering;

        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(PortraitAvatar.Create(station.ArtworkFile ?? "Radio/radio_quran.png", station.Name(loc.Language), 56, 16, activeRing: playing));

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        var titleTb = Kit.Text(station.Name(loc.Language), "SirajHeading", 15, bold: true,
            brush: Kit.B(active ? "SirajPrimaryBrush" : "SirajOnSurfaceBrush"));
        titleTb.MaxLines = 1;
        titleRow.Children.Add(titleTb);
        if (playing) titleRow.Children.Add(new EqualizerBars(13) { IsActive = true });
        info.Children.Add(titleRow);

        var desc = Kit.Text(buffering ? loc["adhkar_radio_connecting"] : station.Description(loc.Language), "SirajCaption", 12);
        desc.MaxLines = 2;
        info.Children.Add(desc);

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2, 0, 0) };
        if (active)
            meta.Children.Add(Kit.Pill(loc["radio_live_now"], "SirajPrimaryBrush", "SirajOnPrimaryBrush"));
        if (station.HasPlace)
            meta.Children.Add(Kit.Pill(station.Location(loc.Language), "SirajGlassPillBrush", "SirajOnSurfaceVariantBrush"));
        if (meta.Children.Count > 0) info.Children.Add(meta);

        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        var rightBtns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

        var pinBtn = new Button
        {
            Content = Kit.Icon(pinned ? "\uE840" : "\uE718", 13, Kit.B(pinned ? "SirajGoldBrush" : "SirajOnSurfaceVariantBrush")),
            Width = 34, Height = 34, Padding = new Thickness(0), CornerRadius = new CornerRadius(17),
            Background = Kit.B(pinned ? "SirajGoldSoftBrush" : "SirajGlassPillBrush"), BorderThickness = new Thickness(0),
        };
        AutomationProperties.SetName(pinBtn, loc[pinned ? "radio_unpin" : "radio_pin"]);
        ToolTipService.SetToolTip(pinBtn, loc[pinned ? "radio_unpin" : "radio_pin"]);
        pinBtn.Click += (_, _) => TogglePin(station.Id);
        rightBtns.Children.Add(pinBtn);

        var playBtn = new Button
        {
            Content = Kit.Icon(playing ? "\uE769" : "\uE768", 14, Kit.B(active ? "SirajOnPrimaryBrush" : "SirajPrimaryBrush")),
            Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(20),
            Background = Kit.B(active ? "SirajPrimaryBrush" : "SirajPrimarySoftBrush"), BorderThickness = new Thickness(0),
        };
        AutomationProperties.SetName(playBtn, loc[playing ? "radio_stop" : "play"]);
        playBtn.Click += (_, _) => ToggleStation(station);
        rightBtns.Children.Add(playBtn);

        Grid.SetColumn(rightBtns, 2);
        grid.Children.Add(rightBtns);

        var card = Kit.CardButton(grid, () => ToggleStation(station), name: station.Name(loc.Language), padding: new Thickness(16, 14, 16, 14));
        if (active) card.BorderBrush = Kit.B("SirajPrimaryBrush");
        return card;
    }

    private static void ToggleStation(RadioStation station)
    {
        if (App.Media.Kind == MediaKind.Radio && App.Media.CurrentStation?.Id == station.Id && App.Media.IsPlaying)
            App.Media.Pause();
        else
            App.Media.PlayRadio(station);
    }

    private static void TogglePin(string id)
    {
        App.Settings.Update(s =>
        {
            var next = new List<string>(s.PinnedRadioIds ?? []);
            if (!next.Remove(id)) next.Insert(0, id);
            return s with { PinnedRadioIds = next };
        });
    }

    private bool Matches(RadioStation s)
    {
        if (!string.IsNullOrWhiteSpace(_query))
        {
            var q = _query.Trim();
            bool hit = s.NameAr.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                       s.NameEn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                       s.NameTr.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                       s.NameUr.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                       s.LocEn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                       s.LocAr.Contains(q, StringComparison.OrdinalIgnoreCase);
            if (!hit) return false;
        }
        return _filter switch
        {
            RadioFilter.Broadcasts => s.Kind == RadioKind.Broadcast && !s.IsTopical,
            RadioFilter.Reciters => s.Kind == RadioKind.Channel && !s.IsTopical,
            RadioFilter.TafsirAndSunnah => s.IsTopical,
            _ => true,
        };
    }
}
