using Microsoft.Win32;

namespace Siraj.App.Services;

internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Siraj";

    public static string Command => $"\"{AppPaths.Exe}\" --background";

    public static bool IsOn
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) is string;
        }
    }

    public static void Set(bool on)
    {
        if (AppPaths.IsSandboxed) return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (on) key.SetValue(Name, Command);
            else key.DeleteValue(Name, throwOnMissingValue: false);
        }
        catch (Exception e)
        {
            Log.Error("autostart", e);
        }
    }

    public static void Sync(bool wanted)
    {
        if (AppPaths.IsSandboxed) return;
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        var current = key?.GetValue(Name) as string;
        if (wanted && current != Command) Set(true);
        if (!wanted && current != null) Set(false);
    }
}
