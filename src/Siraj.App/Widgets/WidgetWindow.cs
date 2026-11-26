using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Native;
using Siraj.App.Services;
using Siraj.Core.Settings;
using Siraj.Core.Theme;
using Windows.Graphics;

namespace Siraj.App.Widgets;

internal sealed class WidgetWindow : Window
{
    private readonly IntPtr _hwnd;
    private readonly WidgetView _view;
    private readonly Win32.SubclassProc _subclass;
    private WidgetConfig _config;
    private bool _peeking;
    private bool _clear;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _peekEnd;

    public string Id => _config.Id;

    public WidgetWindow(WidgetConfig config)
    {
        _config = config;
        _view = new WidgetView(config, live: true);
        var root = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        root.Children.Add(_view);
        Content = root;
        App.Theme.Register(root);
        Title = "Siraj widget";
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetIcon(AppPaths.Asset("Siraj.ico"));

        _subclass = OnMessage;
        Win32.SetWindowSubclass(_hwnd, _subclass, 1, 0);

        // A tool window (no taskbar button) and a bare popup: the subclass strips the frame from
        // these and from every later change the presenter makes (see OnMessage).
        long ex = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE).ToInt64();
        ex = (ex | Win32.WS_EX_TOOLWINDOW) & ~Win32.WS_EX_APPWINDOW;
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex));
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_STYLE, Win32.GetWindowLongPtr(_hwnd, Win32.GWL_STYLE));
        Win32.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
        // Owned by the desktop, so Show desktop (Win+D) leaves it in place.
        var desktop = Win32.FindWindow("Progman", null);
        if (desktop != IntPtr.Zero) Win32.SetWindowLongPtr(_hwnd, Win32.GWLP_HWNDPARENT, desktop);

        ApplyDress();
        _view.PointerPressed += OnPointerPressed;
        _view.ContextFlyout = BuildMenu();
        root.SizeChanged += (_, _) => Fit();
        App.Theme.Changed += OnThemeChanged;
        Closed += (_, _) => App.Theme.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged() => _view.ContextFlyout = BuildMenu();

    public void Show()
    {
        Place();
        AppWindow.Show(false);
        SendToBack();
    }

    private void ApplyDress()
    {
        bool glass = _config.Background == "glass" && DesktopAcrylicController.IsSupported();
        _clear = !glass;
        SystemBackdrop = glass ? new DesktopAcrylicBackdrop() : new TransparentBackdrop();
        int sheet = glass ? 0 : -1;
        var margins = new Win32.MARGINS { Left = sheet, Right = sheet, Top = sheet, Bottom = sheet };
        Win32.DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        int corner = glass ? Win32.DWMWCP_ROUND : Win32.DWMWCP_DONOTROUND;
        Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        int border = Win32.DWMWA_COLOR_NONE;
        Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_BORDER_COLOR, ref border, sizeof(int));
        Win32.RedrawWindow(_hwnd, IntPtr.Zero, IntPtr.Zero, Win32.RDW_INVALIDATE | Win32.RDW_ERASE | Win32.RDW_FRAME | Win32.RDW_UPDATENOW);
    }

    public void Apply(WidgetConfig config)
    {
        bool dressChanged = config.Background != _config.Background;
        bool sizeChanged = config.Scale != _config.Scale || config.Face != _config.Face;
        _config = config;
        if (dressChanged) ApplyDress();
        _view.Apply(config);
        _view.ContextFlyout = BuildMenu();
        if (sizeChanged) Fit();
    }

    public void Refresh()
    {
        _view.Rebuild();
        _view.ContextFlyout = BuildMenu();
    }

    private double Scale => Win32.GetDpiForWindow(_hwnd) / 96.0;

    private SizeInt32 PixelSize()
    {
        var face = WidgetFaces.Find(_config.Face);
        return new SizeInt32((int)Math.Ceiling(face.Width * _config.Scale * Scale), (int)Math.Ceiling(face.Height * _config.Scale * Scale));
    }

    private void Place()
    {
        var size = PixelSize();
        int x, y;
        if (!double.IsNaN(_config.X) && !double.IsNaN(_config.Y))
        {
            x = (int)_config.X;
            y = (int)_config.Y;
            var area = Win32.WorkAreaAt(x + size.Width / 2, y + size.Height / 2);
            x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - size.Width));
            y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - size.Height));
        }
        else
        {
            (x, y) = FreeSpot(size);
            Save(x, y);
        }
        AppWindow.MoveAndResize(new RectInt32(x, y, size.Width, size.Height));
    }

    private (int X, int Y) FreeSpot(SizeInt32 size)
    {
        var area = Win32.WorkAreaOf(_hwnd);
        int gap = (int)(16 * Scale), margin = (int)(24 * Scale);
        var taken = App.Widgets.Rects(this);
        bool rtl = Loc.Current.IsRtl;
        for (int column = 0; column < 8; column++)
        {
            int x = rtl ? area.Left + margin + column * (size.Width + gap) : area.Right - margin - size.Width - column * (size.Width + gap);
            for (int y = area.Top + margin; y + size.Height <= area.Bottom - margin; y += gap)
            {
                var candidate = new RectInt32(x, y, size.Width, size.Height);
                var hit = taken.FirstOrDefault(r => Overlaps(r, candidate, gap));
                if (hit.Width == 0) return (x, y);
                y = hit.Y + hit.Height;
            }
        }
        return (area.Right - margin - size.Width, area.Top + margin);
    }

    private static bool Overlaps(RectInt32 a, RectInt32 b, int gap) =>
        a.X < b.X + b.Width + gap && b.X < a.X + a.Width + gap && a.Y < b.Y + b.Height + gap && b.Y < a.Y + a.Height + gap;

    public RectInt32 Placement => new(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);

    private void Fit()
    {
        var size = PixelSize();
        if (AppWindow.Size.Width != size.Width || AppWindow.Size.Height != size.Height)
            AppWindow.Resize(size);
    }

    public void Peek(TimeSpan duration)
    {
        _peeking = true;
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        // One timer, held here: a second peek restarts it, and it cannot be collected before it
        // fires and leave the widget above every window.
        if (_peekEnd == null)
        {
            _peekEnd = DispatcherQueue.CreateTimer();
            _peekEnd.IsRepeating = false;
            _peekEnd.Tick += (_, _) =>
            {
                Win32.SetWindowPos(_hwnd, new IntPtr(-2) /* HWND_NOTOPMOST */, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                _peeking = false;
                SendToBack();
            };
            Closed += (_, _) => _peekEnd.Stop();
        }
        _peekEnd.Stop();
        _peekEnd.Interval = duration;
        _peekEnd.Start();
    }

    private void SendToBack() =>
        Win32.SetWindowPos(_hwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);

    private IntPtr OnMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, nuint data)
    {
        // Whatever moves it in the z-order - a click, the menu - it goes back behind the windows.
        if (msg == Win32.WM_WINDOWPOSCHANGING)
        {
            unsafe
            {
                var pos = (Win32.WINDOWPOS*)lParam;
                if ((pos->flags & Win32.SWP_NOZORDER) == 0 && !_peeking) pos->hwndInsertAfter = Win32.HWND_BOTTOM;
            }
        }
        // A bare popup, whatever the presenter sets: without border and title bar it still asks
        // for WS_DLGFRAME, a 3-pixel frame Windows paints white and takes out of the content.
        // The presenter decides in this same message, so its answer is corrected afterwards.
        else if (msg == Win32.WM_STYLECHANGING)
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
        // Under the drawing, the window's own surface: WinUI fills it with the theme's background,
        // white in the light theme, which the clear frame would show at the corners and behind a
        // bare widget. Black is what the frame composes as nothing.
        else if (msg == Win32.WM_ERASEBKGND && _clear)
        {
            Win32.GetClientRect(hwnd, out var rect);
            Win32.FillRect(wParam, ref rect, Win32.GetStockObject(Win32.BLACK_BRUSH));
            return new IntPtr(1);
        }
        else if (msg == Win32.WM_DISPLAYCHANGE || msg == 0x02E0 /* WM_DPICHANGED */)
        {
            DispatcherQueue.TryEnqueue(Fit);
        }
        return Win32.DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_view);
        if (!point.Properties.IsLeftButtonPressed || _config.Locked) return;
        // Hand the drag to Windows' own move loop; it returns when the button is let go.
        Win32.ReleaseCapture();
        Win32.SendMessage(_hwnd, Win32.WM_NCLBUTTONDOWN, Win32.HTCAPTION, IntPtr.Zero);
        var position = AppWindow.Position;
        Snap(ref position);
        Save(position.X, position.Y);
        SendToBack();
    }

    private void Snap(ref PointInt32 position)
    {
        var size = AppWindow.Size;
        var area = Win32.WorkAreaAt(position.X + size.Width / 2, position.Y + size.Height / 2);
        int snap = (int)(16 * Scale), margin = (int)(12 * Scale);
        int x = position.X, y = position.Y;
        if (Math.Abs(x - area.Left - margin) < snap) x = area.Left + margin;
        if (Math.Abs(area.Right - (x + size.Width) - margin) < snap) x = area.Right - size.Width - margin;
        if (Math.Abs(y - area.Top - margin) < snap) y = area.Top + margin;
        if (Math.Abs(area.Bottom - (y + size.Height) - margin) < snap) y = area.Bottom - size.Height - margin;
        if (x != position.X || y != position.Y)
        {
            position = new PointInt32(x, y);
            AppWindow.Move(position);
        }
    }

    private void Save(int x, int y)
    {
        _config = _config with { X = x, Y = y };
        App.Widgets.SavePosition(_config.Id, x, y);
    }

    private MenuFlyout BuildMenu()
    {
        var loc = Loc.Current;
        var menu = new MenuFlyout();

        var dress = new MenuFlyoutSubItem { Text = loc["widget_style_background"] };
        foreach (var (key, label) in new[] { ("glass", "widget_background_glass"), ("soft", "widget_background_soft"), ("filled", "widget_background_colour"), ("none", "widget_background_none") })
        {
            var item = new RadioMenuFlyoutItem { Text = loc[label], GroupName = "dress", IsChecked = _config.Background == key };
            item.Click += (_, _) => App.Widgets.Update(_config with { Background = key });
            dress.Items.Add(item);
        }
        menu.Items.Add(dress);

        var colour = new MenuFlyoutSubItem { Text = loc["widget_style_colour"] };
        var app = new RadioMenuFlyoutItem { Text = loc["win_widget_colour_app"], GroupName = "colour", IsChecked = _config.Palette == "App" };
        app.Click += (_, _) => App.Widgets.Update(_config with { Palette = "App" });
        colour.Items.Add(app);
        foreach (var name in Palettes.Names)
        {
            var item = new RadioMenuFlyoutItem { Text = loc[Controls.Ui.PaletteKey(name)], GroupName = "colour", IsChecked = _config.Palette == name };
            item.Click += (_, _) => App.Widgets.Update(_config with { Palette = name });
            colour.Items.Add(item);
        }
        menu.Items.Add(colour);

        var opacity = new MenuFlyoutSubItem { Text = loc["widget_style_opacity"] };
        foreach (int value in new[] { 100, 85, 70, 55, 40 })
        {
            var item = new RadioMenuFlyoutItem { Text = loc.Format("widget_style_percent", loc.Number(value)), GroupName = "opacity", IsChecked = _config.Opacity == value };
            item.Click += (_, _) => App.Widgets.Update(_config with { Opacity = value });
            opacity.Items.Add(item);
        }
        menu.Items.Add(opacity);

        var size = new MenuFlyoutSubItem { Text = loc["win_widget_size"] };
        foreach (var (scale, label) in new[] { (0.85, "win_widget_size_small"), (1.0, "win_widget_size_medium"), (1.25, "win_widget_size_large") })
        {
            var item = new RadioMenuFlyoutItem { Text = loc[label], GroupName = "size", IsChecked = Math.Abs(_config.Scale - scale) < 0.01 };
            item.Click += (_, _) => App.Widgets.Update(_config with { Scale = scale });
            size.Items.Add(item);
        }
        menu.Items.Add(size);

        var locked = new ToggleMenuFlyoutItem { Text = loc["win_widget_lock"], IsChecked = _config.Locked };
        locked.Click += (_, _) => App.Widgets.Update(_config with { Locked = locked.IsChecked });
        menu.Items.Add(locked);

        menu.Items.Add(new MenuFlyoutSeparator());
        var open = new MenuFlyoutItem { Text = loc["win_tray_open"] };
        open.Click += (_, _) => App.ShowMainWindow();
        menu.Items.Add(open);
        var remove = new MenuFlyoutItem { Text = loc["win_widget_remove"] };
        remove.Click += (_, _) => App.Widgets.Remove(_config.Id);
        menu.Items.Add(remove);
        return menu;
    }
}
