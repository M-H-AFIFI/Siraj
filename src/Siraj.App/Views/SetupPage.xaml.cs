using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Adhan;
using Siraj.Core.Places;
using Siraj.Core.Prayer;
using Siraj.Core.Settings;

namespace Siraj.App.Views;

public sealed partial class SetupPage : Page
{
    private const int Steps = 6;
    private int _step;
    private bool _addWidget = true;
    private TextBlock? _placeStatus;

    public SetupPage()
    {
        InitializeComponent();
        Kit.Lift(BackButton);
        Kit.Lift(NextButton);
        ProgressTrack.SizeChanged += (_, _) => UpdateProgress(animate: false);
        Loaded += (_, _) =>
        {
            App.Settings.Changed += OnSettings;
            UpdateProgress(animate: false);
        };
        Unloaded += (_, _) => App.Settings.Changed -= OnSettings;
        Show(0);
    }

    private static AppSettings S => App.Settings.Current;

    private static void Set(Func<AppSettings, AppSettings> change) => App.Settings.Update(change);

    private void OnSettings(AppSettings old, AppSettings updated) => DispatcherQueue.TryEnqueue(() =>
    {
        // Language changes rebuild the window; location and theme changes refresh this step.
        if (old.Location != updated.Location || old.Palette != updated.Palette || old.ThemeMode != updated.ThemeMode) Show(0);
    });

