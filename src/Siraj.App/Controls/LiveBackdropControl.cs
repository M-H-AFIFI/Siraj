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
    private bool _subscribed;

    private string? _lastScene;
    private string? _previousScene;
    private double _fadeStartSec;
    private const double CrossfadeSeconds = 0.26;

    public string? OverrideScene { get; set; }
    public bool? OverrideLive { get; set; }
    public bool? OverrideGlass { get; set; }
    public bool DrawVeil { get; set; } = true;
    public bool Animate { get; set; } = true;

    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            UpdateTimer();
        }
    }

    public LiveBackdropControl()
    {
        IsHitTestVisible = false;
        Content = _canvas;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Refresh() => _canvas.Invalidate();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed)
        {
            _canvas.Draw += OnDraw;
            App.Theme.Changed += OnThemeOrSettings;
            App.Settings.Changed += OnSettings;
            Views.MainWindow.ActiveChanged += UpdateTimer;
            _subscribed = true;
        }
        _lastScene = LiveScenes.NormalizeId(OverrideScene ?? App.Settings.Current.LiveScene);
        _timer ??= CreateTimer();
        UpdateTimer();
        _canvas.Invalidate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribed)
        {
            _canvas.Draw -= OnDraw;
            App.Theme.Changed -= OnThemeOrSettings;
            App.Settings.Changed -= OnSettings;
            Views.MainWindow.ActiveChanged -= UpdateTimer;
            _subscribed = false;
        }
        _timer?.Stop();
    }

    private DispatcherQueueTimer CreateTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        // 30 fps for the main backdrop, 15 fps for secondary preview cards so they don't compete.
        timer.Interval = TimeSpan.FromMilliseconds(OverrideScene != null ? 66 : 33);
        timer.Tick += (_, _) => _canvas.Invalidate();
        return timer;
    }

    private void OnThemeOrSettings() => DispatcherQueue.TryEnqueue(() =>
    {
        UpdateTimer();
        _canvas.Invalidate();
    });

    private void OnSettings(Core.Settings.AppSettings old, Core.Settings.AppSettings updated)
    {
        if (old.LiveBackground != updated.LiveBackground || old.LiveScene != updated.LiveScene ||
            old.LiveIntensity != updated.LiveIntensity || old.LiveSkySync != updated.LiveSkySync ||
            old.GlassUi != updated.GlassUi || old.GlassFinish != updated.GlassFinish ||
            old.ReduceMotion != updated.ReduceMotion)
        {
            if (OverrideScene == null && !string.Equals(old.LiveScene, updated.LiveScene, StringComparison.OrdinalIgnoreCase))
            {
                _previousScene = LiveScenes.NormalizeId(old.LiveScene);
                _lastScene = LiveScenes.NormalizeId(updated.LiveScene);
                _fadeStartSec = Clock.Elapsed.TotalSeconds;
            }
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
        if (want)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _canvas.Invalidate();
        }
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        float w = (float)sender.ActualWidth, h = (float)sender.ActualHeight;
        if (w < 1 || h < 1) return;
        var s = App.Settings.Current;
        bool live = OverrideLive ?? s.LiveBackground;
        bool glass = OverrideGlass ?? s.GlassUi;
        string sceneId = LiveScenes.NormalizeId(OverrideScene ?? s.LiveScene);
        var intensity = LiveScenes.FindIntensity(s.LiveIntensity);
        var now = DateTime.Now;
        int mins = now.Hour * 60 + now.Minute;
        var colors = LiveScenes.ColorsFor(App.Theme.Scheme, s.BlackMode, s.LiveSkySync, mins);
        double nowSec = Clock.Elapsed.TotalSeconds;
        float t = Animate && Kit.Motion
            ? LiveScenes.RestSeconds + (float)nowSec * intensity.MotionScale
            : LiveScenes.RestSeconds;

        float crossfade = 1f;
        string? prev = null;
        if (_previousScene != null && Kit.Motion)
        {
            double elapsed = nowSec - _fadeStartSec;
            if (elapsed < CrossfadeSeconds)
            {
                float u = Math.Clamp((float)(elapsed / CrossfadeSeconds), 0f, 1f);
                crossfade = u * u * (3f - 2f * u);
                prev = _previousScene;
            }
            else
            {
                _previousScene = null;
            }
        }

        Theme.LiveSceneRenderer.DrawBackdrop(args.DrawingSession, sender, w, h, sceneId, colors, intensity,
            App.Theme.Glass, live, glass, s.LiveSkySync, mins, t, DrawVeil, prev, crossfade);
    }
}
