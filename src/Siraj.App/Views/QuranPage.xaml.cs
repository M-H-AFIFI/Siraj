using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Quran;
using Siraj.Core.Text;

namespace Siraj.App.Views;

public sealed partial class QuranPage : Page
{
    private enum IndexTab { Surahs, Juz, Bookmarks }

    private IndexTab _tab = IndexTab.Surahs;
    private CancellationTokenSource? _search;
    private bool _loaded;
    private bool _warming;
    private string _builtKey = "";
    private bool _tabsBuilt;

    public QuranPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        var loc = Loc.Current;
        TitleText.Text = loc["nav_quran"];
        SearchBox.PlaceholderText = loc["quran_index_search_hint"];
        SearchBox.QueryIcon = SirajVectorIcon.MakePathIcon("search");
        DownloadTitle.Text = loc["win_quran_download_title"];
        DownloadBody.Text = loc.Format("win_quran_download_body", loc.Format("win_megabytes", loc.Number(QuranService.DownloadBytes / 1_000_000)));
        CancelButton.Content = loc["settings_cancel"];

        var listenInner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        listenInner.Children.Add(SirajVectorIcon.Make("quick_listen", 16, Kit.B("SirajPrimaryBrush")));
        listenInner.Children.Add(Kit.Text(loc["quick_listen"], "SirajBody", 13.5, bold: true));
        ListenShortcut.Content = listenInner;