    private void UpdateProgress(bool animate)
    {
        double w = ProgressTrack.ActualWidth;
        if (w <= 0) return;
        double target = w * (_step + 1) / Steps;
        if (!animate || !Kit.Motion)
        {
            ProgressFill.Width = target;
            return;
        }
        var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(155),
            EnableDependentAnimation = true,
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut },
        };
        var story = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, ProgressFill);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Width");
        story.Children.Add(anim);
        story.Begin();
    }

    private void Show(int direction = 0)
    {
        var loc = Loc.Current;
        StepText.Text = loc.Format("onboarding_polish_step_of", loc.Number(_step + 1), loc.Number(Steps));
        BackButton.Content = loc["win_back"];
        BackButton.Visibility = _step == 0 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Content = _step == Steps - 1 ? loc["onboarding_done_cta"] : loc["onboarding_continue"];
        NextButton.IsEnabled = _step != 1 || S.Location != null;
        UpdateProgress(animate: direction != 0);
        var body = new StackPanel { Spacing = 14 };
        switch (_step)
        {
            case 0:
                LanguageStep(body);
                break;
            case 1:
                Location(body);
                break;
            case 2:
                Calculation(body);
                break;
            case 3:
                Alerts(body);
                break;
            case 4:
                Look(body);
                break;
            default:
                Desktop(body);
                break;
        }
        StepBody.Content = body;
        if (direction != 0)
        {
            float dir = (loc.IsRtl ? -1f : 1f) * direction;
            Kit.AnimateIn(body, 18 * dir, 0, 1f, 150);
            Kit.AnimateIn(TitleText, 8 * dir, 0, 1f, 130);
        }
    }

    private void LanguageStep(StackPanel body)
    {
        var loc = Loc.Current;
        TitleText.Text = loc["onboarding_lang_title"];
        DescText.Text = loc["onboarding_lang_desc"];
        foreach (var (code, name) in new[] { ("en", "English"), ("ar", "العربية"), ("tr", "Türkçe"), ("ur", "اردو") })
        {
            var radio = new RadioButton
            {
                Content = name, GroupName = "lang", IsChecked = S.Language == code,
                FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SirajFont"], FontSize = 16,
            };
            radio.Checked += (_, _) => Set(s => s with { Language = code });
            body.Children.Add(radio);
        }
    }

    private void Location(StackPanel body)
    {
        var loc = Loc.Current;
        TitleText.Text = loc["onboarding_loc_title"];
        DescText.Text = loc["onboarding_loc_desc"];
        _placeStatus = Ui.Text(S.Location?.DisplayName(loc.Language) is { } name ? loc.Format("win_location_found", name)
            : S.Location != null ? loc["win_location_coordinates"] : loc["settings_not_set"], "SirajHeading");
        body.Children.Add(_placeStatus);

        var detect = Ui.Button(loc["win_location_detect"], async () =>
        {
            _placeStatus!.Text = loc["win_location_detecting"];
            var (outcome, place) = await LocationService.LocateAsync();
            if (outcome == LocationService.Outcome.Found) LocationService.Use(place!, manual: false, recommendMethod: true);
            else
            {
                _placeStatus.Text = loc["win_location_denied"];
                _placeStatus.Style = (Style)Application.Current.Resources["SirajCaption"];
            }
        }, primary: S.Location == null);
        var search = new AutoSuggestBox { PlaceholderText = loc["settings_search_city_hint"], QueryIcon = SirajVectorIcon.MakePathIcon("search") };
        search.TextChanged += (box, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            box.ItemsSource = string.IsNullOrWhiteSpace(box.Text) ? null
                : Cities.Search(box.Text).Take(10).Select(c => $"{c.DisplayName(loc.Language)}, {c.DisplayCountry(loc.Language)}").ToList();
        };
        search.QuerySubmitted += (box, args) =>
        {
            var text = (args.ChosenSuggestion as string ?? args.QueryText).Split(',')[0].Trim();
            var city = Cities.Search(text).FirstOrDefault();
            if (city != null) LocationService.Use(Place.FromCity(city), manual: true, recommendMethod: true);
        };
        search.SuggestionChosen += (box, args) =>
        {
            var city = Cities.Search(((string)args.SelectedItem).Split(',')[0].Trim()).FirstOrDefault();
            if (city != null) LocationService.Use(Place.FromCity(city), manual: true, recommendMethod: true);
        };
        body.Children.Add(detect);
        body.Children.Add(Ui.Text(loc["settings_manual_override"], "SirajCaption"));
        body.Children.Add(search);
    }

    private void Calculation(StackPanel body)
    {
        var loc = Loc.Current;
        TitleText.Text = loc["onboarding_calc_title"];
        var country = S.Location?.Country;
        // Show regional hint when a country-specific calculation method is selected.
        DescText.Text = loc[CalculationMethods.RecommendedFor(country) == CalculationMethods.FallbackMethod
            ? "onboarding_calc_hint_general" : "onboarding_calc_hint_local"];
        var methods = CalculationMethods.All.Select(m => (m.Id, m.Name(loc.Language))).ToList();
        body.Children.Add(Ui.Text(loc["onboarding_calc_authority"], "SirajHeading"));
        body.Children.Add(Ui.Choice<string>(methods, CalculationMethods.CanonicalId(S.CalculationMethod), v => Set(s => s with { CalculationMethod = v }), 360));
        body.Children.Add(Ui.Text(loc["onboarding_calc_asr"], "SirajHeading"));
        body.Children.Add(Ui.Segments<string>([("Shafi", loc["madhab_shafi_desc_short"]), ("Hanafi", loc["madhab_hanafi_desc_short"])], S.Madhab,
            v => Set(s => s with { Madhab = v })));
        if (App.Clock.Day is { } day)
        {
            var preview = string.Join("   ", day.Times.Where(t => t.Type != PrayerType.Sunrise).Select(t => $"{loc.PrayerName(t.Type)} {loc.Time(t.Time)}"));
            body.Children.Add(Ui.Text(preview, "SirajCaption"));
        }
    }

    private void Alerts(StackPanel body)
    {
        var loc = Loc.Current;
        TitleText.Text = loc["onboarding_alerts_title"];
        DescText.Text = loc["win_setup_alerts_desc"];
        body.Children.Add(Ui.Text(loc["onboarding_alerts_which"], "SirajHeading"));
        var which = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var type in new[] { PrayerType.Fajr, PrayerType.Dhuhr, PrayerType.Asr, PrayerType.Maghrib, PrayerType.Isha })
        {
            var check = new CheckBox { Content = loc.PrayerName(type), IsChecked = S.AlertOn(type), MinWidth = 0, Margin = new Thickness(0, 0, 10, 0) };
            check.Click += (_, _) => Set(s => s.WithAlert(type, check.IsChecked == true));
            which.Children.Add(check);
        }
        body.Children.Add(which);
        body.Children.Add(Ui.Text(loc["onboarding_alerts_voice"], "SirajHeading"));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(Ui.Choice<string>(AdhanVoices.All.Select(v => (v.Id, v.Name(loc.Language))).ToList(), AdhanVoices.Find(S.AdhanVoice).Id,
            v => Set(s => s with { AdhanVoice = v }), 300));
        row.Children.Add(Ui.Button(loc["onboarding_preview_adhan"], () =>
        {
            if (App.Adhan.IsPlaying) App.Adhan.Stop();
            else App.Adhan.Preview(AdhanVoices.Find(S.AdhanVoice));
        }));
        body.Children.Add(row);
        var screen = new CheckBox { Content = loc["adhan_screen_setting_title"], IsChecked = S.AdhanWindow };
        screen.Click += (_, _) => Set(s => s with { AdhanWindow = screen.IsChecked == true });
        body.Children.Add(screen);
        body.Children.Add(Ui.Text(loc["win_adhan_window_desc"], "SirajCaption"));
    }

    private void Look(StackPanel body)
    {
        var loc = Loc.Current;
        TitleText.Text = loc["onboarding_style_title"];
        DescText.Text = loc["onboarding_style_desc"];
        body.Children.Add(Ui.Swatches(S.Palette, v => Set(s => s with { Palette = v })));
        body.Children.Add(Ui.Segments<string>([("System", loc["settings_system_default"]), ("Light", loc["settings_light"]), ("Dark", loc["settings_dark"])],
            S.ThemeMode, v => Set(s => s with { ThemeMode = v })));
    }

    private void Desktop(StackPanel body)
    {
        var loc = Loc.Current;
        TitleText.Text = loc["win_setup_desktop_title"];
        DescText.Text = loc["win_setup_desktop_desc"];
        var start = new CheckBox { Content = loc["win_start_with_windows"], IsChecked = S.StartWithWindows };
        start.Click += (_, _) => Set(s => s with { StartWithWindows = start.IsChecked == true });
        body.Children.Add(start);
        body.Children.Add(Ui.Text(loc["win_start_with_windows_desc"], "SirajCaption"));
        var widget = new CheckBox { Content = loc["win_setup_widget"], IsChecked = _addWidget };
        widget.Click += (_, _) => _addWidget = widget.IsChecked == true;
        body.Children.Add(widget);
        body.Children.Add(Ui.Text(loc["onboarding_change_later"], "SirajCaption"));
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_step > 0) _step--;
        Show(-1);
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        App.Adhan.Stop();
        if (_step < Steps - 1)
        {
            _step++;
            Show(1);
            return;
        }
        Set(s => s with { SetupDone = true });
        if (_addWidget && S.Widgets.Count == 0) App.Widgets.Add("next", Widgets.WidgetFaces.Find("next").Dress);
        if (App.MainWindowInstance is MainWindow main) main.SetupFinished();
    }
}
