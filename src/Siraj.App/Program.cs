using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Siraj.App.Services;

namespace Siraj.App;

public static partial class Program
{
    private const string InstanceKey = "Siraj.Main.7c1e3f";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (args.Contains("--uninstall"))
        {
            // Called by the uninstaller to remove startup and notification registrations.
            Autostart.Set(false);
            Notifier.UnregisterAll();
            return 0;
        }

        var instance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!instance.IsCurrent)
        {
            // Forward activation (including --quit from the installer) to the running instance.
            Redirect(instance, AppInstance.GetCurrent().GetActivatedEventArgs());
            return 0;
        }
        if (args.Contains("--quit"))
        {
            instance.UnregisterKey();
            return 0;
        }
        instance.Activated += (_, e) => App.OnRedirected(e);

        App.StartArgs = args;
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    private static void Redirect(AppInstance target, AppActivationArguments args)
    {
        var done = CreateEvent(IntPtr.Zero, true, false, null);
        Task.Run(() =>
        {
            target.RedirectActivationToAsync(args).AsTask().Wait();
            SetEvent(done);
        });
        CoWaitForMultipleObjects(0, 0xFFFFFFFF, 1, [done], out _);
        CloseHandle(done);
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateEventW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateEvent(IntPtr attributes, [MarshalAs(UnmanagedType.Bool)] bool manualReset, [MarshalAs(UnmanagedType.Bool)] bool initialState, string? name);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetEvent(IntPtr handle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    [LibraryImport("ole32.dll")]
    private static partial int CoWaitForMultipleObjects(uint flags, uint timeout, uint count, IntPtr[] handles, out uint index);
}
