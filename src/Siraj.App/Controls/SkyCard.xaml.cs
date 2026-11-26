using System.Diagnostics;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Native;
using Siraj.App.Services;
using Siraj.App.Sky;
using Siraj.Core.Hijri;
using Siraj.Core.Prayer;
using Siraj.Core.Sky;
using Siraj.Core.Text;
using Windows.Foundation;
using Windows.UI;

namespace Siraj.App.Controls;

public sealed partial class SkyCard : UserControl
{
    private const long SkyStepSeconds = 15;
    private const double BadgeRockRadPerSec = 2.0 * Math.PI / 7.0;
    private static readonly Argb MoonlitText = Argb.Hex(0xFFC8D6FF);
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private IDisposable? _seconds;
    private readonly double _born = Clock.Elapsed.TotalSeconds;
    private DispatcherQueueTimer? _frames;
    private readonly SkyGeometry _geo = new();
    private MoonPhase? _moon;
    private long _moonMinute = -1;
    private DateOnly _glyphMoonDate;
    private CanvasRenderTarget? _back, _front, _glass;
    private long _layersStep = -1;
    private uint _layersAccent;
    private bool _layersHasDay;
    private Size _layersSize;
    private string? _shownPrayer, _shownPhase;
    private bool _isSunIcon = true;
    private bool _pulsingPill;
    private bool _duringAzan;
    private Color _pillPulseBase = Color.FromArgb(255, 255, 112, 67);
    private bool? _lastDaytimeBrush;
    private uint _lastOrbTintKey;
    private uint _lastBorderKey;

