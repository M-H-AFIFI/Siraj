using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Siraj.Core.Prayer;

namespace Siraj.App.Services;

internal static class Notifier
{
    private static bool _registered;

    public static void Register()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += (_, args) => App.Dispatcher.TryEnqueue(() => Invoked(args.Arguments));
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception e)
        {
            // Notifications are a courtesy on top of the adhan, which plays regardless.
            Log.Error("notification registration failed", e);
        }
    }

    public static void Unregister()
    {
        try { if (_registered) AppNotificationManager.Default.Unregister(); } catch { }
    }

    public static void UnregisterAll()
    {
        try { AppNotificationManager.Default.UnregisterAll(); } catch { }
    }

    public static void Invoked(IDictionary<string, string> arguments)
    {
        arguments.TryGetValue("action", out var action);
        switch (action)
        {
            case "stop":
                App.Adhan.Stop();
                break;
            case "settings":
                App.ShowMainWindow("settings");
                break;
            default:
                App.ShowMainWindow();
                break;
        }
    }

    public static void ShowPrayer(PrayerType prayer, DateTimeOffset time, bool sound, bool adhan)
    {
        var loc = Loc.Current;
        var name = loc.PrayerName(new PrayerTime(prayer, time));
        var builder = new AppNotificationBuilder()
            .AddArgument("action", "open")
            .AddText(prayer == PrayerType.Sunrise ? name : $"{loc["adhan_screen_now"]} {name}")
            .AddText(prayer == PrayerType.Sunrise ? loc["reminders_sunrise_desc"] : loc.Time(time))
            .SetTimeStamp(time.LocalDateTime);
        var iqama = PrayerCalculator.IqamaMinutes(prayer);
        if (iqama > 0) builder.AddText(loc.Format("adhan_notification_iqama", loc.Time(time.AddMinutes(iqama))));
        if (adhan)
        {
            builder.AddButton(new AppNotificationButton(loc["adhan_screen_stop"]).AddArgument("action", "stop"));
            builder.SetScenario(AppNotificationScenario.Reminder);
        }
        if (!sound || adhan) builder.MuteAudio();
        Show(builder);
    }

    public static void ShowReminder(PrayerType prayer, int minutes, DateTimeOffset prayerTime)
    {
        var loc = Loc.Current;
        var name = loc.PrayerName(new PrayerTime(prayer, prayerTime));
        var builder = new AppNotificationBuilder()
            .AddArgument("action", "open")
            .AddText(loc.Format("win_reminder_title", name, loc.Number(minutes)))
            .AddText(loc.Time(prayerTime));
        if (App.Settings.Current.AlertType == "silent") builder.MuteAudio();
        Show(builder);
    }

    public static void ShowText(string title, string body)
    {
        Show(new AppNotificationBuilder().AddArgument("action", "open").AddText(title).AddText(body).MuteAudio());
    }

    public static void ShowUpdateAvailable(string version)
    {
        var loc = Loc.Current;
        string v = $"v{loc.Digitize(version)}";
        Show(new AppNotificationBuilder()
            .AddArgument("action", "settings")
            .AddText(loc.Format("win_update_available_title", v))
            .AddText(loc["win_update_available_body"])
            .MuteAudio());
    }

    private static void Show(AppNotificationBuilder builder)
    {
        if (!_registered) return;
        try
        {
            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception e)
        {
            Log.Error("showing a notification", e);
        }
    }
}
