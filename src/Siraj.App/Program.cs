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
            // Run by the uninstaller: take away what Siraj registered for the user.
            Autostart.Set(false);
            Notifier.UnregisterAll();
            return 0;
        }

#if DEBUG
        bool isolatedShot = args.Any(a => a.StartsWith("--shot="));
        if (!isolatedShot)
        {
#endif
        var instance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!instance.IsCurrent)
        {
            // --quit (from the installer and uninstaller) travels the same way and closes the running copy.
            Redirect(instance, AppInstance.GetCurrent().GetActivatedEventArgs());
            return 0;
        }
        if (args.Contains("--quit"))
        {
            // Nothing running to close.
            instance.UnregisterKey();
            return 0;
        }
        instance.Activated += (_, e) => App.OnRedirected(e);
#if DEBUG
        }
#endif

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