    public SkyCard()
    {
        InitializeComponent();
        Kit.Lift(HijriCapsule);
        Kit.Lift(GregorianButton);
        AttachTextShadow(LabelShadowHost, LabelText, Color.FromArgb(140, 0, 0, 0), new Vector3(0, 2, 0), 12f);
        AttachTextShadow(NameShadowHost, NameText, Color.FromArgb(140, 0, 0, 0), new Vector3(0, 2, 0), 12f);
        AttachTextShadow(TimeShadowHost, PrayerTimeText, Color.FromArgb(140, 0, 0, 0), new Vector3(0, 2, 0), 12f);
        AttachTextShadow(GregorianShadowHost, GregorianText, Color.FromArgb(217, 0, 0, 0), new Vector3(0, 1, 0), 6f);
        AttachTextShadow(HijriShadowHost, HijriCapsuleText, Color.FromArgb(153, 0, 0, 0), new Vector3(0, 1, 0), 3f);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void AttachTextShadow(FrameworkElement host, TextBlock source, Color color, Vector3 offset, float blur)
    {
        SpriteVisual? sprite = null;
        void Ensure()
        {
            if (sprite == null)
            {
                var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;
                var shadow = compositor.CreateDropShadow();
                shadow.Color = color;
                shadow.Offset = offset;
                shadow.BlurRadius = blur;
                shadow.Mask = source.GetAlphaMask();
                sprite = compositor.CreateSpriteVisual();
                sprite.Shadow = shadow;
                ElementCompositionPreview.SetElementChildVisual(host, sprite);
            }
            sprite.Size = new Vector2((float)source.ActualWidth, (float)source.ActualHeight);
        }

        source.Loaded += (_, _) => Ensure();
        source.SizeChanged += (_, _) => Ensure();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _seconds = App.Clock.SubscribeSeconds();
        App.Clock.Tick += OnTick;
        App.Clock.DayChanged += OnTick;
        App.Theme.Changed += OnThemeChanged;
        XamlRoot.Changed += OnXamlRootChanged;
        Views.MainWindow.ActiveChanged += UpdateMotion;

        _frames = DispatcherQueue.CreateTimer();
        _frames.Interval = TimeSpan.FromMilliseconds(66);
        _frames.Tick += (_, _) =>
        {
            double t = Clock.Elapsed.TotalSeconds - _born;
            PhaseIconRotation.Angle = _isSunIcon ? (t * 6.0) % 360.0 : 9.0 * Math.Sin(t * BadgeRockRadPerSec);
            if (_pulsingPill)
            {
                // 1200ms reverse cycle between 0.35 and 0.85 alpha.
                double phase = (t % 2.4) / 1.2;
                if (phase > 1.0) phase = 2.0 - phase;
                double eased = phase * phase * (3.0 - 2.0 * phase);
                byte a = (byte)Math.Clamp((0.35 + 0.50 * eased) * 255.0, 0, 255);
                CountdownPill.BorderBrush = new SolidColorBrush(Color.FromArgb(a, _pillPulseBase.R, _pillPulseBase.G, _pillPulseBase.B));
            }
            if (_duringAzan)
            {
                double p = (t % 1.6) / 1.6;
                double radiate = 0.8 * (1.0 - p * p);
                AzanGlow.Opacity = radiate * 0.4;
            }
            Canvas.Invalidate();
        };

        UpdateMotion();
        _shownPrayer = _shownPhase = null;
        _lastDaytimeBrush = null;
        _lastBorderKey = 0;
        OnTick();
        Enter();
    }

    private void OnThemeChanged()
    {
        _lastDaytimeBrush = null;
        _lastBorderKey = 0;
        OnTick();
        Canvas.Invalidate();
    }

    private void Enter()
    {
        if (!Kit.Motion) return;
        Kit.Rise(LabelBadge, 0, 8, 180);
        Kit.Rise(NameHost, 30, 8, 180);
        Kit.Rise(TimeHost, 60, 8, 180);
        Kit.Rise(CountdownHost, 90, 8, 180);
        Kit.Rise(CalendarFooter, 120, 8, 180);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _seconds?.Dispose();
        _seconds = null;
        App.Clock.Tick -= OnTick;
        App.Clock.DayChanged -= OnTick;
        App.Theme.Changed -= OnThemeChanged;
        if (XamlRoot != null) XamlRoot.Changed -= OnXamlRootChanged;
        Views.MainWindow.ActiveChanged -= UpdateMotion;
        _frames?.Stop();
        _frames = null;
        _back?.Dispose();
        _front?.Dispose();
        _glass?.Dispose();
        _back = _front = _glass = null;
        _geo.Dispose();
        Canvas.RemoveFromVisualTree();
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateMotion();
    private static bool MotionWanted => !App.Settings.Current.ReduceMotion && Win32.AnimationsEnabled();

    private void UpdateMotion()
    {
        if (_frames == null) return;
        bool visible = XamlRoot?.IsHostVisible ?? false;
        if (visible && Views.MainWindow.IsActive && MotionWanted) _frames.Start();
        else { _frames.Stop(); Canvas.Invalidate(); }
    }

    private void OnTick()
    {
        var loc = Loc.Current;
        var clock = App.Clock;
        var now = clock.Now;
        var day = clock.Day;
        bool eastern = Digits.UseEastern(App.Settings.Current.NumeralSystem, loc.Language);
        bool isRtl = loc.IsRtl;
        var today = clock.Place?.Today(now) ?? DateOnly.FromDateTime(DateTime.Now);

        PhaseChip.FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        LabelText.CharacterSpacing = isRtl ? 0 : 45;
        GregorianText.CharacterSpacing = loc.Language == "ar" ? 0 : 25;

        // Calendar capsule dates and tonight's 21:00 moon phase.
        GregorianText.Text = HijriText.Gregorian(today, loc.Language, eastern);
        var hijri = clock.Hijri.Year == 0 ? HijriCalendars.Current.ToHijri(today) : clock.Hijri;
        HijriCapsuleText.Text = HijriText.Format(hijri, loc.Language, eastern);
        if (_glyphMoonDate != today)
        {
            _glyphMoonDate = today;
            var tonight = new DateTimeOffset(today.ToDateTime(new TimeOnly(21, 0)), now.Offset);
            HijriMoonLit.Data = BuildMoonGlyphGeometry(MoonPhase.At(tonight).PhaseFraction);
        }

        if (day == null)
        {
            PhaseChip.Visibility = Visibility.Collapsed;
            CountdownHost.Visibility = Visibility.Collapsed;
            LabelBadge.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            LabelText.Foreground = new SolidColorBrush(Color.FromArgb(217, 255, 255, 255));
            LabelText.Text = loc["location_required"];
            NameText.Text = loc["onboarding_loc_title"];
            PrayerTimeText.Text = "";
            LocationButton.Content = loc["dashboard_location_action"];
            LocationButton.Visibility = Visibility.Visible;
            Canvas.Invalidate();
            return;
        }
        LocationButton.Visibility = Visibility.Collapsed;
        PhaseChip.Visibility = Visibility.Visible;
        CountdownHost.Visibility = Visibility.Visible;

        var state = clock.State ?? PrayerState.At(day, now);
        var displayedPrayer = state switch
        {
            PrayerState.AzanCalled a => a.Prayer,
            PrayerState.WaitingForIqama w => w.Prayer,
            PrayerState.InPrayer p => p.Prayer,
            PrayerState.UpcomingAzan u => u.Prayer,
            _ => day.Next,
        };

        var arc = SolarArc.At(now, day.Times, displayedPrayer);
        var moment = SkyTimeline.At(now, day.Times);
        var skyTheme = SkyPalette.Tint(moment.Colors, App.Theme.Scheme.Primary);

        PhaseText.Text = loc[arc.PhaseKey];
        _isSunIcon = arc.IsDaytime;
        PhaseIcon.Glyph = _isSunIcon ? "\uE706" : "\uE708";
        PhaseIcon.Foreground = new SolidColorBrush(ThemeService.ToColor(skyTheme.OrbGlow));

        bool isDuringAzan = state is PrayerState.AzanCalled;
        bool isWaitingForIqama = state is PrayerState.WaitingForIqama;
        bool isInPrayer = state is PrayerState.InPrayer;
        bool isMaghrib = displayedPrayer?.Type == PrayerType.Maghrib;
        long diffMinutes = displayedPrayer != null ? (long)(displayedPrayer.Time - now).TotalMinutes : 999;
        bool isUrgent = !isDuringAzan && !isWaitingForIqama && !isInPrayer && diffMinutes is >= 1 and <= 15;
        bool isSoon = !isDuringAzan && !isWaitingForIqama && !isInPrayer && diffMinutes is >= 16 and <= 60;

        Color badgeBg = isDuringAzan ? Color.FromArgb(64, 255, 152, 0)
            : isWaitingForIqama ? Color.FromArgb(51, 255, 213, 79)
            : isInPrayer ? Color.FromArgb(56, 76, 175, 80)
            : Color.FromArgb(0, 0, 0, 0);

        Color badgeFg = isDuringAzan ? Color.FromArgb(255, 255, 183, 77)
            : isWaitingForIqama ? Color.FromArgb(255, 255, 224, 130)
            : isInPrayer ? Color.FromArgb(255, 129, 199, 132)
            : Color.FromArgb(217, 255, 255, 255);

        Color pillAccent = isDuringAzan ? Color.FromArgb(255, 255, 152, 0)
            : isWaitingForIqama ? Color.FromArgb(255, 229, 181, 88)
            : isInPrayer ? Color.FromArgb(255, 129, 199, 132)
            : isUrgent ? Color.FromArgb(255, 255, 112, 67)
            : isSoon ? Color.FromArgb(255, 255, 183, 77)
            : ThemeService.ToColor(skyTheme.CardBorderTop);

        Color pillBg = isDuringAzan ? Color.FromArgb(97, 230, 81, 0)
            : isWaitingForIqama ? Color.FromArgb(107, 62, 39, 35)
            : isInPrayer ? Color.FromArgb(97, 27, 94, 32)
            : isUrgent ? Color.FromArgb(82, 183, 28, 28)
            : isSoon ? Color.FromArgb(89, 66, 32, 6)
            : Color.FromArgb(97, 0, 0, 0);

        Color iconTint = isDuringAzan ? Color.FromArgb(255, 255, 183, 77)
            : isWaitingForIqama ? Color.FromArgb(255, 255, 213, 79)
            : isInPrayer ? Color.FromArgb(255, 129, 199, 132)
            : isUrgent ? Color.FromArgb(255, 255, 112, 67)
            : isSoon ? Color.FromArgb(255, 255, 213, 79)
            : Color.FromArgb(255, 229, 181, 88);

        double borderWidth = isDuringAzan || isUrgent ? 1.5
            : isWaitingForIqama || isInPrayer ? 1.2
            : 1.0;

        float pillAccentAlpha = isInPrayer ? 0.85f : isSoon ? 0.70f : 0.60f;
        _pulsingPill = isDuringAzan || isWaitingForIqama || isUrgent;
        _duringAzan = isDuringAzan;
        _pillPulseBase = pillAccent;
        AzanGlow.Visibility = isDuringAzan ? Visibility.Visible : Visibility.Collapsed;
        if (!isDuringAzan) AzanGlow.Opacity = 0;

        void SetCountdownPhrase(string? templateKey, string digits)
        {
            if (templateKey == null)
            {
                CountdownBefore.Text = digits;
                CountdownBefore.Visibility = Visibility.Visible;
                CountdownText.Visibility = Visibility.Collapsed;
                CountdownAfter.Visibility = Visibility.Collapsed;
                return;
            }
            CountdownText.Visibility = Visibility.Visible;
            const string slot = "§§";
            var phrase = loc.Format(templateKey, slot);
            int idx = phrase.IndexOf(slot, StringComparison.Ordinal);
            if (idx < 0)
            {
                CountdownBefore.Visibility = Visibility.Collapsed;
                CountdownAfter.Visibility = Visibility.Collapsed;
                CountdownText.Text = digits;
                return;
            }
            string before = phrase[..idx].Trim();
            string after = phrase[(idx + slot.Length)..].Trim();
            CountdownBefore.Text = before;
            CountdownBefore.Visibility = before.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            CountdownText.Text = digits;
            CountdownAfter.Text = after;
            CountdownAfter.Visibility = after.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        LabelText.Text = isDuringAzan && isMaghrib ? loc["azan_maghrib_now"]
            : isDuringAzan ? loc["adhan_badge"]
            : isWaitingForIqama ? loc["iqama"]
            : isInPrayer ? loc["in_prayer"]
            : loc["next_prayer"];

        if (displayedPrayer != null)
        {
            NameText.Text = isDuringAzan && isMaghrib ? loc["maghrib_prayer_now"] : loc.PrayerName(displayedPrayer);
            PrayerTimeText.Text = loc.Time(displayedPrayer.Time);
        }

        switch (state)
        {
            case PrayerState.AzanCalled:
                SetCountdownPhrase(null, loc["azan_time_now"]);
                break;
            case PrayerState.WaitingForIqama w:
                SetCountdownPhrase("until_iqama_format", TimeText.Countdown(w.UntilIqama, false, eastern, loc.Language));
                break;
            case PrayerState.InPrayer:
                SetCountdownPhrase(null, loc["prayer_in_progress"]);
                break;
            case PrayerState.UpcomingAzan u:
                SetCountdownPhrase("dashboard_countdown_format", TimeText.Countdown(u.Remaining, true, eastern, loc.Language));
                break;
        }

        LabelBadge.Background = new SolidColorBrush(badgeBg);
        LabelText.Foreground = new SolidColorBrush(badgeFg);
        CountdownPill.Background = new SolidColorBrush(pillBg);
        CountdownPill.BorderThickness = new Thickness(borderWidth);
        if (_pulsingPill)
        {
            if (_frames == null || !_frames.IsRunning)
                CountdownPill.BorderBrush = new SolidColorBrush(Color.FromArgb(217, pillAccent.R, pillAccent.G, pillAccent.B));
        }
        else
        {
            byte midA = (byte)Math.Round(MathF.Min(pillAccentAlpha * 0.75f, 0.55f) * 255f);
            byte botA = (byte)Math.Round(MathF.Min(pillAccentAlpha * 0.25f, 0.20f) * 255f);
            CountdownPill.BorderBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(82, 255, 255, 255), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(midA, pillAccent.R, pillAccent.G, pillAccent.B), Offset = 0.5 },
                    new GradientStop { Color = Color.FromArgb(botA, pillAccent.R, pillAccent.G, pillAccent.B), Offset = 1 },
                },
            };
        }
        CountdownIcon.Foreground = new SolidColorBrush(iconTint);
        CountdownIcon.Glyph = isDuringAzan ? "\uEA8F" : "\uE916";

        var textLight = arc.IsDaytime ? Argb.Lerp(skyTheme.OrbCore, skyTheme.OrbGlow, 0.55f) : MoonlitText;
        uint tintKey = (uint)(textLight.R << 16 | textLight.G << 8 | textLight.B);
        if (_lastDaytimeBrush != arc.IsDaytime || _lastOrbTintKey != tintKey)
        {
            _lastDaytimeBrush = arc.IsDaytime;
            _lastOrbTintKey = tintKey;
            var white = Argb.Hex(0xFFFFFFFF);
            NameText.Foreground = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = ThemeService.ToColor(white), Offset = 0 },
                    new GradientStop { Color = ThemeService.ToColor(Argb.Lerp(white, textLight, 0.32f)), Offset = 1 },
                },
            };
            PrayerTimeText.Foreground = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = ThemeService.ToColor(white), Offset = 0 },
                    new GradientStop { Color = ThemeService.ToColor(white), Offset = 0.45 },
                    new GradientStop { Color = ThemeService.ToColor(Argb.Lerp(white, textLight, 0.55f)), Offset = 1 },
                },
            };
        }

        uint borderKey = (uint)(skyTheme.CardBorderTop.R << 16 | skyTheme.CardBorderTop.G << 8 | skyTheme.CardBorderTop.B)
            ^ ((uint)(skyTheme.CardBg.R << 16 | skyTheme.CardBg.G << 8 | skyTheme.CardBg.B) * 31u);
        if (borderKey != _lastBorderKey)
        {
            _lastBorderKey = borderKey;
            Card.Background = new SolidColorBrush(ThemeService.ToColor(skyTheme.CardBg));
            CardBorder.BorderBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = ThemeService.ToColor(skyTheme.CardBorderTop), Offset = 0 },
                    new GradientStop { Color = ThemeService.ToColor(skyTheme.CardBorderBottom), Offset = 1 },
                },
            };
            PhaseChip.BorderBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(82, 255, 255, 255), Offset = 0 },
                    new GradientStop { Color = ThemeService.ToColor(skyTheme.CardBorderTop, 0.40f), Offset = 0.5 },
                    new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 1 },
                },
            };
        }

        string prayer = LabelText.Text + "|" + NameText.Text;
        if (_shownPrayer != null && prayer != _shownPrayer) Kit.Rise(Heading, 0, 8, 200);
        _shownPrayer = prayer;
        if (_shownPhase != null && PhaseText.Text != _shownPhase) Kit.Rise(PhaseChip, 0, 5, 200);
        _shownPhase = PhaseText.Text;

        if (_frames == null || !_frames.IsRunning) Canvas.Invalidate();
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var clock = App.Clock;
        var now = clock.Now;
        var times = clock.Day?.Times ?? [];
        long minute = now.ToUnixTimeSeconds() / 60;
        long step = now.ToUnixTimeSeconds() / SkyStepSeconds;

        var accent = App.Theme.Scheme.Primary;
        uint accentKey = (uint)(accent.A << 24 | accent.R << 16 | accent.G << 8 | accent.B);
        var rawMoment = SkyTimeline.At(now, times);
        var moment = rawMoment with { Colors = SkyPalette.Tint(rawMoment.Colors, accent) };

        if (minute != _moonMinute || _moon == null)
        {
            _moon = MoonPhase.At(now);
            _moonMinute = minute;
        }

        var state = clock.Day != null ? (clock.State ?? PrayerState.At(clock.Day, now)) : null;
        var displayedPrayer = state switch
        {
            PrayerState.AzanCalled az => az.Prayer,
            PrayerState.WaitingForIqama iq => iq.Prayer,
            PrayerState.InPrayer ip => ip.Prayer,
            PrayerState.UpcomingAzan up => up.Prayer,
            _ => clock.Day?.Next,
        };

        var arc = SolarArc.At(now, times, displayedPrayer);
        float w = (float)sender.ActualWidth, h = (float)sender.ActualHeight;
        if (w < 1 || h < 1) return;

        _geo.Update(sender, w, h);

        bool hasDay = clock.Day != null;
        bool motion = _frames?.IsRunning ?? false;
        var still = new SkyFrame(moment, arc, _moon, 0, false);
        var size = new Size(w, h);

        if (_back == null || _front == null || _glass == null
            || step != _layersStep || accentKey != _layersAccent || hasDay != _layersHasDay
            || size != _layersSize || !ReferenceEquals(_back.Device, sender.Device))
        {
            _back?.Dispose();
            _front?.Dispose();
            _glass?.Dispose();
            _back = new CanvasRenderTarget(sender, w, h, sender.Dpi);
            _front = new CanvasRenderTarget(sender, w, h, sender.Dpi);
            _glass = new CanvasRenderTarget(sender, w, h, sender.Dpi);

            using (var ds = _back.CreateDrawingSession())
            {
                ds.Clear(ThemeService.ToColor(moment.Colors.CardBg));
                SkyRenderer.DrawBack(ds, sender, _geo, w, h, still, showArc: hasDay);
            }
            using (var ds = _front.CreateDrawingSession())
            {
                ds.Clear(Color.FromArgb(0, 0, 0, 0));
                SkyRenderer.DrawFront(ds, sender, _geo, w, h, still);
            }
            using (var ds = _glass.CreateDrawingSession())
            {
                ds.Clear(Color.FromArgb(0, 0, 0, 0));
                SkyRenderer.DrawGlass(ds, sender, _geo, w, h, still, cornerRadius: 32f);
            }

            _layersStep = step;
            _layersAccent = accentKey;
            _layersHasDay = hasDay;
            _layersSize = size;
        }

        var live = new SkyFrame(moment, arc, _moon, Clock.Elapsed.TotalSeconds - _born, motion);
        args.DrawingSession.DrawImage(_back);
        SkyRenderer.DrawLive(args.DrawingSession, sender, _geo, w, h, live, showArc: hasDay);
        args.DrawingSession.DrawImage(_front);
        SkyRenderer.DrawLiveForeground(args.DrawingSession, sender, _geo, w, h, live);
        args.DrawingSession.DrawImage(_glass);
    }

    private static Geometry? BuildMoonGlyphGeometry(float phaseFraction)
    {
        const double cx = 6.5, cy = 6.5, r = 6.0;
        if (phaseFraction is >= 0.47f and <= 0.53f)
            return new EllipseGeometry { Center = new Point(cx, cy), RadiusX = r, RadiusY = r };
        if (phaseFraction < 0.02f || phaseFraction > 0.98f)
            return null;

        double k = Math.Cos(phaseFraction * Math.Tau);
        bool isWaxing = phaseFraction is >= 0f and <= 0.5f;
        const double m = 0.55228475;
        double limbX = isWaxing ? r : -r;
        double rx = k * limbX;

        var fig = new PathFigure { StartPoint = new Point(cx, cy - r), IsClosed = true, IsFilled = true };
        fig.Segments.Add(new BezierSegment
        {
            Point1 = new Point(cx + limbX * m, cy - r),
            Point2 = new Point(cx + limbX, cy - r * m),
            Point3 = new Point(cx + limbX, cy),
        });
        fig.Segments.Add(new BezierSegment
        {
            Point1 = new Point(cx + limbX, cy + r * m),
            Point2 = new Point(cx + limbX * m, cy + r),
            Point3 = new Point(cx, cy + r),
        });

        bool isCrescent = (phaseFraction is >= 0.02f and <= 0.22f) || (phaseFraction is >= 0.78f and <= 0.98f);
        if (isCrescent && k > 0.05)
        {
            double innerRx = limbX * Math.Clamp(k * 0.90, 0.18, 0.85);
            fig.Segments.Add(new BezierSegment
            {
                Point1 = new Point(cx + innerRx * 0.22, cy + r * 0.68),
                Point2 = new Point(cx + innerRx, cy + r * 0.38),
                Point3 = new Point(cx + innerRx, cy),
            });
            fig.Segments.Add(new BezierSegment
            {
                Point1 = new Point(cx + innerRx, cy - r * 0.38),
                Point2 = new Point(cx + innerRx * 0.22, cy - r * 0.68),
                Point3 = new Point(cx, cy - r),
            });
        }
        else
        {
            fig.Segments.Add(new BezierSegment
            {
                Point1 = new Point(cx + rx * m, cy + r),
                Point2 = new Point(cx + rx, cy + r * m),
                Point3 = new Point(cx + rx, cy),
            });
            fig.Segments.Add(new BezierSegment
            {
                Point1 = new Point(cx + rx, cy - r * m),
                Point2 = new Point(cx + rx * m, cy - r),
                Point3 = new Point(cx, cy - r),
            });
        }

        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        return geo;
    }

    private void OnSetLocation(object sender, RoutedEventArgs e) => App.ShowMainWindow("settings");
    private void OnOpenCalendar(object sender, RoutedEventArgs e) => App.ShowMainWindow("calendar");
}
