using System.Runtime.InteropServices;

namespace Siraj.App.Native;

internal static partial class Win32
{
    public const int WM_USER = 0x0400;
    public const int WM_COMMAND = 0x0111;
    public const int WM_DESTROY = 0x0002;
    public const int WM_NCDESTROY = 0x0082;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_LBUTTONDBLCLK = 0x0203;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_CONTEXTMENU = 0x007B;
    public const int WM_POWERBROADCAST = 0x0218;
    public const int WM_TIMECHANGE = 0x001E;
    public const int WM_SETTINGCHANGE = 0x001A;
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_DPICHANGED = 0x02E0;
    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int WM_NCHITTEST = 0x0084;
    public const int PBT_APMRESUMEAUTOMATIC = 0x0012;
    public const int PBT_APMRESUMESUSPEND = 0x0007;
    public const int NIN_SELECT = WM_USER + 0;
    public const int NIN_KEYSELECT = WM_USER + 1;

    public const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    public const int NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_SHOWTIP = 0x80;
    public const int NOTIFYICON_VERSION_4 = 4;

    public const uint MF_STRING = 0x0, MF_POPUP = 0x10, MF_SEPARATOR = 0x800, MF_GRAYED = 0x1, MF_CHECKED = 0x8;
    public const uint TPM_RETURNCMD = 0x0100, TPM_RIGHTBUTTON = 0x0002, TPM_LAYOUTRTL = 0x8000, TPM_BOTTOMALIGN = 0x20;

    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20, GWLP_HWNDPARENT = -8;
    public const long WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000, WS_EX_LAYERED = 0x80000;
    public const long WS_CAPTION = 0xC00000, WS_THICKFRAME = 0x40000, WS_SYSMENU = 0x80000, WS_MINIMIZEBOX = 0x20000, WS_MAXIMIZEBOX = 0x10000;
    public const long WS_POPUP = 0x80000000, WS_BORDER = 0x800000, WS_DLGFRAME = 0x400000, WS_EX_WINDOWEDGE = 0x100, WS_EX_DLGMODALFRAME = 0x1;

