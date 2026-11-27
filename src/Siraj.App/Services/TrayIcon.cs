using System.Runtime.InteropServices;
using Siraj.App.Native;
using Siraj.Core.Prayer;

namespace Siraj.App.Services;

internal sealed class TrayIcon : IDisposable
{
    private const int CallbackMessage = Win32.WM_USER + 77;
    private const int IdOpen = 1, IdStop = 2, IdSettings = 3, IdQuit = 4, IdTaskbar = 5, IdUpdate = 6, IdWidgetBase = 100;

    private readonly Win32.WndProc _proc;
    private readonly IntPtr _hwnd;
    private readonly uint _taskbarCreated;
    private IntPtr _icon;
    private bool _added;
    private string _tip = "Siraj";

    public event Action? TimeShifted;
    public event Action? TaskbarCreated;

    public TrayIcon()
    {
        _proc = WndProc;
        var className = "SirajTrayWindow";
        var wc = new Win32.WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<Win32.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = Win32.GetModuleHandle(null),
            lpszClassName = Marshal.StringToHGlobalUni(className),
        };
        Win32.RegisterClassEx(ref wc);
        // Top-level hidden window; message-only windows do not receive system broadcasts.
        _hwnd = Win32.CreateWindowEx(0, className, "Siraj", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        _taskbarCreated = Win32.RegisterWindowMessage("TaskbarCreated");
        LoadIcon();
        Add();
    }

    private void LoadIcon()
    {
        if (_icon != IntPtr.Zero) Win32.DestroyIcon(_icon);
        uint dpi = Win32.GetDpiForSystem();
        int size = Win32.GetSystemMetricsForDpi(Win32.SM_CXSMICON, dpi);
        _icon = Win32.LoadImage(IntPtr.Zero, AppPaths.Asset("Siraj.ico"), Win32.IMAGE_ICON, size, size, Win32.LR_LOADFROMFILE);
    }

    private Win32.NOTIFYICONDATA Data(int flags) => new()
    {
        cbSize = Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = flags,
        uCallbackMessage = CallbackMessage,
        hIcon = _icon,
        szTip = _tip.Length > 127 ? _tip[..127] : _tip,
        szInfo = "",
        szInfoTitle = "",
    };

