using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Siraj.App.Services;
using Siraj.App.Views;
using Siraj.App.Widgets;
using Siraj.Core.Adhan;
using Siraj.Core.Settings;

namespace Siraj.App;

public partial class App : Application
{
    internal static SettingsStore Settings { get; private set; } = null!;
    internal static Core.Devotions.JournalStore Journal { get; private set; } = null!;
    internal static ThemeService Theme { get; private set; } = null!;
    internal static PrayerClock Clock { get; private set; } = null!;
    internal static AdhanService Adhan { get; private set; } = null!;
    internal static TrayIcon Tray { get; private set; } = null!;
    internal static WidgetManager Widgets { get; private set; } = null!;
    internal static TaskbarWidgetController? TaskbarWidget { get; private set; }
    internal static QuranService Quran { get; private set; } = null!;
    internal static Quran.QuranPlayer QuranPlayer { get; private set; } = null!;
    internal static MediaHubService Media { get; private set; } = null!;
    internal static UpdateService Updates { get; private set; } = null!;
    internal static DispatcherQueue Dispatcher { get; private set; } = null!;

    private static MainWindow? _main;

    internal static MainWindow? MainWindowInstance => _main;

    internal static string[] StartArgs { get; set; } = [];

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            Log.Error($"unhandled ({e.Message})", e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("fatal", e.ExceptionObject as Exception);
#if DEBUG
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            if (e.Exception is System.Runtime.InteropServices.COMException com)
                Log.Info($"first chance {com.HResult:X8} {com.Message.Trim()} at {Environment.StackTrace}");
        };
#endif
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("unobserved task", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Keep running in the tray after the main window closes.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        Dispatcher = DispatcherQueue.GetForCurrentThread();

        Settings = new SettingsStore(AppPaths.Data);
        if (!Settings.Current.SetupDone && !File.Exists(Path.Combine(AppPaths.Data, "settings.json")))
            Settings.Update(s => s with { Language = Loc.SystemLanguage() });
        Loc.Use(Settings.Current.Language);
        Journal = new Core.Devotions.JournalStore(AppPaths.Data);
        Log.Info($"start {string.Join(' ', StartArgs)}");

        Theme = new ThemeService();
        Theme.Apply();
        Clock = new PrayerClock(Dispatcher);
#if DEBUG
        ApplyPreviewTime();
#endif
        Adhan = new AdhanService(Dispatcher);
        Quran = new QuranService();
        QuranPlayer = new Quran.QuranPlayer(Dispatcher);
        Media = new MediaHubService(Dispatcher);
        QuranPlayer.Changed += () =>
        {
            if (QuranPlayer.IsPlaying && Media.IsPlaying)
                Media.Pause();
        };
        Tray = new TrayIcon();
        Tray.TimeShifted += () =>
        {
            Clock.Recompute();
            Adhan.Replan();
        };
        Clock.Tick += () => Tray.Update(TrayIcon.Tooltip(Clock));
        Clock.DayChanged += () => Tray.Update(TrayIcon.Tooltip(Clock));
        Tray.Update(TrayIcon.Tooltip(Clock));
        Notifier.Register();
        Widgets = new WidgetManager();
        TaskbarWidget = new TaskbarWidgetController();
        Tray.TaskbarCreated += () => Dispatcher.TryEnqueue(() => TaskbarWidget?.OnTaskbarCreated());

        Settings.Changed += OnSettingsChanged;
        if (Settings.Current.SetupDone)
        {
            Autostart.Sync(Settings.Current.StartWithWindows);
            VoiceDownloads.EnsureFor(AdhanVoices.Find(Settings.Current.AdhanVoice), Settings.Current.FajrAdhan);
        }

        Widgets.Restore();
        Updates = new UpdateService(Dispatcher, justUpdated: StartArgs.Contains("--updated"));
        TaskbarWidget.Start();
        Updates.StartBackgroundCheck();

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (activation.Kind == ExtendedActivationKind.AppNotification && activation.Data is AppNotificationActivatedEventArgs toast)
        {
            Notifier.Invoked(toast.Arguments);
        }
        else if (!StartArgs.Contains("--background") || !Settings.Current.SetupDone)
        {
            ShowMainWindow();
        }
    }

#if DEBUG
    private static void ApplyPreviewTime()
    {
        var arg = StartArgs.FirstOrDefault(a => a.StartsWith("--sky-time="));
        if (arg != null && DateTimeOffset.TryParse(arg["--sky-time=".Length..], out var at))
        {
            Clock.PreviewOffset = at - DateTimeOffset.Now;
            Clock.Recompute();
        }
    }
#endif

    private static void OnSettingsChanged(AppSettings old, AppSettings updated)
    {
        Dispatcher.TryEnqueue(() =>
        {
            if (old.Language != updated.Language)
            {
                Loc.Use(updated.Language);
                _main?.Rebuild();
                Widgets.Refresh();
                TaskbarWidget?.Refresh();
            }
            if (old.Palette != updated.Palette || old.ThemeMode != updated.ThemeMode || old.BlackMode != updated.BlackMode ||
                old.GlassUi != updated.GlassUi || old.GlassFinish != updated.GlassFinish || old.LiveBackground != updated.LiveBackground)
                Theme.Apply();
            if (updated.SetupDone && (old.StartWithWindows != updated.StartWithWindows || !old.SetupDone))
                Autostart.Set(updated.StartWithWindows);
            if (updated.SetupDone && (old.AdhanVoice != updated.AdhanVoice || old.FajrAdhan != updated.FajrAdhan || !old.SetupDone))
                VoiceDownloads.EnsureFor(AdhanVoices.Find(updated.AdhanVoice), updated.FajrAdhan);
            if (old.HourFormat != updated.HourFormat || old.NumeralSystem != updated.NumeralSystem || old.AlertType != updated.AlertType)
            {
                Widgets.Refresh();
                TaskbarWidget?.Refresh();
            }
            if (old.TaskbarWidget != updated.TaskbarWidget || old.SetupDone != updated.SetupDone)
                TaskbarWidget?.Apply(updated.TaskbarWidget);
            Tray.Update(TrayIcon.Tooltip(Clock));
        });
    }

    internal static void ShowMainWindow() => ShowMainWindow(null);

    internal static void ShowMainWindow(string? page)
    {
        if (_main == null)
        {
            _main = new MainWindow();
            _main.Closed += (_, _) => _main = null;
        }
        _main.Open(page);
    }

    internal static void OnRedirected(AppActivationArguments args)
    {
        Dispatcher.TryEnqueue(() =>
        {
            if (args.Kind == ExtendedActivationKind.AppNotification && args.Data is AppNotificationActivatedEventArgs toast)
                Notifier.Invoked(toast.Arguments);
            else if (args.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch && launch.Arguments.Contains("--quit"))
                Quit();
            else if (args.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs bg && bg.Arguments.Contains("--background"))
                return;
            else
                ShowMainWindow();
        });
    }

    internal static void Quit()
    {
        Log.Info("quit");
        Updates?.Dispose();
        QuranPlayer?.Stop();
        Media?.Dispose();
        Journal?.Dispose();
        Adhan?.Dispose();
        TaskbarWidget?.Dispose();
        Widgets?.CloseAll();
        if (Tray != null)
        {
            Tray.Dispose();
            Notifier.Unregister();
        }
        _main?.Close();
        Current.Exit();
    }
}
