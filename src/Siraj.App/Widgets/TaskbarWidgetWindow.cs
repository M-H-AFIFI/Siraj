using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Native;
using Siraj.App.Services;
using Siraj.Core.Settings;
using Windows.Graphics;

namespace Siraj.App.Widgets;

internal sealed class TaskbarWidgetWindow : Window
{
    private readonly IntPtr _hwnd;
    private readonly TaskbarPillView _pill;
    private readonly Win32.SubclassProc _subclass;
    private readonly TaskbarWidgetController _controller;
    private readonly DispatcherQueueTimer _syncTimer;
    private TaskbarWidgetConfig _config;
    private RectInt32 _lastRect;
    private bool _clear = true;
    private bool _closed;
    private bool _dragging;
    private int _dragX;

    public IntPtr Handle => _hwnd;
    public RectInt32 Placement
    {
        get
        {
            if (Win32.GetWindowRect(_hwnd, out var r) && r.Right > r.Left && r.Bottom > r.Top)
                return new RectInt32(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            return new RectInt32(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
        }
    }

    public TaskbarWidgetWindow(TaskbarWidgetController controller, TaskbarWidgetConfig config)
    {
        _controller = controller;
        _config = config;
        _pill = new TaskbarPillView(config, live: true, draggable: true, useRealBackdrop: true);
        _pill.Clicked += () => _controller.ToggleFlyout(Placement);
        _pill.SizeInvalidated += SyncPosition;
        _pill.DragDelta += OnDragDelta;
        _pill.DragCompleted += OnDragCompleted;

        var root = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        root.Children.Add(_pill);
        Content = root;
        App.Theme.Register(root);
        Title = "Siraj taskbar";
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);
        try { AppWindow.IsShownInSwitchers = false; } catch { }

        _subclass = OnMessage;
        Win32.SetWindowSubclass(_hwnd, _subclass, 2, 0);

        // Tool window (hidden from Alt+Tab and taskbar) without WS_EX_NOACTIVATE so a user click
        // legitimately activates Siraj and lets the prayer flyout take foreground focus cleanly.
        // Background repositioning still passes SWP_NOACTIVATE and AppWindow.Show(false).
        long ex = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE).ToInt64();
        ex = (ex | Win32.WS_EX_TOOLWINDOW) & ~(Win32.WS_EX_APPWINDOW | Win32.WS_EX_NOACTIVATE);
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex));
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_STYLE, Win32.GetWindowLongPtr(_hwnd, Win32.GWL_STYLE));
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);

        ApplyBackdrop();
        _pill.ContextFlyout = BuildMenu();

        _syncTimer = DispatcherQueue.CreateTimer();
        _syncTimer.Interval = TimeSpan.FromMilliseconds(900);
        _syncTimer.Tick += (_, _) => { if (!_dragging) SyncPosition(); };
        _syncTimer.Start();

        App.Theme.Changed += OnThemeChanged;
        Closed += (_, _) =>
        {
            _closed = true;
            _syncTimer.Stop();
            App.Theme.Changed -= OnThemeChanged;
        };
    }

    private void ApplyBackdrop()
    {
        bool glass = _config.Style == "glass" && DesktopAcrylicController.IsSupported();
        _clear = !glass;
        SystemBackdrop = glass ? new DesktopAcrylicBackdrop() : new TransparentBackdrop();
        int sheet = glass ? 0 : -1;
        var margins = new Win32.MARGINS { Left = sheet, Right = sheet, Top = sheet, Bottom = sheet };
        Win32.DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        int corner = glass
            ? (_config.CornerRadius <= 6 ? Win32.DWMWCP_ROUNDSMALL : Win32.DWMWCP_ROUND)
            : Win32.DWMWCP_DONOTROUND;
        Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        int border = Win32.DWMWA_COLOR_NONE;
        Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_BORDER_COLOR, ref border, sizeof(int));
        Win32.RedrawWindow(_hwnd, IntPtr.Zero, IntPtr.Zero, Win32.RDW_INVALIDATE | Win32.RDW_ERASE | Win32.RDW_FRAME | Win32.RDW_UPDATENOW);
    }

    private void OnThemeChanged() => _pill.ContextFlyout = BuildMenu();

    public void Apply(TaskbarWidgetConfig config)
    {
        bool backdropChanged = config.Style != _config.Style
            || (config.Style == "glass" && config.CornerRadius != _config.CornerRadius);
        _config = config;
        if (backdropChanged) ApplyBackdrop();
        _pill.Apply(config);
        _pill.ContextFlyout = BuildMenu();
        SyncPosition();
    }

    public void Refresh()
    {
        _pill.Refresh();
        _pill.ContextFlyout = BuildMenu();
        SyncPosition();
    }

    private void OnDragDelta(int deltaX)
    {
        if (_closed) return;
        var info = Win32.QueryTaskbar();
        if (info == null) return;
        var tb = info.Value;
        if (!_dragging)
        {
            _dragging = true;
            _dragX = _lastRect.X;
        }
        int minX = tb.TrayRect.Left + 4;
        int maxX = Math.Max(minX, tb.TrayRect.Right - _lastRect.Width - 4);
        _dragX = Math.Clamp(_dragX + deltaX, minX, maxX);
        var rect = new RectInt32(_dragX, _lastRect.Y, _lastRect.Width, _lastRect.Height);
        _lastRect = rect;
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, rect.X, rect.Y, rect.Width, rect.Height, Win32.SWP_NOACTIVATE);
    }

    private void OnDragCompleted()
    {
        if (!_dragging) return;
        _dragging = false;
        var info = Win32.QueryTaskbar();
        if (info == null) return;
        var tb = info.Value;
        int minX = tb.TrayRect.Left + 4;
        int span = Math.Max(1, (tb.TrayRect.Right - _lastRect.Width - 4) - minX);
        double pct = Math.Clamp((_dragX - minX) * 100.0 / span, 0.0, 100.0);
        pct = Math.Round(pct, 1);
        App.Settings.Update(s => s with
        {
            TaskbarWidget = s.TaskbarWidget with { Position = "custom", CustomPositionPercent = pct, OffsetX = 0 },
        });
    }

    public void SyncPosition()
    {
        if (_closed || _dragging || _pill.IsInteracting) return;
        var info = Win32.QueryTaskbar();
        if (info == null || info.Value.AutoHidden)
        {
            if (AppWindow.IsVisible) AppWindow.Hide();
            return;
        }

        var tb = info.Value;
        if (_config.HideInFullscreen && Win32.IsFullscreenOnMonitor(tb.MonitorRect, _hwnd, _controller.FlyoutHandle))
        {
            if (AppWindow.IsVisible) AppWindow.Hide();
            return;
        }

        double scale = tb.Dpi / 96.0;
        int trayH = tb.TrayRect.Bottom - tb.TrayRect.Top;
        int pillH = Math.Clamp(trayH - (int)Math.Round(8 * scale), (int)Math.Round(30 * scale), (int)Math.Round(42 * scale));
        double widthDip = _pill.MeasureWidth(pillH / scale);
        int pillW = (int)Math.Ceiling(widthDip * scale);
        int gap = (int)Math.Round(8 * scale);
        int minX = tb.TrayRect.Left + 4;
        int maxX = Math.Max(minX, tb.TrayRect.Right - pillW - 4);

        int x;
        switch (_config.Position)
        {
            case "custom":
                double frac = Math.Clamp(_config.CustomPositionPercent, 0.0, 100.0) / 100.0;
                x = minX + (int)Math.Round(frac * (maxX - minX));
                break;
            case "left":
                x = tb.TrayRect.Left + (int)Math.Round(12 * scale) + (int)Math.Round(_config.OffsetX * scale);
                break;
            case "center":
                x = tb.TrayRect.Left + ((tb.TrayRect.Right - tb.TrayRect.Left) - pillW) / 2 + (int)Math.Round(_config.OffsetX * scale);
                break;
            case "right":
                x = (tb.TrayOnLeft
                    ? tb.TrayRect.Right - pillW - (int)Math.Round(12 * scale)
                    : tb.NotifyRect.Left - pillW - gap) + (int)Math.Round(_config.OffsetX * scale);
                break;
            default:
                x = (tb.TrayOnLeft
                    ? tb.NotifyRect.Right + gap
                    : tb.NotifyRect.Left - pillW - gap) + (int)Math.Round(_config.OffsetX * scale);
                break;
        }

        x = Math.Clamp(x, minX, maxX);
        int y = tb.TrayRect.Top + (trayH - pillH) / 2;

        var rect = new RectInt32(x, y, pillW, pillH);
        if (rect != _lastRect)
        {
            _lastRect = rect;
            AppWindow.MoveAndResize(rect);
        }

        if (!AppWindow.IsVisible)
            AppWindow.Show(false);

        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, rect.X, rect.Y, rect.Width, rect.Height, Win32.SWP_NOACTIVATE);
    }

    private MenuFlyout BuildMenu()
    {
        var loc = Loc.Current;
        var menu = new MenuFlyout();

        var open = new MenuFlyoutItem { Text = loc["win_tray_open"] };
        open.Click += (_, _) => App.ShowMainWindow("prayer");
        menu.Items.Add(open);

        var custom = new MenuFlyoutItem { Text = loc["win_taskbar_customize"] };
        custom.Click += (_, _) => App.ShowMainWindow("widgets");
        menu.Items.Add(custom);

        var lockItem = new ToggleMenuFlyoutItem { Text = loc["win_taskbar_lock"], IsChecked = _config.Locked };
        lockItem.Click += (_, _) => App.Settings.Update(s => s with { TaskbarWidget = s.TaskbarWidget with { Locked = lockItem.IsChecked } });
        menu.Items.Add(lockItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        bool muted = App.Settings.Current.AlertType == "silent";
        var mute = new MenuFlyoutItem { Text = loc[muted ? "win_taskbar_unmute" : "win_taskbar_mute"] };
        mute.Click += (_, _) =>
        {
            App.Settings.Update(s => s with { AlertType = muted ? "azan" : "silent" });
            if (!muted && App.Adhan.IsPlaying) App.Adhan.Stop();
        };
        menu.Items.Add(mute);

        var hide = new MenuFlyoutItem { Text = loc["win_taskbar_hide"] };
        hide.Click += (_, _) => App.Settings.Update(s => s with { TaskbarWidget = s.TaskbarWidget with { Enabled = false } });
        menu.Items.Add(hide);

        return menu;
    }

    private IntPtr OnMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, nuint data)
    {
        if (msg == Win32.WM_STYLECHANGING)
        {
            var result = Win32.DefSubclassProc(hwnd, msg, wParam, lParam);
            unsafe
            {
                var change = (Win32.STYLESTRUCT*)lParam;
                if (wParam.ToInt32() == Win32.GWL_STYLE)
                    change->styleNew = (uint)((change->styleNew & ~(Win32.WS_CAPTION | Win32.WS_THICKFRAME | Win32.WS_SYSMENU)) | Win32.WS_POPUP);
                else if (wParam.ToInt32() == Win32.GWL_EXSTYLE)
                    change->styleNew = (uint)(change->styleNew & ~(Win32.WS_EX_WINDOWEDGE | Win32.WS_EX_DLGMODALFRAME));
            }
            return result;
        }
        if (msg == Win32.WM_ERASEBKGND && _clear)
        {
            Win32.GetClientRect(hwnd, out var rect);
            Win32.FillRect(wParam, ref rect, Win32.GetStockObject(Win32.BLACK_BRUSH));
            return new IntPtr(1);
        }
        if (msg == Win32.WM_DISPLAYCHANGE || msg == 0x02E0 /* WM_DPICHANGED */)
        {
            DispatcherQueue.TryEnqueue(SyncPosition);
        }
        return Win32.DefSubclassProc(hwnd, msg, wParam, lParam);
    }
}
