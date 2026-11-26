using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.App.Theme;
using Siraj.Core.Adhan;
using Siraj.Core.Audio;
using Siraj.Core.Hijri;
using Siraj.Core.Places;
using Siraj.Core.Prayer;
using Siraj.Core.Settings;
using Siraj.Core.Theme;

namespace Siraj.App.Views;

public sealed partial class SettingsPage : Page
{
    private TextBlock? _placeText;
    private TextBlock? _locateStatus;
    private TextBlock? _quickPlaceSub;
    private ContentControl? _swatches;
    private ContentControl? _sceneGridSlot;
    private ContentControl? _previewSlot;
    private ContentControl? _voicePortraitSlot;
    private TextBlock? _voiceStatus;
    private Button? _listen;
    private UIElement? _locationAnchor;
    private ContentControl? _updateStatusSlot;

    public SettingsPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Loaded += (_, _) =>
        {
            App.Settings.Changed += OnSettingsChanged;
            App.Adhan.PlayingChanged += OnPlaying;
            VoiceDownloads.Changed += UpdateVoiceStatus;
            App.Updates.StateChanged += RefreshUpdateCard;
            RefreshUpdateCard();
        };
        Unloaded += (_, _) =>
        {
            App.Settings.Changed -= OnSettingsChanged;
            App.Adhan.PlayingChanged -= OnPlaying;
            VoiceDownloads.Changed -= UpdateVoiceStatus;
            App.Updates.StateChanged -= RefreshUpdateCard;
        };
        Build();
    }

    private static AppSettings S => App.Settings.Current;
    private static void Set(Func<AppSettings, AppSettings> change) => App.Settings.Update(change);

    private void OnSettingsChanged(AppSettings old, AppSettings updated) => DispatcherQueue.TryEnqueue(() =>
    {
        if (old.Location != updated.Location) UpdatePlace();
        if (old.Palette != updated.Palette) { BuildSwatches(); BuildPreviewCard(); BuildSceneGrid(); }
        if (old.LiveScene != updated.LiveScene || old.LiveBackground != updated.LiveBackground ||
            old.LiveIntensity != updated.LiveIntensity || old.LiveSkySync != updated.LiveSkySync ||
            old.GlassUi != updated.GlassUi || old.GlassFinish != updated.GlassFinish ||
            old.ThemeMode != updated.ThemeMode || old.BlackMode != updated.BlackMode)
        {
            BuildPreviewCard();
            BuildSceneGrid();
        }
        if (old.AdhanVoice != updated.AdhanVoice || old.FajrAdhan != updated.FajrAdhan)
        {
            UpdateVoiceStatus();
            UpdateVoicePortrait();
        }
        if (old.CalculationMethod != updated.CalculationMethod || old.Madhab != updated.Madhab || old.HijriMethod != updated.HijriMethod)
            Build();
    });

    private void Build()
    {
        var loc = Loc.Current;
        Body.Children.Clear();

        var title = Ui.Text(loc["settings_title"], "SirajTitle");
        title.Margin = new Thickness(4, 0, 4, 12);
        Body.Children.Add(title);

        Body.Children.Add(BuildIdentityHeader());

        Body.Children.Add(Ui.SectionWithIcon(loc["live_theme_appearance_title"], "palette"));

        _previewSlot = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 12) };
        BuildPreviewCard();
        Body.Children.Add(_previewSlot);

        _swatches = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        BuildSwatches();

        _sceneGridSlot = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        BuildSceneGrid();

        Body.Children.Add(Ui.Card(
            Ui.Row(loc["live_theme_classic_palettes_title"], loc["live_theme_classic_palettes_desc"], _swatches, stack: true),
            Ui.Row(loc["live_theme_mode_title"], loc["win_dark_mode_desc"],
                Ui.Segments<string>([("System", loc["live_theme_mode_system"]), ("Light", loc["live_theme_mode_light"]), ("Dark", loc["live_theme_mode_dark"])],
                    S.ThemeMode, v => Set(s => s with { ThemeMode = v }))),
            Ui.Row(loc["live_theme_amoled_title"], loc["live_theme_amoled_desc"],
                Ui.Toggle(S.BlackMode, v => Set(s => s with { BlackMode = v }))),
            Ui.Row(loc["appearance_glass_ui"], loc["appearance_glass_ui_desc"],
                Ui.Toggle(S.GlassUi, v => Set(s => s with { GlassUi = v }))),
            Ui.Row(loc["live_theme_glass_style_title"], loc["appearance_glass_mesh_desc"],
                Ui.Segments<string>(
                [
                    ("Clear", loc["live_theme_glass_style_clear"]),
                    ("Satin", loc["live_theme_glass_style_satin"]),
                    ("Frosted", loc["live_theme_glass_style_frosted"]),
                ], S.GlassFinish, v => Set(s => s with { GlassFinish = v }))),
            Ui.Row(loc["live_theme_live_bg_title"], loc["live_theme_live_bg_desc"],
                Ui.Toggle(S.LiveBackground, v => Set(s => s with { LiveBackground = v }))),
            Ui.Row(loc["live_theme_section_title"], loc["live_theme_stage_tap_hint"], _sceneGridSlot, stack: true),
            Ui.Row(loc["live_theme_intensity_title"], loc["live_theme_intensity_desc"],
                Ui.Segments<string>(
                [
                    ("Subtle", loc["live_theme_intensity_subtle"]),
                    ("Balanced", loc["live_theme_intensity_balanced"]),
                    ("Vivid", loc["live_theme_intensity_vivid"]),
                ], S.LiveIntensity, v => Set(s => s with { LiveIntensity = v }))),
            Ui.Row(loc["live_theme_sky_sync_title"], loc["live_theme_sky_sync_desc"],
                Ui.Toggle(S.LiveSkySync, v => Set(s => s with { LiveSkySync = v }))),
            Ui.Row(loc["settings_reduce_motion"], loc["win_motion_desc"],
                Ui.Toggle(S.ReduceMotion, v => Set(s => s with { ReduceMotion = v })))));

        var locSec = Ui.SectionWithIcon(loc["settings_location"], "location_on");
        _locationAnchor = locSec;
        Body.Children.Add(locSec);
        _placeText = Ui.Text("", "SirajHeading");
        _locateStatus = Ui.Text("", "SirajCaption");
        var search = new AutoSuggestBox
        {
            PlaceholderText = loc["settings_search_city_hint"],
            QueryIcon = SirajVectorIcon.MakePathIcon("search"),
            MinWidth = 280,
        };
        search.TextChanged += (box, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            box.ItemsSource = string.IsNullOrWhiteSpace(box.Text) ? null
                : Cities.Search(box.Text).Take(12).Select(c => new CityItem(c)).ToList();
        };
        search.SuggestionChosen += (box, args) =>
        {
            if (args.SelectedItem is CityItem item)
            {
                LocationService.Use(Place.FromCity(item.City), manual: true, recommendMethod: true);
                box.Text = "";
                box.ItemsSource = null;
            }
        };
        search.QuerySubmitted += (box, args) =>
        {
            var city = args.ChosenSuggestion is CityItem chosen ? chosen.City : Cities.Search(args.QueryText).FirstOrDefault();
            if (city == null) return;
            LocationService.Use(Place.FromCity(city), manual: true, recommendMethod: true);
            box.Text = "";
            box.ItemsSource = null;
        };
        var detect = Ui.Button(loc["win_location_detect"], async () => await Detect());
        var placeRow = new StackPanel { Spacing = 4 };
        placeRow.Children.Add(_placeText);
        placeRow.Children.Add(_locateStatus);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(search);
        actions.Children.Add(detect);
        placeRow.Children.Add(actions);
        var placeCard = new Grid { Padding = new Thickness(20, 16, 20, 16) };
        placeCard.Children.Add(placeRow);
        Body.Children.Add(Ui.Card(placeCard));
        UpdatePlace();

        Body.Children.Add(Ui.SectionWithIcon(loc["settings_prayer_calculation"], "nav_prayer"));
        var methods = CalculationMethods.All.Select(m => (m.Id, m.Name(loc.Language))).ToList();
        var method = Ui.Choice<string>(methods, CalculationMethods.CanonicalId(S.CalculationMethod), v => Set(s => s with { CalculationMethod = v }), 300);
        var methodInfo = CalculationMethods.ById(S.CalculationMethod);
        var rows = new List<UIElement>
        {
            Ui.Row(loc["settings_calc_method_title"],
                methodInfo != null ? loc.Format("settings_calc_used_in", methodInfo.Where(loc.Language)) : loc["settings_calc_method_desc"], method),
            Ui.Row(loc["settings_calc_madhab_title"], loc["settings_calc_madhab_desc"],
                Ui.Segments<string>([("Shafi", loc["settings_asr_standard"]), ("Hanafi", loc["settings_asr_hanafi"])], S.Madhab,
                    v => Set(s => s with { Madhab = v }))),
        };
        if (CalculationMethods.NeedsHighLatitudeRule(S.Location?.Latitude))
        {
            rows.Add(Ui.Row(loc["settings_calc_high_latitude_title"], loc["settings_calc_high_latitude_desc"],
                Ui.Choice<string>([("MiddleOfTheNight", loc["high_latitude_middle_of_the_night"]),
                    ("SeventhOfTheNight", loc["high_latitude_one_seventh"]), ("TwilightAngle", loc["high_latitude_angle_based"])],
                    S.HighLatitudeRule, v => Set(s => s with { HighLatitudeRule = v }))));
        }
        rows.Add(BuildOffsets());
        Body.Children.Add(Ui.Card([.. rows]));

        Body.Children.Add(Ui.SectionWithIcon(loc["settings_reminders"], "notifications"));
        Body.Children.Add(Ui.Card(BuildAlerts().ToArray()));

        Body.Children.Add(Ui.SectionWithIcon(loc["hijri_setting_title"], "hijri_calendar"));
        var country = S.Location?.Country;
        var region = HijriRegion.ForCountry(country);
        string source = loc[region.Method switch
        {
            HijriMethod.Egypt => "hijri_source_egypt",
            HijriMethod.Turkey => "hijri_source_turkey",
            HijriMethod.Mabims => "hijri_source_mabims",
            HijriMethod.Sighting or HijriMethod.NakedEye => "hijri_source_sighting",
            _ => "hijri_source_umm_al_qura"
        }];
        Body.Children.Add(Ui.Card(
            Ui.Row(loc["hijri_calendar_heading"], S.HijriMethod == HijriCalendars.MethodUmmAlQura
                    ? loc["hijri_mode_umm_al_qura_body"] : loc.Format("hijri_mode_auto_body", source),
                Ui.Segments<string>([(HijriCalendars.MethodAuto, loc["hijri_mode_auto"]), (HijriCalendars.MethodUmmAlQura, loc["hijri_mode_umm_al_qura"])],
                    S.HijriMethod, v => Set(s => s with { HijriMethod = v }))),
            Ui.Row(loc["hijri_adjust_title"], loc["hijri_adjust_body"],
                Ui.Choice<int>([(-2, loc["hijri_adjust_minus_two"]), (-1, loc["hijri_adjust_minus_one"]), (0, loc["hijri_adjust_none"]),
                    (1, loc["hijri_adjust_plus_one"]), (2, loc["hijri_adjust_plus_two"])], S.HijriAdjustment,
                    v => Set(s => s with { HijriAdjustment = v }), 200))));

        Body.Children.Add(Ui.SectionWithIcon(loc["nav_quran"], "nav_quran"));
        Body.Children.Add(Ui.Card(
            Ui.Row(loc["settings_show_last_bookmark"], loc["settings_show_last_bookmark_desc"],
                Ui.Toggle(S.ShowContinueReadingOnHome, v => Set(s => s with { ShowContinueReadingOnHome = v }))),
            Ui.Row(loc["win_two_pages"], null,
                Ui.Toggle(S.QuranSpread, v => Set(s => s with { QuranSpread = v }))),
            Ui.Row(loc["win_continuous"], null,
                Ui.Toggle(S.QuranContinuous, v => Set(s => s with { QuranContinuous = v })))));

        Body.Children.Add(Ui.SectionWithIcon(loc["settings_section_language"], "translate"));
        Body.Children.Add(Ui.Card(
            Ui.Row(loc["settings_language"], null,
                Ui.Choice<string>([("en", "English"), ("ar", "العربية"), ("tr", "Türkçe"), ("ur", "اردو")], S.Language,
                    v => Set(s => s with { Language = v }), 200)),
            Ui.Row(loc["settings_numeral_system"], loc["settings_numeral_desc"],
                Ui.Segments<string>([("Auto", loc["settings_numeral_auto"]), ("Arabic", "١٢٣"), ("Western", "123")], S.NumeralSystem,
                    v => Set(s => s with { NumeralSystem = v }))),
            Ui.Row(loc["settings_hour_format"], null,
                Ui.Segments<string>([("System", loc["settings_hour_system"]), ("12", loc["settings_hour_12_sample"]), ("24", loc["settings_hour_24_sample"])],
                    S.HourFormat, v => Set(s => s with { HourFormat = v })))));

        Body.Children.Add(Ui.SectionWithIcon(loc["win_settings_windows"], "desktop_windows"));
        Body.Children.Add(Ui.Card(
            Ui.Row(loc["win_start_with_windows"], loc["win_start_with_windows_desc"],
                Ui.Toggle(S.StartWithWindows, v => Set(s => s with { StartWithWindows = v }))),
            Ui.Row(loc["win_close_to_tray"], loc["win_close_to_tray_desc"],
                Ui.Toggle(S.CloseToTray, v => Set(s => s with { CloseToTray = v }))),
            Ui.Row(loc["win_nav_widgets"], null,
                Ui.Button(loc["win_nav_widgets"], () => App.MainWindowInstance?.Navigate("widgets")))));

        Body.Children.Add(Ui.SectionWithIcon(loc["win_update_section"], "system_update"));
        _updateStatusSlot = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        RefreshUpdateCard();
        Body.Children.Add(Ui.Card(
            Ui.Row(loc["win_update_auto"], loc["win_update_auto_desc"],
                Ui.Toggle(S.AutoCheckUpdates, v => Set(s => s with { AutoCheckUpdates = v }))),
            _updateStatusSlot));

        Body.Children.Add(Ui.SectionWithIcon(loc["settings_support_header"], "info"));
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        Body.Children.Add(Ui.Card(
            Ui.Row(loc["win_about_windows"], loc.Format("about_version", loc.Digitize(version)), null),
            Ui.Row(loc["win_privacy_note"], null, null),
            Ui.Row(loc["win_open_logs"], null, Ui.Button(loc["win_open_logs"], () =>
            {
                Directory.CreateDirectory(AppPaths.Logs);
                _ = Windows.System.Launcher.LaunchFolderPathAsync(AppPaths.Logs);
            }))));
    }

    private void RefreshUpdateCard()
    {
        if (_updateStatusSlot == null || App.Updates == null) return;
        var loc = Loc.Current;
        var snap = App.Updates.Snapshot;
        string currentVer = $"v{loc.Digitize(App.Updates.CurrentVersion.ToString())}";
        string arch = App.Updates.Architecture;

        var container = new StackPanel { Padding = new Thickness(20, 14, 20, 14), Spacing = 10 };
        var topRow = new Grid { ColumnSpacing = 16 };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };

        switch (snap.State)
        {
            case UpdateState.Checking:
                left.Children.Add(Ui.Text(loc["win_update_checking"]));
                left.Children.Add(Ui.Text($"{currentVer} · {arch}", "SirajCaption"));
                actions.Children.Add(new ProgressRing { Width = 20, Height = 20, IsActive = true });
                break;

            case UpdateState.UpToDate:
                left.Children.Add(Ui.Text(loc.Format("win_update_up_to_date", currentVer)));
                left.Children.Add(Ui.Text($"{currentVer} · {arch}", "SirajCaption"));
                actions.Children.Add(Ui.Button(loc["win_update_check"], () => _ = App.Updates.CheckAsync(manual: true)));
                break;

            case UpdateState.Available when snap.Release is { } rel:
            {
                string nextVer = $"v{loc.Digitize(rel.Version.ToString())}";
                left.Children.Add(Ui.Text(loc.Format("win_update_available_title", nextVer)));
                left.Children.Add(Ui.Text(loc["win_update_available_body"], "SirajCaption"));
                actions.Children.Add(Ui.Button(loc["win_update_now"], () => _ = App.Updates.DownloadAndInstallAsync(), primary: true));
                break;
            }

            case UpdateState.Downloading when snap.Release is { } rel:
            {
                string nextVer = $"v{loc.Digitize(rel.Version.ToString())}";
                string pct = snap.TotalBytes > 0 ? loc.Digitize($"{(int)Math.Round(snap.Progress * 100)}%") : "";
                left.Children.Add(Ui.Text(loc.Format("win_update_downloading", nextVer, pct)));
                if (snap.TotalBytes > 0)
                {
                    double dlMb = snap.DownloadedBytes / (1024.0 * 1024.0);
                    double totMb = snap.TotalBytes / (1024.0 * 1024.0);
                    left.Children.Add(Ui.Text(loc.Digitize($"{dlMb:0.0} / {totMb:0.0} MB"), "SirajCaption"));
                }
                actions.Children.Add(Ui.Button(loc["win_update_cancel"], () => App.Updates.CancelDownload()));
                break;
            }

            case UpdateState.Verifying:
                left.Children.Add(Ui.Text(loc["win_update_verifying"]));
                actions.Children.Add(new ProgressRing { Width = 20, Height = 20, IsActive = true });
                break;

            case UpdateState.ReadyToInstall when snap.Release is { } rel:
            {
                string nextVer = $"v{loc.Digitize(rel.Version.ToString())}";
                left.Children.Add(Ui.Text(loc.Format("win_update_ready_title", nextVer)));
                left.Children.Add(Ui.Text(loc["win_update_available_body"], "SirajCaption"));
                actions.Children.Add(Ui.Button(loc["win_update_install_restart"], () => App.Updates.InstallNow(), primary: true));
                break;
            }

            case UpdateState.Installing:
                left.Children.Add(Ui.Text(loc["win_update_installing"]));
                actions.Children.Add(new ProgressRing { Width = 20, Height = 20, IsActive = true });
                break;

            case UpdateState.Failed:
                left.Children.Add(Ui.Text(loc[snap.ErrorKey ?? "win_update_failed"]));
                left.Children.Add(Ui.Text($"{currentVer} · {arch}", "SirajCaption"));
                if (snap.Release != null)
                    actions.Children.Add(Ui.Button(loc["win_update_now"], () => _ = App.Updates.DownloadAndInstallAsync(), primary: true));
                actions.Children.Add(Ui.Button(loc["win_update_check"], () => _ = App.Updates.CheckAsync(manual: true)));
                break;

            default:
                left.Children.Add(Ui.Text(snap.JustUpdated
                    ? loc.Format("win_update_completed", currentVer)
                    : loc.Format("about_version", loc.Digitize(App.Updates.CurrentVersion.ToString()))));
                left.Children.Add(Ui.Text($"{currentVer} · {arch}", "SirajCaption"));
                actions.Children.Add(Ui.Button(loc["win_update_check"], () => _ = App.Updates.CheckAsync(manual: true)));
                break;
        }

        topRow.Children.Add(left);
        Grid.SetColumn(actions, 1);
        topRow.Children.Add(actions);
        container.Children.Add(topRow);

        if (snap.State == UpdateState.Downloading)
        {
            container.Children.Add(new ProgressBar
            {
                Minimum = 0, Maximum = 100, Value = snap.Progress * 100,
                IsIndeterminate = snap.TotalBytes <= 0,
            });
        }
        else if (snap.State == UpdateState.Verifying)
        {
            container.Children.Add(new ProgressBar { IsIndeterminate = true });
        }
        else if (snap.State is UpdateState.Available or UpdateState.ReadyToInstall
            && snap.Release is { Notes.Length: > 0 } relWithNotes)
        {
            var notesTb = Ui.Text(relWithNotes.Notes, "SirajCaption");
            notesTb.TextWrapping = TextWrapping.Wrap;
            notesTb.MaxLines = 6;
            notesTb.TextTrimming = TextTrimming.CharacterEllipsis;
            container.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 10, 12, 10),
                Background = Kit.B("SirajGlassPillBrush"),
                BorderBrush = Kit.B("SirajDividerBrush"),
                BorderThickness = new Thickness(1),
                Child = notesTb,
            });
        }

        _updateStatusSlot.Content = container;
    }

    private UIElement BuildIdentityHeader()
    {
        var loc = Loc.Current;
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        var stack = new StackPanel { Spacing = 16 };
        var top = new Grid { ColumnSpacing = 16 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var emblem = new Border
        {
            Width = 54, Height = 54, CornerRadius = new CornerRadius(17),
            Background = Kit.B("SirajPrimaryBrush"),
            Child = SirajVectorIcon.Make("emblem", 30, Kit.B("SirajOnPrimaryBrush")),
        };
        top.Children.Add(emblem);

        var brand = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        brand.Children.Add(Kit.Text(loc["app_name"], "SirajHeading", 22, bold: true));
        brand.Children.Add(Kit.Arabic("وَسِرَاجًا مُّنِيرًا", 17, Kit.B("SirajPrimaryBrush"),
            loc.IsRtl ? TextAlignment.Right : TextAlignment.Left));
        Grid.SetColumn(brand, 1);
        top.Children.Add(brand);

        var verPill = Kit.Pill($"v{loc.Digitize(version)}", "SirajPrimarySoftBrush", "SirajPrimaryBrush");
        verPill.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(verPill, 2);
        top.Children.Add(verPill);
        stack.Children.Add(top);

        var tiles = new Grid { ColumnSpacing = 10 };
        for (int i = 0; i < 3; i++) tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        string cityLabel = S.Location?.DisplayName(loc.Language) ?? loc["settings_not_set"];
        _quickPlaceSub = Kit.Text(cityLabel, "SirajCaption", 12);
        _quickPlaceSub.MaxLines = 1;
        tiles.Children.Add(QuickTile("location_on", loc["settings_location"], _quickPlaceSub, () =>
        {
            if (_locationAnchor is FrameworkElement fe) fe.StartBringIntoView();
        }));

        string modeLabel = S.ThemeMode switch
        {
            "Light" => loc["live_theme_mode_light"],
            "Dark" => loc["live_theme_mode_dark"],
            _ => loc["live_theme_mode_system"],
        };
        string modeIcon = S.ThemeMode switch
        {
            "Light" => "light_mode",
            "Dark" => "dark_mode",
            _ => "brightness_auto",
        };
        var modeTile = QuickTile(modeIcon, loc["live_theme_mode_title"], Kit.Text(modeLabel, "SirajCaption", 12), () =>
        {
            string next = S.ThemeMode switch { "System" => "Light", "Light" => "Dark", _ => "System" };
            Set(s => s with { ThemeMode = next });
        });
        Grid.SetColumn(modeTile, 1);
        tiles.Children.Add(modeTile);

        string langLabel = S.Language switch { "ar" => "العربية", "tr" => "Türkçe", "ur" => "اردو", _ => "English" };
        var langTile = QuickTile("translate", loc["settings_language"], Kit.Text(langLabel, "SirajCaption", 12), () =>
        {
            string[] langs = ["en", "ar", "tr", "ur"];
            int nextIdx = (Array.IndexOf(langs, S.Language) + 1) % langs.Length;
            Set(s => s with { Language = langs[nextIdx] });
        });
        Grid.SetColumn(langTile, 2);
        tiles.Children.Add(langTile);

        stack.Children.Add(tiles);
        return Kit.Card(stack, new Thickness(20, 18, 20, 18));
    }

    private static Button QuickTile(string iconKey, string title, TextBlock sub, Action click)
    {
        var inner = new StackPanel { Spacing = 4 };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        row.Children.Add(Kit.Icon(iconKey, 16, Kit.B("SirajPrimaryBrush")));
        row.Children.Add(Kit.Text(title, "SirajBody", 13, bold: true));
        inner.Children.Add(row);
        inner.Children.Add(sub);
        return Kit.CardButton(inner, click, name: title, background: "SirajGlassPillBrush", padding: new Thickness(14, 11, 14, 11));
    }

    private void BuildPreviewCard()
    {
        if (_previewSlot == null) return;
        var loc = Loc.Current;
        var scene = LiveScenes.Find(S.LiveScene);
        var scheme = App.Theme.Scheme;
        bool isDark = scheme.IsDark;
        string paletteName = loc[Ui.PaletteKey(Palettes.Resolve(S.Palette))];
        string modeLabel = S.BlackMode && isDark
            ? loc["live_theme_amoled_title"]
            : S.ThemeMode switch
            {
                "Light" => loc["live_theme_mode_light"],
                "Dark" => loc["live_theme_mode_dark"],
                _ => loc["live_theme_mode_system"],
            };

        var stage = new Grid();

        var plainTop = Core.Sky.Argb.Lerp(scheme.Surface, scheme.PrimaryContainer, isDark ? 0.45f : 0.72f);
        var plainBot = isDark ? scheme.SurfaceContainerLow : scheme.Surface;
        stage.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = ThemeService.ToColor(plainTop), Offset = 0 },
                    new GradientStop { Color = ThemeService.ToColor(plainBot), Offset = 1 },
                },
            },
        });

        if (S.LiveBackground)
        {
            stage.Children.Add(new LiveBackdropControl
            {
                OverrideScene = S.LiveScene,
                OverrideLive = true,
                OverrideGlass = S.GlassUi,
                DrawVeil = true,
                Animate = true,
            });
        }

        var content = new StackPanel { Spacing = 14, Padding = new Thickness(18, 16, 18, 16) };

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = new Border
        {
            Width = 42, Height = 42, CornerRadius = new CornerRadius(13),
            Background = Kit.B("SirajPrimaryBrush"),
            Child = SirajVectorIcon.Make("emblem", 23, Kit.B("SirajOnPrimaryBrush")),
        };
        header.Children.Add(badge);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        titleStack.Children.Add(Kit.Text(loc["app_name"], "SirajHeading", 16, bold: true));
        var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        metaRow.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = 8, Height = 8, Fill = Kit.B("SirajPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center,
        });
        metaRow.Children.Add(Kit.Text($"{paletteName}{loc.Sep}{modeLabel}", "SirajCaption", 12, bold: true));
        titleStack.Children.Add(metaRow);
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        var pills = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        pills.Children.Add(Kit.Pill(
            loc[S.LiveBackground ? "live_theme_stage_live_badge" : "live_theme_stage_static_badge"],
            "SirajPrimaryBrush", "SirajOnPrimaryBrush"));
        if (S.GlassUi)
        {
            string finishKey = S.GlassFinish switch
            {
                "Satin" => "live_theme_glass_style_satin",
                "Frosted" => "live_theme_glass_style_frosted",
                _ => "live_theme_glass_style_clear",
            };
            pills.Children.Add(Kit.Pill($"{loc["appearance_glass_badge"]} · {loc[finishKey]}", "SirajGlassElevatedBrush", "SirajOnSurfaceBrush"));
        }
        if (S.LiveSkySync && S.LiveBackground)
            pills.Children.Add(Kit.Pill(loc["live_theme_stage_sky_sync_badge"], "SirajGoldSoftBrush", "SirajGoldBrush"));
        Grid.SetColumn(pills, 2);
        header.Children.Add(pills);
        content.Children.Add(header);

        var sampleInner = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        var sampleTop = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center };
        sampleTop.Children.Add(Kit.Icon("schedule", 15, Kit.B("SirajPrimaryBrush")));
        string sceneLabel = S.LiveBackground ? loc[scene.TitleKey] : loc["looks_background_plain"];
        sampleTop.Children.Add(Kit.Text($"{sceneLabel}{loc.Sep}{loc["appearance_preview_badge"]}", "SirajCaption", 12.5, bold: true, brush: Kit.B("SirajPrimaryBrush")));
        sampleInner.Children.Add(sampleTop);
        var ayahTb = Kit.Text(loc["appearance_preview_ayah"], "SirajBody", 14);
        ayahTb.TextAlignment = TextAlignment.Center;
        ayahTb.HorizontalAlignment = HorizontalAlignment.Center;
        sampleInner.Children.Add(ayahTb);

        var miniCard = new Border
        {
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(18, 13, 18, 13),
            Background = Kit.B("SirajGlassCardBrush"),
            BorderBrush = Kit.B("SirajGlassBorderBrush"),
            BorderThickness = new Thickness(1),
            Child = sampleInner,
        };
        content.Children.Add(miniCard);
        stage.Children.Add(content);

        _previewSlot.Content = new Border
        {
            CornerRadius = new CornerRadius(26),
            BorderBrush = Kit.B("SirajGlassBorderBrush"),
            BorderThickness = new Thickness(1),
            Child = stage,
        };
    }

    private void BuildSceneGrid()
    {
        if (_sceneGridSlot == null) return;
        var loc = Loc.Current;
        var scheme = App.Theme.Scheme;
        string activeSceneId = LiveScenes.NormalizeId(S.LiveScene);
        var items = new List<UIElement>(LiveScenes.All.Count + 1);

        {
            bool plainChosen = !S.LiveBackground;
            var plainTop = Core.Sky.Argb.Lerp(scheme.Surface, scheme.PrimaryContainer, scheme.IsDark ? 0.36f : 0.62f);
            var plainBot = scheme.IsDark ? scheme.SurfaceContainerLow : scheme.Surface;
            var plainFill = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = ThemeService.ToColor(plainTop), Offset = 0 },
                    new GradientStop { Color = ThemeService.ToColor(plainBot), Offset = 1 },
                },
            };
            items.Add(BuildSceneTile(loc["looks_background_plain"], thumb: null, fallbackFill: plainFill, plainChosen,
                lightLabel: scheme.IsDark, () => Set(s => s with { LiveBackground = false })));
        }

        foreach (var scene in LiveScenes.All)
        {
            bool chosen = S.LiveBackground && string.Equals(activeSceneId, scene.Id, StringComparison.OrdinalIgnoreCase);
            var thumb = SceneThumbCache.Get(scene.Id);
            string sceneId = scene.Id;
            items.Add(BuildSceneTile(loc[scene.TitleKey], thumb: thumb, fallbackFill: Kit.B("SirajSurfaceContainerHighBrush"),
                chosen, lightLabel: true, () => Set(s => s with { LiveBackground = true, LiveScene = sceneId })));
        }

        _sceneGridSlot.Content = Kit.Columns(136, 10, items);
    }

    private static Button BuildSceneTile(string title, Microsoft.UI.Xaml.Media.ImageSource? thumb, Brush fallbackFill,
        bool chosen, bool lightLabel, Action click)
    {
        var tile = new Grid { Height = 88 };

        if (thumb != null)
            tile.Children.Add(new Image { Source = thumb, Stretch = Stretch.UniformToFill });
        else
            tile.Children.Add(new Border { Background = fallbackFill });

        if (chosen)
        {
            tile.Children.Add(new Border
            {
                Width = 22, Height = 22, CornerRadius = new CornerRadius(11),
                Background = Kit.B("SirajPrimaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 6, 0),
                Child = Kit.Icon("check", 13, Kit.B("SirajOnPrimaryBrush")),
            });
        }

        var nameTb = new TextBlock
        {
            Text = title,
            FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
            FontSize = 11.5, FontWeight = chosen ? FontWeights.Bold : FontWeights.SemiBold,
            Foreground = lightLabel ? new SolidColorBrush(Colors.White) : Kit.B("SirajOnSurfaceBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(8, 0, 8, 6),
        };
        tile.Children.Add(nameTb);

        var frame = new Border
        {
            CornerRadius = new CornerRadius(14),
            BorderBrush = Kit.B(chosen ? "SirajPrimaryBrush" : "SirajGlassBorderBrush"),
            BorderThickness = new Thickness(chosen ? 2.5 : 1),
            Child = tile,
        };

        var btn = new Button
        {
            Content = frame, Padding = new Thickness(0), CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            UseSystemFocusVisuals = false,
        };
        AutomationProperties.SetName(btn, title);
        btn.Click += (_, _) => click();
        Kit.Lift(btn);
        return btn;
    }

    private UIElement BuildOffsets()
    {
        var loc = Loc.Current;
        var grid = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var type in new[] { PrayerType.Fajr, PrayerType.Dhuhr, PrayerType.Asr, PrayerType.Maghrib, PrayerType.Isha })
        {
            var box = new NumberBox
            {
                Header = loc.PrayerName(type), Value = S.OffsetOf(type), Minimum = -30, Maximum = 30, SmallChange = 1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Width = 96,
            };
            LocalNumbers.Apply(box);
            box.ValueChanged += (_, args) =>
            {
                if (double.IsNaN(args.NewValue)) return;
                Set(s => s.WithOffset(type, (int)Math.Round(args.NewValue)));
            };
            grid.Children.Add(box);
        }
        var scroller = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = grid };
        return Ui.Row(loc["settings_calc_offsets_title"], loc["settings_calc_offsets_desc"], scroller, stack: true);
    }

    private IEnumerable<UIElement> BuildAlerts()
    {
        var loc = Loc.Current;
        var which = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var type in Enum.GetValues<PrayerType>())
        {
            var check = new CheckBox { Content = loc.PrayerName(type), IsChecked = S.AlertOn(type), MinWidth = 0, Margin = new Thickness(0, 0, 8, 0) };
            check.Click += (_, _) => Set(s => s.WithAlert(type, check.IsChecked == true));
            which.Children.Add(check);
        }
        yield return Ui.Row(loc["onboarding_alerts_which"], null,
            new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = which }, stack: true);

        yield return Ui.Row(loc["win_alert_type"], null,
            Ui.Segments<string>([("azan", loc["reminders_mode_full_azan"]), ("notification", loc["reminders_mode_tone_only"]), ("silent", loc["reminders_mode_silent"])],
                S.AlertType, v => Set(s => s with { AlertType = v })));

        var voices = AdhanVoices.All.Select(v => (v.Id, v.Name(loc.Language))).ToList();
        var voice = Ui.Choice<string>(voices, AdhanVoices.Find(S.AdhanVoice).Id, v => Set(s => s with { AdhanVoice = v }), 270);
        _listen = Ui.Button(loc["reminders_preview_audio"], () =>
        {
            if (App.Adhan.IsPlaying) App.Adhan.Stop();
            else App.Adhan.Preview(AdhanVoices.Find(S.AdhanVoice));
        });
        _voiceStatus = Ui.Text("", "SirajCaption");
        _voicePortraitSlot = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
        UpdateVoicePortrait();

        var voiceControls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        voiceControls.Children.Add(_voicePortraitSlot);
        voiceControls.Children.Add(voice);
        voiceControls.Children.Add(_listen);
        var voiceRow = Ui.Row(loc["onboarding_alerts_voice"], null, voiceControls);
        ((StackPanel)voiceRow.Children[0]).Children.Add(_voiceStatus);
        UpdateVoiceStatus();
        yield return voiceRow;

        yield return Ui.Row(loc["fajr_adhan_title"], loc["win_fajr_adhan_desc"], Ui.Toggle(S.FajrAdhan, v => Set(s => s with { FajrAdhan = v })));

        var volume = new Slider { Minimum = 0, Maximum = 100, Value = S.AdhanVolume, Width = 220, StepFrequency = 5 };
        volume.ValueChanged += (_, args) => Set(s => s with { AdhanVolume = (int)args.NewValue });
        yield return Ui.Row(loc["win_volume"], null, volume);

        yield return Ui.Row(loc["win_reminder_before"], null,
            Ui.Choice<int>(PrePrayerReminder.Options.Select(m => (m, m == 0 ? loc["win_off"] : loc.Format("win_minutes_before", loc.Number(m)))).ToList(),
                PrePrayerReminder.Normalize(S.PrePrayerReminderMinutes), v => Set(s => s with { PrePrayerReminderMinutes = v }), 200));

        yield return Ui.Row(loc["adhan_screen_setting_title"], loc["win_adhan_window_desc"],
            Ui.Toggle(S.AdhanWindow, v => Set(s => s with { AdhanWindow = v })));

        yield return Ui.Row(loc["reminders_test_notification_title"], null, Ui.Button(loc["win_test_adhan"], TestAdhan));
    }

    private void UpdateVoicePortrait()
    {
        if (_voicePortraitSlot == null) return;
        var voice = AdhanVoices.Find(S.AdhanVoice);
        string? file = ListeningLibrary.PortraitForAdhan(voice.Id);
        _voicePortraitSlot.Content = PortraitAvatar.Create(file != null ? $"Portraits/{file}" : null, voice.Name(Loc.Current.Language), 40, 12);
    }

    private static void TestAdhan()
    {
        var next = App.Clock.Day?.Next ?? new PrayerTime(PrayerType.Dhuhr, DateTimeOffset.Now);
        var type = next.Type == PrayerType.Sunrise ? PrayerType.Dhuhr : next.Type;
        App.Adhan.Test(type);
    }

    private void UpdateVoiceStatus()
    {
        if (_voiceStatus == null) return;
        var loc = Loc.Current;
        var voice = AdhanVoices.Find(S.AdhanVoice);
        bool missing = AdhanVoices.WantedDownloads(voice, S.FajrAdhan).Any(r => !VoiceDownloads.Has(r));
        _voiceStatus.Text = missing ? loc["win_voice_clearer"] : loc["win_voice_ready"];
    }

    private void OnPlaying(PrayerType? prayer)
    {
        if (_listen != null) _listen.Content = App.Adhan.IsPlaying ? Loc.Current["reminders_stop_adhan"] : Loc.Current["reminders_preview_audio"];
    }

    private void BuildSwatches()
    {
        if (_swatches != null) _swatches.Content = Ui.Swatches(S.Palette, v => Set(s => s with { Palette = v }));
    }

    private void UpdatePlace()
    {
        if (_placeText == null) return;
        var loc = Loc.Current;
        var place = S.Location;
        string display = place == null ? loc["settings_not_set"]
            : place.DisplayName(loc.Language) ?? loc["win_location_coordinates"];
        _placeText.Text = display;
        if (_quickPlaceSub != null) _quickPlaceSub.Text = display;
        if (place != null && _locateStatus != null)
            _locateStatus.Text = loc.Digitize($"{place.Latitude:0.0000}, {place.Longitude:0.0000}") + loc.Sep + place.TimeZone;
    }

    private async Task Detect()
    {
        var loc = Loc.Current;
        if (_locateStatus != null) _locateStatus.Text = loc["win_location_detecting"];
        var (outcome, place) = await LocationService.LocateAsync();
        switch (outcome)
        {
            case LocationService.Outcome.Found:
                LocationService.Use(place!, manual: false, recommendMethod: true);
                break;
            case LocationService.Outcome.Denied:
                if (_locateStatus != null) _locateStatus.Text = loc["win_location_denied"];
                var dialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = loc["location_permission_needed_title"],
                    Content = loc["win_location_denied"],
                    PrimaryButtonText = loc["win_location_open_settings"],
                    CloseButtonText = loc["settings_close"],
                    FlowDirection = loc.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary) LocationService.OpenWindowsLocationSettings();
                break;
            default:
                if (_locateStatus != null) _locateStatus.Text = loc["location_gps_off_desc"];
                break;
        }
    }

    private sealed class CityItem(City city)
    {
        public City City { get; } = city;
        public override string ToString() =>
            $"{City.DisplayName(Loc.Current.Language)}, {City.DisplayCountry(Loc.Current.Language)}";
    }
}
