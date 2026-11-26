using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Controls;
using Siraj.App.Services;
using Siraj.Core.Devotions;
using Windows.System;

namespace Siraj.App.Views;

public sealed partial class TasbihPage : Page
{
    private Ring? _ring;
    private TextBlock? _count;
    private TextBlock? _goalText;
    private TextBlock? _today;
    private TextBlock? _reached;
    private Button? _counter;
    private DispatcherQueueTimerHolder? _soundOff;

    public TasbihPage()
    {
        InitializeComponent();
        Build();
        Loaded += (_, _) => _counter?.Focus(FocusState.Programmatic);
        KeyDown += OnKey;
    }

    private static TasbihState State => App.Journal.Current.Tasbih;

    private static TasbihPhrase[] Phrases => [.. TasbihPhrases.BuiltIn, .. State.Custom];

    private static TasbihPhrase Phrase => Phrases[Math.Clamp(State.Phrase, 0, Phrases.Length - 1)];

    private static int Goal => State.Goals.TryGetValue(State.Phrase, out var g) && g > 0 ? g : Phrase.DefaultGoal;

    private static string TodayKey => Fasting.Key(App.Clock.Place?.Today(App.Clock.Now) ?? DateOnly.FromDateTime(App.Clock.Now.LocalDateTime));