    public static readonly IntPtr HWND_BOTTOM = new(1);
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);
    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_NOZORDER = 0x4, SWP_FRAMECHANGED = 0x20, SWP_SHOWWINDOW = 0x40, SWP_NOOWNERZORDER = 0x0200;
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_BORDER_COLOR = 34;
    public const int DWMWCP_DONOTROUND = 1, DWMWCP_ROUND = 2, DWMWCP_ROUNDSMALL = 3;
    public const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    public const int IDI_APPLICATION = 32512;
    public const int IMAGE_ICON = 1, LR_LOADFROMFILE = 0x10, LR_DEFAULTSIZE = 0x40;
    public const int SM_CXSMICON = 49, SM_CYSMICON = 50;

    public delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    public const int WM_STYLECHANGING = 0x007C;
    public const int WM_ERASEBKGND = 0x0014;
    public const int BLACK_BRUSH = 4;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(IntPtr hwnd, out RECT rect);

    [LibraryImport("user32.dll")]
    public static partial int FillRect(IntPtr hdc, ref RECT rect, IntPtr brush);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr GetStockObject(int index);

    public const uint RDW_INVALIDATE = 0x1, RDW_ERASE = 0x4, RDW_FRAME = 0x400, RDW_UPDATENOW = 0x100;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RedrawWindow(IntPtr hwnd, IntPtr rect, IntPtr region, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    public struct STYLESTRUCT { public uint styleOld, styleNew; }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd, hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    public static partial ushort RegisterClassEx(ref WNDCLASSEX wc);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateWindowEx(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static partial IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(IntPtr hwnd);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessage(string message);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);

    [LibraryImport("user32.dll")]
    public static partial IntPtr CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AppendMenu(IntPtr menu, uint flags, nuint id, string? text);

    [LibraryImport("user32.dll")]
    public static partial int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr tpm);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyMenu(IntPtr menu);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT point);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr LoadImage(IntPtr instance, string name, int type, int cx, int cy, int flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(IntPtr icon);

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetricsForDpi(int index, uint dpi);

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForSystem();

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr GetModuleHandle(string? name);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static partial IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr FindWindow(string? className, string? windowName);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? windowName);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("shell32.dll")]
    public static partial int SHQueryUserNotificationState(out int state);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial int GetClassNameNative(IntPtr hwnd, char* buffer, int maxCount);

    public static unsafe string GetWindowClassName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "";
        char* buf = stackalloc char[128];
        int len = GetClassNameNative(hwnd, buf, 128);
        return len > 0 ? new string(buf, 0, len) : "";
    }

    public readonly record struct TaskbarInfo(
        IntPtr Handle,
        RECT TrayRect,
        RECT NotifyRect,
        RECT MonitorRect,
        RECT WorkRect,
        bool Visible,
        bool AutoHidden,
        bool TrayOnLeft,
        uint Dpi);

    public static TaskbarInfo? QueryTaskbar()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero) return null;
        if (!GetWindowRect(tray, out var trayRect)) return null;
        var mon = MonitorFromWindow(tray, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(mon, ref info)) return null;
        var notify = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        RECT notifyRect = default;
        if (notify == IntPtr.Zero || !GetWindowRect(notify, out notifyRect))
            notifyRect = new RECT { Left = trayRect.Right, Top = trayRect.Top, Right = trayRect.Right, Bottom = trayRect.Bottom };

        int monMid = (info.rcMonitor.Left + info.rcMonitor.Right) / 2;
        int notifyMid = (notifyRect.Left + notifyRect.Right) / 2;
        bool trayOnLeft = notifyRect.Right > notifyRect.Left && notifyMid < monMid;
        bool visible = IsWindowVisible(tray);
        int trayW = trayRect.Right - trayRect.Left;
        int trayH = trayRect.Bottom - trayRect.Top;
        bool autoHidden = !visible || trayW < 20 || trayH < 20
            || trayRect.Top >= info.rcMonitor.Bottom - 4
            || trayRect.Bottom <= info.rcMonitor.Top + 4;
        uint dpi = GetDpiForWindow(tray);
        if (dpi == 0) dpi = GetDpiForSystem();
        if (dpi == 0) dpi = 96;
        return new TaskbarInfo(tray, trayRect, notifyRect, info.rcMonitor, info.rcWork, visible, autoHidden, trayOnLeft, dpi);
    }

    public static bool IsFullscreenOnMonitor(RECT monitorRect, IntPtr ignore1, IntPtr ignore2)
    {
        if (SHQueryUserNotificationState(out int state) == 0 && state is 2 or 3 or 4)
            return true;
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == ignore1 || fg == ignore2) return false;
        string cls = GetWindowClassName(fg);
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "NotifyIconOverflowWindow" or "Windows.UI.Core.CoreWindow")
            return false;
        if (!GetWindowRect(fg, out var r)) return false;
        return r.Left <= monitorRect.Left + 2
            && r.Top <= monitorRect.Top + 2
            && r.Right >= monitorRect.Right - 2
            && r.Bottom >= monitorRect.Bottom - 2;
    }

    public static bool IsTaskbarLightTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("SystemUsesLightTheme") is int v) return v != 0;
        }
        catch { }
        return false;
    }

    [LibraryImport("user32.dll")]
    public static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [LibraryImport("user32.dll")]
    public static partial IntPtr MonitorFromPoint(POINT point, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS { public int Left, Right, Top, Bottom; }

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(IntPtr hwnd, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsIconic(IntPtr hwnd);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    public static partial IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReleaseCapture();

    public const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4, SW_SHOW = 5, SW_RESTORE = 9;
    public const uint WM_NCLBUTTONDOWN = 0x00A1;
    public const int HTCAPTION = 2;

    public delegate IntPtr SubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, nuint refData);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc proc, nuint id, nuint refData);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc proc, nuint id);

    [DllImport("comctl32.dll")]
    public static extern IntPtr DefSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    // Strips caption/frame flags after OverlappedPresenter handles WM_STYLECHANGING so borderless popups have no white 3px border.
    public static IntPtr ForceBorderlessPopupStyle(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var result = DefSubclassProc(hwnd, msg, wParam, lParam);
        unsafe
        {
            var change = (STYLESTRUCT*)lParam;
            if (wParam.ToInt32() == GWL_STYLE)
                change->styleNew = (uint)((change->styleNew & ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU)) | WS_POPUP);
            else if (wParam.ToInt32() == GWL_EXSTYLE)
                change->styleNew = (uint)(change->styleNew & ~(WS_EX_WINDOWEDGE | WS_EX_DLGMODALFRAME));
        }
        return result;
    }

    public const uint GW_HWNDPREV = 3;
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_OBJECT_REORDER = 0x8004;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public const int OBJID_WINDOW = 0;
    public const int CHILDID_SELF = 0;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x2;

    public delegate void WinEventProc(
        IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint eventThread, uint eventTime);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc pfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWinEvent(IntPtr hook);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetWindow(IntPtr hwnd, uint cmd);

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindow(IntPtr hwnd);

    public static bool IsWindowAbove(IntPtr hwnd, IntPtr reference)
    {
        if (hwnd == IntPtr.Zero || reference == IntPtr.Zero) return false;
        var cur = GetWindow(reference, GW_HWNDPREV);
        for (int i = 0; i < 512 && cur != IntPtr.Zero; i++)
        {
            if (cur == hwnd) return true;
            cur = GetWindow(cur, GW_HWNDPREV);
        }
        return false;
    }

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SystemParametersInfo(uint action, uint param, out int value, uint winIni);

    public const uint SPI_GETCLIENTAREAANIMATION = 0x1042;

    public static RECT WorkAreaOf(IntPtr hwnd)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info);
        return info.rcWork;
    }

    public static RECT WorkAreaAt(int x, int y)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST), ref info);
        return info.rcWork;
    }

    public static bool AnimationsEnabled() => !SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, out int on, 0) || on != 0;
}