        QuickJuzBtn.Content = SirajVectorIcon.Make("khatma", 18, Kit.B("SirajPrimaryBrush"));
        ToolTipService.SetToolTip(QuickJuzBtn, loc["quran_index_tab_juz"]);
        AutomationProperties.SetName(QuickJuzBtn, loc["quran_index_tab_juz"]);
    }

    private string CurrentKey()
    {
        var s = App.Settings.Current;
        var kh = App.Journal.Current.Khatma;
        return $"{App.Quran.Status}|{_tab}|{s.LastRead?.Page}:{s.LastRead?.Surah}:{s.LastRead?.Ayah}|{s.Bookmarks.Count}|{kh?.CompletedDays.Count}:{kh?.TargetDays}|{App.Theme.Scheme.Primary}|{App.Theme.IsDark}|{App.Theme.IsGlass}";
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Quran.Changed += OnQuranChanged;
        App.Settings.Changed += OnSettingsChanged;
        App.Journal.Changed += OnJournalChanged;
        App.Theme.Changed += OnThemeChanged;
        _loaded = true;
        if (!_tabsBuilt) { BuildTabs(); _tabsBuilt = true; }
        string key = CurrentKey();
        if (key != _builtKey || App.Quran.Status != QuranService.State.Ready)
            Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        _search?.Cancel();
        App.Quran.Changed -= OnQuranChanged;
        App.Settings.Changed -= OnSettingsChanged;
        App.Journal.Changed -= OnJournalChanged;
        App.Theme.Changed -= OnThemeChanged;
    }

    private void OnQuranChanged() => DispatcherQueue.TryEnqueue(() => { _builtKey = ""; Refresh(); });

    private void OnThemeChanged() => DispatcherQueue.TryEnqueue(() => { _builtKey = ""; if (_loaded) { BuildTabs(); Refresh(); } });

    private void OnJournalChanged() => DispatcherQueue.TryEnqueue(() => { _builtKey = ""; if (_loaded) BuildKhatmaCard(); });

    private void OnSettingsChanged(Core.Settings.AppSettings old, Core.Settings.AppSettings updated)
    {
        if (old.Bookmarks != updated.Bookmarks || old.LastRead != updated.LastRead)
            DispatcherQueue.TryEnqueue(() => { _builtKey = ""; Refresh(); });
    }

    private void BuildTabs()
    {
        var loc = Loc.Current;
        TabsSlot.Content = Ui.Segments<IndexTab>(
        [
            (IndexTab.Surahs, loc["quran_index_tab_surahs"]),
            (IndexTab.Juz, loc["quran_index_tab_juz"]),
            (IndexTab.Bookmarks, loc["quran_index_tab_bookmarks"]),
        ], _tab, tab =>
        {
            _tab = tab;
            if (!string.IsNullOrEmpty(SearchBox.Text)) SearchBox.Text = "";
            ShowTab();
        });
    }

    private void OnQuickToggleJuz(object sender, RoutedEventArgs e)
    {
        _tab = _tab == IndexTab.Juz ? IndexTab.Surahs : IndexTab.Juz;
        BuildTabs();
        ShowTab();
    }

    private void OnOpenListen(object sender, RoutedEventArgs e) => App.MainWindowInstance?.Navigate("listen");

    private async void Refresh()
    {
        if (!_loaded) return;
        var loc = Loc.Current;
        var quran = App.Quran;
        bool ready = quran.Status == QuranService.State.Ready;
        DownloadCard.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        List.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;

        if (!ready)
        {
            bool busy = quran.Status == QuranService.State.Downloading;
            DownloadBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            DownloadBar.Value = quran.Progress.Fraction;
            CancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            DownloadButton.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
            DownloadButton.Content = quran.Status == QuranService.State.Failed ? loc["win_retry"] : loc["win_quran_download_action"];
            DownloadStatus.Text = quran.Status switch
            {
                QuranService.State.Downloading => loc.Format("win_quran_downloading",
                    loc.Format("win_megabytes", loc.Number(quran.Progress.Received / 1_000_000)),
                    loc.Format("win_megabytes", loc.Number(QuranService.DownloadBytes / 1_000_000))),
                QuranService.State.Failed => loc["win_quran_download_failed"],
                _ => ""
            };
            return;
        }

        // Warm the SQLite cache off the UI thread so the first visit never hitches.
        if (!_warming)
        {
            _warming = true;
            try
            {
                await Task.Run(() => { _ = quran.Database.Surahs(); _ = quran.Database.Ayahs(); });
            }
            finally { _warming = false; }
        }
        if (!_loaded) return;

        _builtKey = CurrentKey();
        BuildContinueHero();
        BuildKhatmaCard();
        if (string.IsNullOrWhiteSpace(SearchBox.Text)) ShowTab();
    }

    private void BuildContinueHero()
    {
        var loc = Loc.Current;
        var db = App.Quran.Database;
        var last = App.Settings.Current.LastRead;
        int page = last?.Page ?? 1;
        int surahNum = last?.Surah ?? 1;
        int ayahNum = last?.Ayah ?? 1;
        var surah = db.Surah(surahNum);
        int juz = QuranDatabase.JuzOfPage(page);

        var scheme = App.Theme.Scheme;
        var primary = ThemeService.ToColor(scheme.Primary);
        var deeper = ThemeService.ToColor(Core.Sky.Argb.Lerp(scheme.Primary, Core.Sky.Argb.Hex(0xFF000000), scheme.IsDark ? 0.28f : 0.18f));
        var onPrimary = Kit.B("SirajOnPrimaryBrush");
        var onPrimarySoft = new SolidColorBrush(ColorHelper.FromArgb(208,
            ((SolidColorBrush)onPrimary).Color.R, ((SolidColorBrush)onPrimary).Color.G, ((SolidColorBrush)onPrimary).Color.B));

        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = new Border
        {
            Width = 52, Height = 52, CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(ColorHelper.FromArgb(40, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center,
            Child = SirajVectorIcon.Make("quick_quran", 24, onPrimary),
        };
        grid.Children.Add(badge);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 3 };
        info.Children.Add(Kit.Text(loc["quran_continue_reading"], "SirajCaption", 12, bold: true, brush: onPrimarySoft));
        var surahTitle = Kit.Text(loc.IsRtl ? surah.Name : $"{surah.EnglishName} · {surah.Name}", "SirajHeading", 22, bold: true, brush: onPrimary);
        info.Children.Add(surahTitle);
        string meta = loc.Format("quran_index_position",
            loc.Format("quran_ayah_label", loc.Number(ayahNum)),
            loc.Number(page),
            loc.Number(juz));
        info.Children.Add(Kit.Text(meta, "SirajCaption", 12.5, brush: onPrimarySoft));
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        var arrowCircle = new Border
        {
            Width = 44, Height = 44, CornerRadius = new CornerRadius(22),
            Background = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Kit.Icon(loc.IsRtl ? "arrow_back" : "arrow_forward", 19, new SolidColorBrush(primary)),
        };
        Grid.SetColumn(arrowCircle, 2);
        grid.Children.Add(arrowCircle);

        var grad = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = primary, Offset = 0 },
                new GradientStop { Color = deeper, Offset = 1 },
            },
        };
        var btn = new Button
        {
            Content = grid,
            Background = grad,
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(72, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(20, 18, 20, 18),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        btn.Resources["ButtonBackground"] = Kit.Clone(grad);
        btn.Resources["ButtonBackgroundPointerOver"] = Kit.Clone(grad);
        btn.Resources["ButtonBackgroundPressed"] = Kit.Clone(grad);
        btn.Resources["ButtonBorderBrush"] = new SolidColorBrush(ColorHelper.FromArgb(72, 255, 255, 255));
        btn.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(ColorHelper.FromArgb(130, 255, 255, 255));
        btn.Click += (_, _) => Open(new ReaderArgs(page));
        AutomationProperties.SetName(btn, $"{loc["quran_continue_reading"]}: {surah.EnglishName}");
        Kit.Lift(btn);
        ContinueSlot.Content = btn;
    }

    private void BuildKhatmaCard()
    {
        var loc = Loc.Current;
        var khatma = App.Journal.Current.Khatma;

        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(Kit.IconBadge("khatma", 44, 22));

        var info = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        if (khatma != null)
        {
            var summary = KhatmaPlanner.Summarize(khatma, DateOnly.FromDateTime(DateTime.Now));
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            titleRow.Children.Add(Kit.Text(loc["kh_card_title"], "SirajHeading", 15, bold: true));
            string badgeText = summary.IsFinished
                ? loc["kh_finished_title"]
                : loc.Format("kh_day_n", loc.Number(summary.TodayPortion.DayNumber));
            titleRow.Children.Add(Kit.Pill(badgeText, "SirajPrimarySoftBrush", "SirajPrimaryBrush"));
            info.Children.Add(titleRow);

            string detail = summary.IsFinished
                ? loc["kh_finished_body"]
                : loc.Format("kh_card_today", loc.Format("kh_pages_range", loc.Number(summary.TodayPortion.FromPage), loc.Number(summary.TodayPortion.ToPage)));
            info.Children.Add(Kit.Text(detail, "SirajCaption"));

            var barRow = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 3, 0, 0) };
            barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            barRow.Children.Add(Kit.Bar(summary.PagesRead, summary.TotalPages));
            var pct = Kit.Text(loc.Format("kh_progress", loc.Number(summary.PagesRead), loc.Number(summary.TotalPages)), "SirajCaption", 11.5);
            Grid.SetColumn(pct, 1);
            barRow.Children.Add(pct);
            info.Children.Add(barRow);
        }
        else
        {
            info.Children.Add(Kit.Text(loc["kh_card_title"], "SirajHeading", 15, bold: true));
            info.Children.Add(Kit.Text(loc["kh_card_empty"], "SirajCaption"));
        }
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        var chevron = Kit.Icon(loc.IsRtl ? "chevron_left" : "chevron_right", 18, Kit.B("SirajOnSurfaceVariantBrush"));
        chevron.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(chevron, 2);
        grid.Children.Add(chevron);

        KhatmaSlot.Content = Kit.CardButton(grid, () => App.MainWindowInstance?.Navigate("khatma", drillIn: true),
            name: loc["kh_card_title"], padding: new Thickness(18, 14, 18, 14));
    }

    private void OnDownload(object sender, RoutedEventArgs e) => App.Quran.StartDownload();
    private void OnCancel(object sender, RoutedEventArgs e) => App.Quran.CancelDownload();
    private static void Open(ReaderArgs args) => App.MainWindowInstance?.OpenReader(args);

    private void ShowTab()
    {
        if (App.Quran.Status != QuranService.State.Ready) return;
        List.Items.Clear();
        if (_tab == IndexTab.Juz)
        {
            foreach (var row in JuzRows()) List.Items.Add(row);
        }
        else if (_tab == IndexTab.Bookmarks)
        {
            var rows = BookmarkRows().ToList();
            if (rows.Count == 0)
            {
                List.Items.Add(Kit.Card(Kit.Text(Loc.Current["quran_no_bookmarks"], "SirajCaption")));
            }
            else foreach (var row in rows) List.Items.Add(row);
        }
        else
        {
            foreach (var row in SurahRows(App.Quran.Database.Surahs())) List.Items.Add(row);
        }
    }

    private static Border Row(object target, UIElement leading, string title, string? subtitle, string trailing)
    {
        var end = Ui.Text(trailing, "SirajCaption");
        return Row(target, [title, subtitle, trailing], leading, Ui.Text(title, "SirajHeading"),
            string.IsNullOrEmpty(subtitle) ? null : Ui.Text(subtitle, "SirajCaption"), end);
    }

    private static Border Row(object target, string?[] spoken, UIElement leading, UIElement title, UIElement? subtitle, FrameworkElement trailing)
    {
        var grid = new Grid { ColumnSpacing = 14, Padding = new Thickness(16, 10, 18, 10), MinHeight = 64 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(leading);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
        text.Children.Add(title);
        if (subtitle != null) text.Children.Add(subtitle);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        trailing.VerticalAlignment = VerticalAlignment.Center;
        trailing.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(trailing, 2);
        grid.Children.Add(trailing);
        var row = new Border
        {
            Child = grid, Tag = target, CornerRadius = new CornerRadius(18),
            Background = Kit.B("SirajGlassCardBrush"), BorderBrush = Kit.B("SirajGlassBorderBrush"), BorderThickness = new Thickness(1),
        };
        AutomationProperties.SetName(row, string.Join(", ", spoken.Where(t => !string.IsNullOrEmpty(t))));
        return row;
    }

    private static TextBlock ArabicName(string name, double size) => new()
    {
        Text = name, FontFamily = (FontFamily)Application.Current.Resources["SirajArabicFont"], FontSize = size,
        FontWeight = FontWeights.Bold, Foreground = Ui.Brush("SirajOnSurfaceBrush"), TextTrimming = TextTrimming.CharacterEllipsis,
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight, LineHeight = size * 1.35,
    };

    private static UIElement Revelation(string text, bool meccan)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        var fg = Kit.B(meccan ? "SirajPrimaryBrush" : "SirajOnSurfaceVariantBrush");
        row.Children.Add(SirajVectorIcon.Make(meccan ? "kaaba" : "masjid", 11, fg));
        var label = Kit.Text(text, "SirajCaption", 11.5, bold: true, brush: fg);
        row.Children.Add(label);
        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Kit.B(meccan ? "SirajPrimarySoftBrush" : "SirajSurfaceContainerHighBrush"),
            Padding = new Thickness(7, 2, 7, 3),
            Child = row,
        };
    }

    private static UIElement Medallion(string text)
    {
        var grid = new Grid { Width = 42, Height = 42, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(new Border
        {
            Width = 30, Height = 30, CornerRadius = new CornerRadius(6),
            Background = Kit.B("SirajPrimarySoftBrush"), BorderBrush = Kit.B("SirajPrimaryBorderBrush"), BorderThickness = new Thickness(1),
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5), RenderTransform = new RotateTransform { Angle = 45 },
        });
        grid.Children.Add(new Border
        {
            Width = 30, Height = 30, CornerRadius = new CornerRadius(6),
            Background = Kit.B("SirajPrimarySoftBrush"), BorderBrush = Kit.B("SirajPrimaryBorderBrush"), BorderThickness = new Thickness(1),
        });
        grid.Children.Add(new TextBlock
        {
            Text = text, FontSize = 12.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Foreground = Kit.B("SirajPrimaryBrush"),
            FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
        });
        return grid;
    }

    private static IEnumerable<Border> SurahRows(IEnumerable<Surah> surahs)
    {
        var loc = Loc.Current;
        var db = App.Quran.Database;
        foreach (var s in surahs)
        {
            string kind = loc[s.IsMeccan ? "quran_polish_meccan" : "quran_polish_medinan"];
            string ayahs = loc.Format("quran_index_ayah_count", loc.Number(s.AyahCount));
            int page = db.PageOf(s.Number, 1);
            string pageText = loc.Format("quran_index_page_short", loc.Number(page));

            var title = loc.IsRtl ? ArabicName(s.Name, 21) : Ui.Text(s.EnglishName, "SirajHeading");
            var detail = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            detail.Children.Add(Revelation(kind, s.IsMeccan));
            var where = Ui.Text(pageText, "SirajCaption");
            where.VerticalAlignment = VerticalAlignment.Center;
            detail.Children.Add(where);

            var end = new StackPanel { Spacing = 1 };
            if (!loc.IsRtl)
            {
                var arabic = ArabicName(s.Name, 21);
                arabic.Foreground = Kit.B("SirajPrimaryBrush");
                arabic.HorizontalAlignment = HorizontalAlignment.Right;
                end.Children.Add(arabic);
            }
            var count = Ui.Text(ayahs, "SirajCaption");
            count.HorizontalAlignment = HorizontalAlignment.Right;
            end.Children.Add(count);

            yield return Row(new ReaderArgs(page), [loc.IsRtl ? s.Name : s.EnglishName, kind, pageText, loc.IsRtl ? null : s.Name, ayahs],
                Medallion(loc.Number(s.Number)), title, detail, end);
        }
    }

    private static IEnumerable<Border> JuzRows() => Enumerable.Range(1, 30).Select(JuzRow);

    private static Border JuzRow(int juz)
    {
        var loc = Loc.Current;
        var db = App.Quran.Database;
        var ayahs = db.Ayahs();
        var starts = db.JuzStarts();
        string Where(Ayah ayah) => $"{(loc.IsRtl ? db.Surah(ayah.Surah).Name : db.Surah(ayah.Surah).EnglishName)} {loc.Number(ayah.NumberInSurah)}";
        var first = starts[juz - 1].First;
        var last = juz < 30 ? ayahs[starts[juz].First.Number - 2] : ayahs[^1];
        int page = first.Page, lastPage = last.Page;

        bool arabic = loc.Language == "ar";
        var title = arabic ? ArabicName(Ajzaa.ArabicOrdinal(juz), 19) : Ui.Text(loc.Format("quran_index_juz_title", loc.Number(juz)), "SirajHeading");
        string range = $"{Where(first)}{(loc.IsRtl ? " ← " : " → ")}{Where(last)}";
        var detail = new StackPanel { Spacing = 6 };
        detail.Children.Add(Ui.Text(range, "SirajCaption"));
        var hizbs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var spokenHizbs = new List<string>();
        foreach (int hizb in new[] { juz * 2 - 1, juz * 2 })
        {
            int hizbPage = Ajzaa.HizbStartPage(hizb);
            string label = loc.Format("quran_index_hizb", loc.Number(hizb), loc.Number(hizbPage));
            hizbs.Children.Add(HizbButton(label, hizbPage));
            spokenHizbs.Add(label);
        }
        detail.Children.Add(hizbs);

        var end = new StackPanel { Spacing = 1 };
        var opening = ArabicName(Ajzaa.OpeningWords(juz), 20);
        opening.FontWeight = FontWeights.Normal;
        opening.Foreground = Kit.B("SirajPrimaryBrush");
        opening.HorizontalAlignment = HorizontalAlignment.Right;
        end.Children.Add(opening);
        string pages = loc.Format("quran_index_page_range", loc.Number(page), loc.Number(lastPage));
        var pagesText = Ui.Text(pages, "SirajCaption");
        pagesText.HorizontalAlignment = HorizontalAlignment.Right;
        end.Children.Add(pagesText);

        string name = arabic ? Ajzaa.ArabicOrdinal(juz) : loc.Format("quran_index_juz_title", loc.Number(juz));
        return Row(new ReaderArgs(page, (first.Surah, first.NumberInSurah)),
            [name, Ajzaa.OpeningWords(juz), range, pages, .. spokenHizbs],
            Medallion(loc.Number(juz)), title, detail, end);
    }

    private static Button HizbButton(string label, int page)
    {
        var text = Ui.Text(label, "SirajCaption");
        text.FontSize = 11.5;
        text.FontWeight = FontWeights.SemiBold;
        var button = new Button
        {
            Content = text, Padding = new Thickness(10, 3, 10, 4), MinHeight = 0, CornerRadius = new CornerRadius(10),
            Background = Kit.B("SirajGlassPillBrush"), BorderBrush = Kit.B("SirajGlassBorderBrush"), BorderThickness = new Thickness(1),
        };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => Open(new ReaderArgs(page));
        return button;
    }

    private static IEnumerable<Border> BookmarkRows()
    {
        var loc = Loc.Current;
        var db = App.Quran.Database;
        bool eastern = Digits.UseEastern(App.Settings.Current.NumeralSystem, loc.Language);
        foreach (var b in App.Settings.Current.Bookmarks)
        {
            var surah = db.Surah(b.Surah);
            var icon = new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(13),
                Background = Kit.B("SirajGoldSoftBrush"),
                Child = Kit.Icon("bookmark", 17, Kit.B("SirajGoldBrush")),
            };
            yield return Row(new ReaderArgs(b.Page, (b.Surah, b.Ayah)), icon,
                $"{(loc.IsRtl ? surah.Name : surah.EnglishName)} {loc.Number(b.Surah)}:{loc.Number(b.Ayah)}",
                HijriText.Gregorian(DateOnly.FromDateTime(b.Added.ToLocalTime().DateTime), loc.Language, eastern, "d MMMM yyyy"),
                loc.Format("quran_index_page_short", loc.Number(b.Page)));
        }
    }

    private static Border AyahRow(SearchHit hit)
    {
        var loc = Loc.Current;
        var db = App.Quran.Database;
        var surah = db.Surah(hit.Ayah.Surah);
        var text = new TextBlock
        {
            FontFamily = new FontFamily("ms-appx:///Assets/Quran/quran_digitalkhatt_v2.otf#DigitalKhatt New Madina"),
            FontSize = 22, TextWrapping = TextWrapping.Wrap, FlowDirection = FlowDirection.RightToLeft, LineHeight = 46,
            Foreground = Kit.B("SirajOnSurfaceBrush"),
        };
        var (display, found) = DigitalKhattText.ForDisplay(hit.Ayah.TextUthmani, hit.Ranges);
        int at = 0;
        foreach (var (start, end) in found.OrderBy(r => r.Start))
        {
            if (start < at) continue;
            if (start > at) text.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = display[at..start] });
            text.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = display[start..end], Foreground = Kit.B("SirajPrimaryBrush") });
            at = end;
        }
        if (at < display.Length) text.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = display[at..] });
        var reference = Ui.Text(loc.Format("quran_index_ayah_result_label", loc.IsRtl ? surah.Name : surah.EnglishName, loc.Number(hit.Ayah.NumberInSurah)), "SirajCaption");
        var stack = new StackPanel { Spacing = 4, Padding = new Thickness(18, 12, 18, 12) };
        stack.Children.Add(text);
        stack.Children.Add(reference);
        return new Border
        {
            Child = stack, Tag = new ReaderArgs(hit.Ayah.Page, (hit.Ayah.Surah, hit.Ayah.NumberInSurah)),
            CornerRadius = new CornerRadius(18),
            Background = Kit.B("SirajGlassCardBrush"), BorderBrush = Kit.B("SirajGlassBorderBrush"), BorderThickness = new Thickness(1),
        };
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FrameworkElement { Tag: ReaderArgs args }) Open(args);
    }

    private void OnSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) _ = Search(sender.Text);
    }

    private void OnSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) => _ = Search(args.QueryText);

    private async Task Search(string query)
    {
        _search?.Cancel();
        var cancel = (_search = new CancellationTokenSource()).Token;
        if (string.IsNullOrWhiteSpace(query)) { ShowTab(); return; }
        var db = App.Quran.Database;
        await Task.Delay(180, cancel).ContinueWith(_ => { });
        if (cancel.IsCancellationRequested) return;

        var rows = new List<UIElement>();
        var trimmed = Digits.ToWestern(query.Trim());
        if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
        {
            if (number is >= 1 and <= 114) rows.AddRange(SurahRows(new[] { db.Surah(number) }));
            if (number is >= 1 and <= QuranDatabase.PageCount) rows.Add(PageRow(number));
        }
        else
        {
            if (QuranQuery.Ayah(query, db.Surahs()) is { } named) rows.Add(AyahRow(new SearchHit(db.Ayah(named.Surah.Number, named.Ayah), [])));
            if (QuranQuery.Juz(query) is int juz) rows.Add(JuzRow(juz));
            if (QuranQuery.Page(query) is int page) rows.Add(PageRow(page));
            if (rows.Count == 0) await FindWords(query, rows, cancel);
        }
        if (cancel.IsCancellationRequested) return;
        List.Items.Clear();
        foreach (var row in rows) List.Items.Add(row);
    }

    private static async Task FindWords(string query, List<UIElement> rows, CancellationToken cancel)
    {
        var loc = Loc.Current;
        var db = App.Quran.Database;
        var folded = ArabicSearch.NormalizeQuery(query);
        if (QuranQuery.Revelation(query) is bool meccan)
        {
            rows.AddRange(SurahRows(db.Surahs().Where(s => s.IsMeccan == meccan).ToList()));
            return;
        }
        var surahs = db.Surahs().Where(s =>
            SurahNames.Matches(s, query) || ArabicSearch.Normalize(s.Name).Contains(folded, StringComparison.Ordinal)).ToList();
        rows.AddRange(SurahRows(surahs));
        if (folded.Length < ArabicSearch.MinQueryLength)
        {
            if (rows.Count == 0) rows.Add(Ui.Text(loc.Format("quran_index_query_too_short", loc.Number(ArabicSearch.MinQueryLength)), "SirajCaption"));
            return;
        }
        var hits = await Task.Run(() => db.Search(query, 200), cancel).ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : []);
        if (cancel.IsCancellationRequested) return;
        if (hits.Count > 0)
        {
            rows.Add(Ui.Section(loc["quran_index_section_ayat"]));
            rows.AddRange(hits.Select(AyahRow));
            if (hits.Count >= 200) rows.Add(Ui.Text(loc.Format("quran_index_results_capped", loc.Number(200)), "SirajCaption"));
        }
        else if (rows.Count == 0)
        {
            rows.Add(Ui.Text(loc["quran_index_no_ayat"], "SirajCaption"));
        }
    }

    private static Border PageRow(int page)
    {
        var loc = Loc.Current;
        return Row(new ReaderArgs(page), Medallion(loc.Number(page)), loc.Format("quran_page_label", loc.Number(page)), null, "");
    }
}
