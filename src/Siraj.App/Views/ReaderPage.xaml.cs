using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Siraj.App.Controls;
using Siraj.App.Quran;
using Siraj.App.Services;
using Siraj.Core.Quran;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Siraj.App.Views;

internal sealed record ReaderArgs(int Page, (int Surah, int Ayah)? Ayah = null);

public sealed partial class ReaderPage : Page
{
    private int _page = 1;
    private (int Surah, int Ayah)? _selected;
    private bool _building;
    private MushafColors _mushafColors = MushafColors.For("Light", "Creamy", false, false, Microsoft.UI.Colors.Green);

    public ReaderPage()
    {
        InitializeComponent();
        LeftPage.AyahClicked += ayah => Select(ayah, LeftPage.PageNumber);
        RightPage.AyahClicked += ayah => Select(ayah, RightPage.PageNumber);
        Pages.SizeChanged += (_, _) => Arrange();
        Loaded += (_, _) =>
        {
            App.QuranPlayer.Changed += OnPlayer;
            App.Theme.Changed += ApplyColors;
            Focus(FocusState.Programmatic);
        };
        Unloaded += (_, _) =>
        {
            App.QuranPlayer.Changed -= OnPlayer;
            App.Theme.Changed -= ApplyColors;
            FinishTurnNow();
            TurnLayer.RemoveFromVisualTree();
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var loc = Loc.Current;
        if (e.Parameter is ReaderArgs args)
        {
            _page = Math.Clamp(args.Page, 1, QuranDatabase.PageCount);
            _selected = args.Ayah;
        }
        else if (App.Settings.Current.LastRead is { } last)
        {
            _page = last.Page;
        }
        BackIcon.IconKey = loc.IsRtl ? "arrow_forward" : "arrow_back";
        // Set both tooltip and AutomationProperties.Name for icon-only buttons.
        Label(BackButton, loc["win_back"]);
        Label(SpreadToggle, loc["win_two_pages"]);
        Label(PaperButton, loc["settings_mushaf_appearance_header"]);
        Label(PageBox, loc["win_go_to_page"]);
        LocalNumbers.Apply(PageBox);
        Label(PreviousButton, loc["previous"]);
        Label(NextButton, loc["next"]);
        Label(StopButton, loc["rdl_action_stop"]);
        Label(CloseSideButton, loc["close"]);
        SpreadToggle.IsChecked = App.Settings.Current.QuranSpread;
        ContinuousBox.Content = loc["win_continuous"];
        ContinuousBox.IsChecked = App.Settings.Current.QuranContinuous;
        PlayFromButton.Content = loc["quran_polish_play_from_here"];
        CopyButton.Content = loc["quran_action_copy"];
        TafsirTitle.Text = loc["win_tafsir_muyassar"];
        BuildPaperMenu();
        BuildReciters();
        ApplyColors();
        ShowPage(_page);
        if (_selected is { } ayah) Select(ayah, _page);
        UpdatePlayer();
    }

    private static void Label(FrameworkElement control, string name)
    {
        ToolTipService.SetToolTip(control, name);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, name);
    }

    private bool TwoPages => App.Settings.Current.QuranSpread && Pages.ActualWidth >= 1.1 * Pages.ActualHeight && Pages.ActualWidth >= 700;

    private void Arrange()
    {
        bool two = TwoPages;
        var (frameW, frameH) = MushafPageView.Frame(0, 0, two ? PageSide.Left : PageSide.Single);
        double height = Pages.ActualHeight - 8 - frameH;
        if (height <= 0) return;
        FinishTurnNow();
        double sheetWidth = Math.Min(height * 0.66, (Pages.ActualWidth - 32 - (two ? 2 * frameW : frameW)) / (two ? 2 : 1));
        double sheetHeight = Math.Min(height, sheetWidth / 0.66);
        LeftPage.Side = PageSide.Left;
        RightPage.Side = two ? PageSide.Right : PageSide.Single;
        foreach (var view in new[] { LeftPage, RightPage })
        {
            var (w, h) = MushafPageView.Frame(Math.Max(200, sheetWidth), Math.Max(300, sheetHeight), view.Side);
            view.Width = w;
            view.Height = h;
        }
        LeftPage.Visibility = two ? Visibility.Visible : Visibility.Collapsed;
        ShowPage(_page);
    }