    private void Add()
    {
        var data = Data(Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP | Win32.NIF_SHOWTIP);
        _added = Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref data);
        data.uVersion = Win32.NOTIFYICON_VERSION_4;
        Win32.Shell_NotifyIcon(Win32.NIM_SETVERSION, ref data);
    }

    public void Update(string tip)
    {
        if (tip == _tip) return;
        _tip = tip;
        var data = Data(Win32.NIF_TIP | Win32.NIF_SHOWTIP);
        if (_added) Win32.Shell_NotifyIcon(Win32.NIM_MODIFY, ref data);
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == CallbackMessage)
            {
                int ev = (int)(lParam.ToInt64() & 0xFFFF);
                switch (ev)
                {
                    case Win32.WM_LBUTTONUP or Win32.NIN_SELECT or Win32.NIN_KEYSELECT:
                        App.Dispatcher.TryEnqueue(App.ShowMainWindow);
                        break;
                    case Win32.WM_CONTEXTMENU or Win32.WM_RBUTTONUP:
                        ShowMenu();
                        break;
                }
                return IntPtr.Zero;
            }
            if (msg == _taskbarCreated && _taskbarCreated != 0)
            {
                // Re-register tray icon after Explorer restarts.
                LoadIcon();
                Add();
                TaskbarCreated?.Invoke();
                return IntPtr.Zero;
            }
            switch (msg)
            {
                case Win32.WM_POWERBROADCAST when wParam.ToInt64() is Win32.PBT_APMRESUMEAUTOMATIC or Win32.PBT_APMRESUMESUSPEND:
                case Win32.WM_TIMECHANGE:
                    Log.Info(msg == Win32.WM_TIMECHANGE ? "clock changed" : "resumed from sleep");
                    App.Dispatcher.TryEnqueue(() => TimeShifted?.Invoke());
                    break;
                case Win32.WM_SETTINGCHANGE:
                    // Timezone changes fire WM_SETTINGCHANGE with "intl" or an empty section.
                    TimeZoneInfo.ClearCachedData();
                    App.Dispatcher.TryEnqueue(() => TimeShifted?.Invoke());
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error("tray message", e);
        }
        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        var loc = Loc.Current;
        string Label(string text) => loc.IsRtl ? "\u200F" + text : text;
        var menu = Win32.CreatePopupMenu();
        try
        {
            Win32.AppendMenu(menu, Win32.MF_STRING, IdOpen, Label(loc["win_tray_open"]));
            var clock = App.Clock;
            if (clock.Day?.Next is { } next)
            {
                Win32.AppendMenu(menu, Win32.MF_STRING | Win32.MF_GRAYED, 0,
                    Label($"{loc.PrayerName(next)}  {loc.Time(next.Time)}"));
            }
            if (App.Adhan.IsPlaying)
                Win32.AppendMenu(menu, Win32.MF_STRING, IdStop, Label(loc["adhan_screen_stop"]));
            Win32.AppendMenu(menu, Win32.MF_SEPARATOR, 0, null);
            var widgets = Win32.CreatePopupMenu();
            int i = 0;
            foreach (var face in Widgets.WidgetFaces.All)
                Win32.AppendMenu(widgets, Win32.MF_STRING, (nuint)(IdWidgetBase + i++), Label(loc[face.TitleKey]));
            string widgetsLabel = loc.IsRtl ? "\u200F\u00A0\u00A0" + loc["win_tray_add_widget"] : loc["win_tray_add_widget"];
            Win32.AppendMenu(menu, Win32.MF_POPUP, (nuint)widgets, widgetsLabel);
            uint tbFlags = Win32.MF_STRING | (App.Settings.Current.TaskbarWidget.Enabled ? Win32.MF_CHECKED : 0);
            Win32.AppendMenu(menu, tbFlags, IdTaskbar, Label(loc["win_taskbar_title"]));
            if (App.Updates?.AvailableRelease is { } rel
                && App.Updates.Snapshot.State is UpdateState.Available or UpdateState.ReadyToInstall)
            {
                Win32.AppendMenu(menu, Win32.MF_STRING, IdUpdate,
                    Label(loc.Format("win_update_tray_item", $"v{loc.Digitize(rel.Version.ToString())}")));
            }
            Win32.AppendMenu(menu, Win32.MF_STRING, IdSettings, Label(loc["settings_title"]));
            Win32.AppendMenu(menu, Win32.MF_SEPARATOR, 0, null);
            Win32.AppendMenu(menu, Win32.MF_STRING, IdQuit, Label(loc["win_tray_quit"]));

            Win32.GetCursorPos(out var p);
            Win32.SetForegroundWindow(_hwnd);
            // Avoid TPM_LAYOUTRTL: Windows 11 dark-mode popup menus mirror every glyph horizontally under LAYOUT_RTL.
            uint flags = Win32.TPM_RETURNCMD | Win32.TPM_RIGHTBUTTON | Win32.TPM_BOTTOMALIGN;
            int id = Win32.TrackPopupMenuEx(menu, flags, p.X, p.Y, _hwnd, IntPtr.Zero);
            App.Dispatcher.TryEnqueue(() =>
            {
                switch (id)
                {
                    case IdOpen:
                        App.ShowMainWindow();
                        break;
                    case IdStop:
                        App.Adhan.Stop();
                        break;
                    case IdTaskbar:
                        App.Settings.Update(s => s with { TaskbarWidget = s.TaskbarWidget with { Enabled = !s.TaskbarWidget.Enabled } });
                        break;
                    case IdUpdate or IdSettings:
                        App.ShowMainWindow("settings");
                        break;
                    case IdQuit:
                        App.Quit();
                        break;
                    case >= IdWidgetBase when id - IdWidgetBase < Widgets.WidgetFaces.All.Length:
                        var face = Widgets.WidgetFaces.All[id - IdWidgetBase];
                        App.Widgets.Add(face.Id, face.Dress);
                        break;
                }
            });
        }
        finally
        {
            Win32.DestroyMenu(menu);
        }
    }

    public static string Tooltip(PrayerClock clock)
    {
        var loc = Loc.Current;
        var next = clock.Day?.Next;
        if (next == null) return "Siraj";
        var left = next.Time - clock.Now;
        string when = left > TimeSpan.Zero
            ? loc.Format("dashboard_countdown_format", Duration(left))
            : loc["prayer_in_progress"];
        return $"{loc["app_name"]}{loc.Sep}{loc.PrayerName(next)} {loc.Time(next.Time)}{loc.Sep}{when}";
    }

    public static string Duration(TimeSpan span)
    {
        var loc = Loc.Current;
        int total = Math.Max(1, (int)Math.Ceiling(span.TotalMinutes));
        int hours = total / 60, minutes = total % 60;
        return hours > 0
            ? loc.Format("win_duration_hm", loc.Number(hours), loc.Number(minutes))
            : loc.Format("win_duration_m", loc.Number(minutes));
    }

    public void Dispose()
    {
        var data = Data(0);
        Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref data);
        if (_icon != IntPtr.Zero) Win32.DestroyIcon(_icon);
        Win32.DestroyWindow(_hwnd);
    }
}
