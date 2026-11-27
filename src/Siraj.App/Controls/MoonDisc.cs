using System.Numerics;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Siraj.App.Services;
using Siraj.App.Sky;
using Siraj.Core.Hijri;
using Siraj.Core.Sky;

namespace Siraj.App.Controls;

internal sealed class MoonDisc : UserControl
{
    private CanvasControl? _canvas;
    private MoonPhase? _phase;

    public MoonDisc()
    {
        // Moon phase orientation is astronomical and must not mirror in RTL.
        FlowDirection = FlowDirection.LeftToRight;
        IsTabStop = false;
        Loaded += (_, _) =>
        {
            _canvas = new CanvasControl { ClearColor = Colors.Transparent };
            _canvas.Draw += OnDraw;
            Content = _canvas;
        };
        Unloaded += (_, _) =>
        {
            if (_canvas == null) return;
            _canvas.Draw -= OnDraw;
            _canvas.RemoveFromVisualTree();
            _canvas = null;
            Content = null;
        };
    }

    public MoonPhase? Phase
    {
        get => _phase;
        set
        {
            _phase = value;
            _canvas?.Invalidate();
        }
    }

    public static string NameKey(MoonPhase.PhaseName name) => name switch
    {
        MoonPhase.PhaseName.New => "moon_new",
        MoonPhase.PhaseName.WaxingCrescent => "moon_waxing_crescent",
        MoonPhase.PhaseName.FirstQuarter => "moon_first_quarter",
        MoonPhase.PhaseName.WaxingGibbous => "moon_waxing_gibbous",
        MoonPhase.PhaseName.Full => "moon_full",
        MoonPhase.PhaseName.WaningGibbous => "moon_waning_gibbous",
        MoonPhase.PhaseName.LastQuarter => "moon_last_quarter",
        _ => "moon_waning_crescent"
    };

    private const double FullMoonAgeDays = 14.765;

    public static string Line(Loc loc, MoonPhase phase, DateOnly today, bool eastern)
    {
        string next;
        if (phase.Name == MoonPhase.PhaseName.Full) next = loc["widget_moon_full_tonight"];
        else if (phase.Waxing)
        {
            int days = (int)Math.Round(FullMoonAgeDays - phase.AgeDays);
            next = days <= 0 ? loc["widget_moon_full_tonight"] : loc.Plural("widget_moon_full_in", days, loc.Number(days));
        }
        else
        {
            var hijri = HijriCalendars.Current.ToHijri(today);
            var start = today.AddDays(hijri.LengthOfMonth - hijri.Day + 1);
            bool arabic = loc.Language is "ar" or "ur";
            next = loc.Format("widget_moon_new_month", Core.Text.HijriText.Gregorian(start, loc.Language, eastern, arabic ? "dddd d MMMM" : "ddd d MMM"));
        }
        return $"{loc.Format("moon_lit", loc.Percent(phase.Illumination * 100))}{loc.Sep}{next}";
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (_phase == null) return;
        float w = (float)sender.ActualWidth, h = (float)sender.ActualHeight;
        float r = Math.Min(w, h) / 2 - 2;
        var center = new Vector2(w / 2, h / 2);
        var colors = SkyTimeline.NightColors;
        args.DrawingSession.FillCircle(center, r, ThemeService.ToColor(colors.Sky[2]));
        SkyRenderer.DrawMoon(args.DrawingSession, sender, center, r * 0.83f, _phase, colors);
    }
}
