using System.Diagnostics;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Siraj.App.Services;
using Siraj.Core.Theme;

namespace Siraj.App.Controls;

public sealed class LiveBackdropControl : UserControl
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private readonly CanvasControl _canvas = new() { IsHitTestVisible = false };
    private DispatcherQueueTimer? _timer;
    private bool _paused;

    public string? OverrideScene { get; set; }
    public bool? OverrideLive { get; set; }
    public bool? OverrideGlass { get; set; }
    public bool DrawVeil { get; set; } = true;
    public bool Animate { get; set; } = true;

    public bool Paused
    {
        get => _paused;
        set { _paused = value; UpdateTimer(); }
    }

    public LiveBackdropControl()
    {
        IsHitTestVisible = false;
        Content = _canvas;
        _canvas.Draw += OnDraw;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Refresh() => _canvas.Invalidate();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Theme.Changed += OnThemeOrSettings;
        App.Settings.Changed += OnSettings;
        Views.MainWindow.ActiveChanged += UpdateTimer;
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(42);
        _timer.Tick += (_, _) => _canvas.Invalidate();
        UpdateTimer();
        _canvas.Invalidate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Theme.Changed -= OnThemeOrSettings;
        App.Settings.Changed -= OnSettings;
        Views.MainWindow.ActiveChanged -= UpdateTimer;
        _timer?.Stop();
        _timer = null;
        _canvas.RemoveFromVisualTree();
    }

    private void OnThemeOrSettings() => DispatcherQueue.TryEnqueue(() => { UpdateTimer(); _canvas.Invalidate(); });

    private void OnSettings(Core.Settings.AppSettings old, Core.Settings.AppSettings updated)
    {
        if (old.LiveBackground != updated.LiveBackground || old.LiveScene != updated.LiveScene ||
            old.LiveIntensity != updated.LiveIntensity || old.LiveSkySync != updated.LiveSkySync ||
            old.GlassUi != updated.GlassUi || old.GlassFinish != updated.GlassFinish ||
            old.ReduceMotion != updated.ReduceMotion)
        {
            OnThemeOrSettings();
        }
    }

    private void UpdateTimer()
    {
        if (_timer == null) return;
        var s = App.Settings.Current;
        bool live = OverrideLive ?? s.LiveBackground;
        bool glass = OverrideGlass ?? s.GlassUi;
        bool want = Animate && !_paused && (live || glass) && Kit.Motion && Views.MainWindow.IsActive;
        if (want) _timer.Start();
        else { _timer.Stop(); _canvas.Invalidate(); }
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        float w = (float)sender.ActualWidth, h = (float)sender.ActualHeight;
        if (w < 1 || h < 1) return;
        var s = App.Settings.Current;
        bool live = OverrideLive ?? s.LiveBackground;
        bool glass = OverrideGlass ?? s.GlassUi;
        string sceneId = OverrideScene ?? s.LiveScene;
        var intensity = LiveScenes.FindIntensity(s.LiveIntensity);
        var now = DateTime.Now;
        int mins = now.Hour * 60 + now.Minute;
        var colors = LiveScenes.ColorsFor(App.Theme.Scheme, s.BlackMode, s.LiveSkySync, mins);
        float t = Animate && Kit.Motion ? (float)Clock.Elapsed.TotalSeconds * intensity.MotionScale : 4.2f;
        Theme.LiveSceneRenderer.DrawBackdrop(args.DrawingSession, sender, w, h, sceneId, colors, intensity,
            App.Theme.Glass, live, glass, s.LiveSkySync, mins, t, DrawVeil);
    }
}
