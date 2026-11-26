using Siraj.App.Native;
using Siraj.App.Services;
using Siraj.Core.Prayer;
using Siraj.Core.Settings;
using Windows.Graphics;

namespace Siraj.App.Widgets;

internal sealed class TaskbarWidgetController : IDisposable
{
    private TaskbarWidgetWindow? _bar;
    private TaskbarFlyoutWindow? _flyout;
    private string? _dismissedUrgentKey;
    private bool _disposed;

    public IntPtr FlyoutHandle => _flyout?.Handle ?? IntPtr.Zero;

    public void Start()
    {
        if (_disposed) return;
        Apply(App.Settings.Current.TaskbarWidget);
    }

    public void Apply(TaskbarWidgetConfig config)
    {
        if (_disposed) return;
        if (!config.Enabled || !App.Settings.Current.SetupDone)
        {
            CloseBar();
            return;
        }
        if (_bar == null)
        {
            try
            {
                _bar = new TaskbarWidgetWindow(this, config);
                _bar.SyncPosition();
            }
            catch (Exception e)
            {
                Log.Error("opening taskbar widget", e);
                _bar = null;
            }
        }
        else
        {
            _bar.Apply(config);
        }
        _flyout?.Refresh();
    }

    public void Refresh()
    {
        _bar?.Refresh();
        _flyout?.Refresh();
    }

    public void OnTaskbarCreated() => _bar?.SyncPosition();

    public bool IsUrgentDismissed(DateOnly date, PrayerType type) =>
        _dismissedUrgentKey == $"{date:yyyy-MM-dd}_{type}";

    public void DismissUrgent(DateOnly date, PrayerType type)
    {
        _dismissedUrgentKey = $"{date:yyyy-MM-dd}_{type}";
        _bar?.Refresh();
        _flyout?.Refresh();
    }

    public void ToggleFlyout(RectInt32 anchorRect)
    {
        if (_flyout != null && (_flyout.IsShown || _flyout.WasHiddenRecently(300)))
        {
            _flyout.HideFlyout();
            return;
        }
        ShowFlyoutAt(anchorRect);
    }

    public void PreviewFlyout()
    {
        var tb = Win32.QueryTaskbar();
        if (_bar != null && _bar.AppWindow.IsVisible)
        {
            ShowFlyoutAt(_bar.Placement);
            return;
        }
        if (tb != null)
        {
            int x = tb.Value.TrayOnLeft ? tb.Value.NotifyRect.Right + 16 : tb.Value.NotifyRect.Left - 180;
            ShowFlyoutAt(new RectInt32(x, tb.Value.TrayRect.Top, 160, tb.Value.TrayRect.Bottom - tb.Value.TrayRect.Top));
        }
    }

    private void ShowFlyoutAt(RectInt32 anchorRect)
    {
        try
        {
            var tb = Win32.QueryTaskbar();
            var work = tb?.WorkRect ?? Win32.WorkAreaAt(anchorRect.X, anchorRect.Y);
            if (_flyout == null)
            {
                _flyout = new TaskbarFlyoutWindow(this);
                _flyout.Closed += (_, _) => _flyout = null;
            }
            _flyout.ShowNear(anchorRect, work);
        }
        catch (Exception e)
        {
            Log.Error("showing taskbar flyout", e);
        }
    }

    private void CloseBar()
    {
        _flyout?.HideFlyout();
        _bar?.Close();
        _bar = null;
    }

    public void Dispose()
    {
        _disposed = true;
        _flyout?.Close();
        _flyout = null;
        _bar?.Close();
        _bar = null;
    }
}