    private void OnKey(object sender, KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is TextBox or NumberBox) return;
        if (e.Key is VirtualKey.Space or VirtualKey.Enter or VirtualKey.Add)
        {
            Count();
            e.Handled = true;
        }
    }

    private void Build()
    {
        var loc = Loc.Current;
        var (root, body) = Kit.Page(loc["tasbih_title"], loc["hub_tasbih_desc"],
            back: () => App.MainWindowInstance?.Navigate("worship"));
        var grid = new Grid { ColumnSpacing = 20, RowSpacing = 20 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var counter = CounterCard();
        var side = SideCards();
        grid.Children.Add(counter);
        grid.Children.Add(side);
        void Arrange(double width)
        {
            bool wide = width >= 820;
            grid.ColumnDefinitions[1].Width = wide ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
            Grid.SetColumn(side, wide ? 1 : 0);
            Grid.SetRow(side, wide ? 0 : 1);
        }
        grid.SizeChanged += (_, e) => Arrange(e.NewSize.Width);
        body.Children.Add(grid);
        Host.Children.Clear();
        Host.Children.Add(root);
        Refresh();
    }

    private UIElement CounterCard()
    {
        var loc = Loc.Current;
        var stack = new StackPanel { Spacing = 14, HorizontalAlignment = HorizontalAlignment.Stretch };

        // The phrase, with the arrows to the one before and after.
        var phraseRow = new Grid { ColumnSpacing = 8 };
        phraseRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        phraseRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        phraseRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var words = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        var arabic = Kit.Arabic(Phrase.Arabic, 34, align: TextAlignment.Center);
        arabic.HorizontalAlignment = HorizontalAlignment.Center;
        words.Children.Add(arabic);
        if (loc.Language != "ar" && Phrase.Transliteration.Length > 0)
        {
            var tr = Kit.Text(Phrase.Transliteration, "SirajHeading");
            tr.HorizontalTextAlignment = TextAlignment.Center;
            words.Children.Add(tr);
        }
        if (Phrase.Meaning(loc.Language) is { Length: > 0 } meaning)
        {
            var m = Kit.Text(meaning, "SirajCaption");
            m.HorizontalTextAlignment = TextAlignment.Center;
            words.Children.Add(m);
        }
        Grid.SetColumn(words, 1);
        phraseRow.Children.Add(words);
        var prev = Round(loc.IsRtl ? "" : "", loc["tools_tasbih_choose_dhikr"], () => Step(-1));
        var next = Round(loc.IsRtl ? "" : "", loc["tools_tasbih_choose_dhikr"], () => Step(1));
        Grid.SetColumn(next, 2);
        phraseRow.Children.Add(prev);
        phraseRow.Children.Add(next);
        stack.Children.Add(phraseRow);

        // The ring, which is the button: ambient inner halo + smooth animated progress arc.
        var ringWrap = new Grid { Width = 272, Height = 272 };
        var scheme = App.Theme.Scheme;
        ringWrap.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = 232, Height = 232,
            Fill = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop { Color = ThemeService.ToColor(scheme.Primary, scheme.IsDark ? 0.20f : 0.12f), Offset = 0 },
                    new GradientStop { Color = ThemeService.ToColor(scheme.Primary, 0f), Offset = 1 },
                },
            },
            IsHitTestVisible = false,
        });
        _ring = new Ring
        {
            Width = 260, Height = 260, Thickness = 14, Smooth = true,
            Track = new SolidColorBrush(ThemeService.ToColor(scheme.Primary, scheme.IsDark ? 0.16f : 0.11f)),
        };
        var centre = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 0 };
        _count = new TextBlock
        {
            FontSize = 72, FontWeight = FontWeights.SemiBold, FontFamily = (FontFamily)Application.Current.Resources["SirajFont"],
            HorizontalAlignment = HorizontalAlignment.Center, Foreground = Kit.B("SirajOnSurfaceBrush"),
        };
        _goalText = Kit.Text("", "SirajCaption", 14);
        _goalText.HorizontalAlignment = HorizontalAlignment.Center;
        centre.Children.Add(_count);
        centre.Children.Add(_goalText);
        _ring.Children.Add(centre);
        ringWrap.Children.Add(_ring);
        _counter = new Button
        {
            Content = ringWrap, Padding = new Thickness(6), CornerRadius = new CornerRadius(150), HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0),
            UseSystemFocusVisuals = false,
        };
        _counter.Resources["ButtonBackgroundPointerOver"] = Kit.Clone("SirajPrimarySoftBrush");
        _counter.Resources["ButtonBackgroundPressed"] = Kit.Clone("SirajPrimaryBorderBrush");
        _counter.Click += (_, _) => Count();
        stack.Children.Add(_counter);

        _reached = Kit.Text(loc["tasbih_goal_reached"], "SirajHeading", brush: Kit.B("SirajPrimaryBrush"));
        _reached.HorizontalAlignment = HorizontalAlignment.Center;
        _reached.Opacity = 0;
        stack.Children.Add(_reached);

        var hint = Kit.Text(loc["win_tasbih_keys"], "SirajCaption");
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.HorizontalTextAlignment = TextAlignment.Center;
        stack.Children.Add(hint);

        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
        tools.Children.Add(Round("", loc["tools_tasbih_reset"], async () =>
        {
            if (!await Kit.Confirm(XamlRoot, loc["tasbih_reset_title"], loc["tools_tasbih_reset_body"], loc["tools_tasbih_reset_confirm"])) return;
            App.Journal.Update(j => j.Tasbih.Count = 0);
            Refresh();
        }));
        tools.Children.Add(SoundButton());
        stack.Children.Add(tools);
        return Kit.Card(stack, new Thickness(24, 24, 24, 24));
    }

    private Button SoundButton()
    {
        var loc = Loc.Current;
        var icon = Kit.Icon(State.Sound ? "" : "", 16, Kit.B("SirajOnSurfaceBrush"));
        var button = Round("", "", () => { });
        button.Content = icon;
        void Label() => AutomationProperties.SetName(button, State.Sound ? loc["tools_tasbih_sound_off"] : loc["tools_tasbih_sound_on"]);
        Label();
        ToolTipService.SetToolTip(button, AutomationProperties.GetName(button));
        button.Click += (_, _) =>
        {
            App.Journal.Update(j => j.Tasbih.Sound = !j.Tasbih.Sound);
            icon.Glyph = State.Sound ? "" : "";
            Label();
            ToolTipService.SetToolTip(button, AutomationProperties.GetName(button));
        };
        return button;
    }

    private static Button Round(string glyph, string name, Action click)
    {
        var button = new Button
        {
            Content = Kit.Icon(glyph, 16, Kit.B("SirajOnSurfaceBrush")), Width = 44, Height = 44, Padding = new Thickness(0),
            CornerRadius = new CornerRadius(22), VerticalAlignment = VerticalAlignment.Center,
            Background = Kit.B("SirajSurfaceContainerHighBrush"), BorderThickness = new Thickness(0),
        };
        if (name.Length > 0)
        {
            AutomationProperties.SetName(button, name);
            ToolTipService.SetToolTip(button, name);
        }
        button.Click += (_, _) => click();
        return button;
    }

    private FrameworkElement SideCards()
    {
        var loc = Loc.Current;
        var side = new StackPanel { Spacing = 16 };

        // Today, and the goal for this phrase.
        var stats = new StackPanel { Spacing = 12 };
        _today = Kit.Text("", "SirajTitle", 22, brush: Kit.B("SirajOnPrimaryContainerBrush"));
        stats.Children.Add(_today);
        var goals = new List<(int, string)> { (33, loc.Number(33)), (34, loc.Number(34)), (99, loc.Number(99)), (100, loc.Number(100)), (1000, loc.Number(1000)) };
        var goalRow = new StackPanel { Spacing = 6 };
        goalRow.Children.Add(Kit.Text(loc["win_tasbih_goal"], "SirajCaption", brush: Kit.B("SirajOnPrimaryContainerBrush")));
        goalRow.Children.Add(new ScrollViewer
        {
            Content = Ui.Segments(goals, Goal, g =>
            {
                App.Journal.Update(j => j.Tasbih.Goals[j.Tasbih.Phrase] = g);
                Refresh();
            }),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled,
        });
        stats.Children.Add(goalRow);
        side.Children.Add(Kit.Accent(stats, new Thickness(22)));

        // Every phrase, the chosen one marked.
        var list = new StackPanel { Spacing = 2 };
        list.Children.Add(Kit.Text(loc["tools_tasbih_choose_dhikr"], "SirajHeading"));
        var phrases = Phrases;
        for (int i = 0; i < phrases.Length; i++)
        {
            int index = i;
            var p = phrases[i];
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var texts = new StackPanel { Spacing = 0 };
            var ar = Kit.Arabic(p.Arabic, 19, align: loc.IsRtl ? TextAlignment.Right : TextAlignment.Left);
            ar.LineHeight = 34;
            texts.Children.Add(ar);
            if (loc.Language != "ar" && p.Transliteration.Length > 0) texts.Children.Add(Kit.Text(p.Transliteration, "SirajCaption"));
            row.Children.Add(texts);
            if (index == State.Phrase)
            {
                var check = Kit.Icon("", 14);
                check.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(check, 1);
                row.Children.Add(check);
            }
            var button = Kit.CardButton(row, () => Select(index), p.Transliteration.Length > 0 ? p.Transliteration : p.Arabic,
                index == State.Phrase ? "SirajSecondaryContainerBrush" : "SirajSurfaceBrush", new Thickness(14, 6, 14, 8));
            button.BorderThickness = new Thickness(0);
            button.Margin = new Thickness(0, 2, 0, 2);
            list.Children.Add(button);
        }

        // The reader's own.
        var add = new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
        var box = new TextBox
        {
            PlaceholderText = loc["tasbih_arabic_label"], FlowDirection = FlowDirection.RightToLeft, CornerRadius = new CornerRadius(12),
            FontFamily = (FontFamily)Application.Current.Resources["SirajArabicFont"], FontSize = 18, MaxLength = 200,
        };
        AutomationProperties.SetName(box, loc["tasbih_arabic_label"]);
        var addButton = Ui.Button(loc["tasbih_add_custom"], () =>
        {
            var text = box.Text.Trim();
            if (text.Length == 0) return;
            App.Journal.Update(j =>
            {
                j.Tasbih.Custom.Add(new TasbihPhrase { Arabic = text, DefaultGoal = 33, IsCustom = true });
                j.Tasbih.Phrase = TasbihPhrases.BuiltIn.Count + j.Tasbih.Custom.Count - 1;
                j.Tasbih.Count = 0;
            });
            Build();
        });
        add.Children.Add(box);
        add.Children.Add(addButton);
        list.Children.Add(add);
        side.Children.Add(Kit.Card(list, new Thickness(16, 18, 16, 18)));
        return side;
    }

    private void Select(int index)
    {
        if (index == State.Phrase) return;
        App.Journal.Update(j => { j.Tasbih.Phrase = index; j.Tasbih.Count = 0; });
        Build();
    }

    private void Step(int by) => Select(((State.Phrase + by) % Phrases.Length + Phrases.Length) % Phrases.Length);

    private void Count()
    {
        var (next, completed) = TasbihPhrases.Increment(State.Count, Goal);
        var today = TodayKey;
        App.Journal.Update(j =>
        {
            j.Tasbih.Count = next;
            if (j.Tasbih.Day != today) { j.Tasbih.Day = today; j.Tasbih.Today = 0; }
            j.Tasbih.Today++;
            j.Tasbih.Total++;
        });
        Refresh();
        if (_ring != null) Kit.Pulse(_ring, completed ? 1.06f : 1.025f);
        if (_reached != null) _reached.Opacity = completed ? 1 : 0;
        if (State.Sound) Click(completed);
    }

    private void Click(bool completed)
    {
        ElementSoundPlayer.State = ElementSoundPlayerState.On;
        ElementSoundPlayer.Volume = AppPaths.Muted ? 0 : 0.6;
        ElementSoundPlayer.Play(completed ? ElementSoundKind.Show : ElementSoundKind.Invoke);
        _soundOff ??= new DispatcherQueueTimerHolder(DispatcherQueue);
        _soundOff.After(TimeSpan.FromMilliseconds(400), () => ElementSoundPlayer.State = ElementSoundPlayerState.Off);
    }

    private void Refresh()
    {
        var loc = Loc.Current;
        int goal = Goal;
        if (_count != null) _count.Text = loc.Number(State.Count);
        if (_goalText != null) _goalText.Text = loc.Format("azkar_progress_of", "", loc.Number(goal)).Trim();
        if (_ring != null) _ring.Fraction = goal > 0 ? (double)State.Count / goal : 0;
        int today = State.Day == TodayKey ? State.Today : 0;
        if (_today != null) _today.Text = loc.Format("tasbih_today", loc.Number(today));
        if (_counter != null)
            AutomationProperties.SetName(_counter, loc["tools_tasbih_count_action"] + ". " +
                loc.Format("tools_tasbih_counter_a11y", loc.Number(State.Count), loc.Number(goal)));
    }
}

internal sealed class DispatcherQueueTimerHolder(Microsoft.UI.Dispatching.DispatcherQueue queue)
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer = queue.CreateTimer();
    private Action? _action;

    public void After(TimeSpan delay, Action action)
    {
        _action = action;
        _timer.Stop();
        _timer.Interval = delay;
        _timer.IsRepeating = false;
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        _action?.Invoke();
    }
}