    private void ShowPage(int page)
    {
        _page = Math.Clamp(page, 1, QuranDatabase.PageCount);
        bool two = TwoPages;
        if (two)
        {
            int right = _page % 2 == 1 ? _page : _page - 1;
            RightPage.Show(right);
            if (right + 1 <= QuranDatabase.PageCount)
            {
                LeftPage.Show(right + 1);
                LeftPage.Visibility = Visibility.Visible;
            }
            else
            {
                LeftPage.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            RightPage.Show(_page);
        }
        _building = true;
        PageBox.Value = _page;
        _building = false;

        var db = App.Quran.Database;
        var first = db.Ayahs().First(a => a.Page >= _page);
        var surah = db.Surah(first.Surah);
        var loc = Loc.Current;
        TitleText.Text = loc.IsRtl ? surah.Name : $"{surah.EnglishName} · {surah.Name}";
        SubtitleText.Text = $"{loc.Format("quran_page_label", loc.Number(_page))}{loc.Sep}{loc.Format("quran_juz_label", loc.Number(QuranDatabase.JuzOfPage(_page)))}";
        App.Quran.SavePosition(_page, first.Surah, first.NumberInSurah);
        RefreshHighlights();
        // Prefetch adjacent spreads for immediate page turns.
        int spread = SpreadOf(_page), step = two ? 2 : 1;
        for (int k = -step; k < 2 * step; k++) if (k < 0 || k >= step) MushafPageView.Prefetch(spread + k);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is TextBox or NumberBox) return;
        switch (e.Key)
        {
            // RTL Mushaf: left/down advances forward, right/up goes back.
            case VirtualKey.Left or VirtualKey.PageDown or VirtualKey.Down or VirtualKey.Space:
                Forward();
                break;
            case VirtualKey.Right or VirtualKey.PageUp or VirtualKey.Up:
                Back();
                break;
            case VirtualKey.Home:
                TurnTo(1);
                break;
            case VirtualKey.End:
                TurnTo(QuranDatabase.PageCount);
                break;
            case VirtualKey.Escape:
                CloseSide();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnPageBox(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_building || double.IsNaN(args.NewValue)) return;
        TurnTo((int)args.NewValue);
    }

    private void OnSpread(object sender, RoutedEventArgs e)
    {
        App.Settings.Update(s => s with { QuranSpread = SpreadToggle.IsChecked == true });
        Arrange();
    }

    private void OnBack(object sender, RoutedEventArgs e) => App.MainWindowInstance?.Navigate("quran", force: true);

    private void Select((int Surah, int Ayah) ayah, int page)
    {
        _selected = ayah;
        _page = page;
        var loc = Loc.Current;
        var db = App.Quran.Database;
        var a = db.Ayah(ayah.Surah, ayah.Ayah);
        var surah = db.Surah(ayah.Surah);
        SideTitle.Text = $"{(loc.IsRtl ? surah.Name : surah.EnglishName)} {loc.Number(ayah.Surah)}:{loc.Number(ayah.Ayah)}";
        AyahText.Text = DigitalKhattText.ForDisplay(a.TextUthmani);
        TafsirText.Text = db.Tafsir(a.Number) ?? "";
        UpdateBookmarkButton();
        Side.Visibility = Visibility.Visible;
        SideColumn.Width = new GridLength(380);
        Arrange();
        RefreshHighlights();
    }

    private void CloseSide()
    {
        _selected = null;
        Side.Visibility = Visibility.Collapsed;
        SideColumn.Width = new GridLength(0);
        Arrange();
        RefreshHighlights();
    }

    private void OnCloseSide(object sender, RoutedEventArgs e) => CloseSide();

    private void UpdateBookmarkButton()
    {
        if (_selected is not { } s) return;
        BookmarkButton.Content = Loc.Current[App.Quran.IsBookmarked(s.Surah, s.Ayah) ? "quran_action_remove_bookmark" : "quran_action_add_bookmark"];
    }

    private void OnBookmark(object sender, RoutedEventArgs e)
    {
        if (_selected is not { } s) return;
        App.Quran.ToggleBookmark(s.Surah, s.Ayah, App.Quran.Database.PageOf(s.Surah, s.Ayah));
        UpdateBookmarkButton();
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (_selected is not { } s) return;
        var db = App.Quran.Database;
        var surah = db.Surah(s.Surah);
        var package = new DataPackage();
        package.SetText($"{db.Ayah(s.Surah, s.Ayah).TextUthmani}\n[{surah.Name} {s.Ayah}]");
        Clipboard.SetContent(package);
        CopyButton.Content = Loc.Current["quran_polish_action_copied"];
    }

    private void OnPlayFrom(object sender, RoutedEventArgs e)
    {
        if (_selected is { } s) App.QuranPlayer.Play(s.Surah, s.Ayah);
    }

    private void BuildReciters()
    {
        var loc = Loc.Current;
        ReciterBox.Items.Clear();
        foreach (var r in Reciters.All)
        {
            var style = r.StyleName(loc.Language);
            ReciterBox.Items.Add(style != null && !r.Name(loc.Language).Contains(style) ? $"{r.Name(loc.Language)} · {style}" : r.Name(loc.Language));
        }
        ReciterBox.SelectedIndex = Reciters.All.ToList().IndexOf(Reciters.Find(App.Settings.Current.ReciterId));
        UpdateReciterAvatar();
    }

    private void OnReciter(object sender, SelectionChangedEventArgs e)
    {
        if (ReciterBox.SelectedIndex < 0) return;
        var id = Reciters.All[ReciterBox.SelectedIndex].Id;
        if (id == App.Settings.Current.ReciterId) return;
        App.Settings.Update(s => s with { ReciterId = id });
        UpdateReciterAvatar();
        // Restart from the current ayah when switching reciters.
        if (App.QuranPlayer.Current is { } at && App.QuranPlayer.IsPlaying) App.QuranPlayer.Play(at.Surah, at.Ayah);
    }

    private void UpdateReciterAvatar()
    {
        var reciter = Reciters.Find(App.Settings.Current.ReciterId);
        string? art = Core.Audio.ListeningLibrary.PortraitFiles.TryGetValue(reciter.Id, out var f) ? $"Portraits/{f}" : null;
        ReciterAvatarSlot.Content = PortraitAvatar.Create(art, reciter.Name(Loc.Current.Language), 34, 17, activeRing: App.QuranPlayer.IsPlaying);
    }

    private void OnContinuous(object sender, RoutedEventArgs e) =>
        App.Settings.Update(s => s with { QuranContinuous = ContinuousBox.IsChecked == true });

    private void OnPlay(object sender, RoutedEventArgs e)
    {
        var player = App.QuranPlayer;
        if (player.IsPlaying) player.Pause();
        else if (player.IsActive) player.Resume();
        else
        {
            var start = _selected ?? FirstAyahOnPage();
            player.Play(start.Surah, start.Ayah);
        }
    }

    private (int Surah, int Ayah) FirstAyahOnPage()
    {
        var a = App.Quran.Database.Ayahs().FirstOrDefault(x => x.Page == (TwoPages ? (_page % 2 == 1 ? _page : _page - 1) : _page))
                ?? App.Quran.Database.Ayahs().First(x => x.Page >= _page);
        return (a.Surah, a.NumberInSurah);
    }

    private void OnPrevious(object sender, RoutedEventArgs e) => App.QuranPlayer.Previous();
    private void OnNext(object sender, RoutedEventArgs e) => App.QuranPlayer.Next();
    private void OnStop(object sender, RoutedEventArgs e) => App.QuranPlayer.Stop();

    private void OnPlayer()
    {
        UpdatePlayer();
        if (App.QuranPlayer.Current is { } at)
        {
            int page = App.Quran.Database.PageOf(at.Surah, at.Ayah);
            bool visible = TwoPages ? page == RightPage.PageNumber || page == LeftPage.PageNumber : page == _page;
            if (!visible) TurnTo(page);
        }
        RefreshHighlights();
    }

    private void UpdatePlayer()
    {
        var player = App.QuranPlayer;
        var loc = Loc.Current;
        PlayIcon.IconKey = player.IsPlaying ? "pause" : "play";
        Label(PlayButton, loc[player.IsPlaying ? "pause" : "play"]);
        if (player.Error != null) NowText.Text = player.Error;
        else if (player.Current is { } at)
        {
            var surah = App.Quran.Database.Surah(at.Surah);
            NowText.Text = $"{Reciters.Find(App.Settings.Current.ReciterId).Name(loc.Language)}{loc.Sep}{(loc.IsRtl ? surah.Name : surah.EnglishName)} {loc.Number(at.Ayah)}";
        }
        else NowText.Text = "";
    }

    private void RefreshHighlights()
    {
        var playing = App.QuranPlayer.Current;
        LeftPage.SetHighlights(playing, _selected);
        RightPage.SetHighlights(playing, _selected);
    }

    private void BuildPaperMenu()
    {
        var loc = Loc.Current;
        PaperMenu.Items.Clear();
        foreach (var (key, label) in new[] { ("System", "settings_mushaf_theme_system_title"), ("Light", "settings_mushaf_theme_light_title"), ("Dark", "settings_mushaf_theme_dark_title") })
        {
            var item = new RadioMenuFlyoutItem { Text = loc[label], GroupName = "theme", IsChecked = App.Settings.Current.QuranTheme == key };
            item.Click += (_, _) =>
            {
                App.Settings.Update(s => s with { QuranTheme = key });
                ApplyColors();
            };
            PaperMenu.Items.Add(item);
        }
        PaperMenu.Items.Add(new MenuFlyoutSeparator());
        foreach (var (key, label) in new[] { ("Creamy", "settings_mushaf_color_yellowish_title"), ("White", "settings_mushaf_color_white_title"), ("Sepia", "settings_mushaf_color_sepia_title") })
        {
            var item = new RadioMenuFlyoutItem { Text = loc[label], GroupName = "paper", IsChecked = App.Settings.Current.QuranPaper == key };
            item.Click += (_, _) =>
            {
                App.Settings.Update(s => s with { QuranPaper = key });
                ApplyColors();
            };
            PaperMenu.Items.Add(item);
        }
        ToolTipService.SetToolTip(PaperButton, loc["settings_mushaf_appearance_header"]);
    }

    private void ApplyColors()
    {
        var s = App.Settings.Current;
        var colors = MushafColors.For(s.QuranTheme, s.QuranPaper, App.Theme.IsDark, s.BlackMode, ThemeService.ToColor(App.Theme.Scheme.Primary));
        _mushafColors = colors;
        LeftPage.SetColors(colors);
        RightPage.SetColors(colors);
    }
}
