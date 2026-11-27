using Siraj.Core.Places;
using Siraj.Core.Prayer;
using Siraj.Core.Settings;

namespace Siraj.Core.Adhan;

public enum AlertKind
{
    Prayer,
    Reminder
}

public sealed record PlannedAlert(PrayerType Prayer, AlertKind Kind, DateTimeOffset At, DateTimeOffset PrayerTime)
{
    public string Key => $"{Prayer}|{Kind}|{PrayerTime.ToUnixTimeSeconds()}";
}

public static class AlertPlanner
{
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    public static IReadOnlyList<PlannedAlert> Plan(AppSettings settings, Place place, DateTimeOffset now)
    {
        var alerts = new List<PlannedAlert>();
        var today = place.Today(now);
        int lead = PrePrayerReminder.Normalize(settings.PrePrayerReminderMinutes);
        var seen = new HashSet<string>();

        for (int d = -1; d <= 2; d++)
        {
            foreach (var p in PrayerCalculator.Raw(place, today.AddDays(d), settings.Calculation))
            {
                if (!settings.AlertOn(p.Type)) continue;
                var prayerAlert = new PlannedAlert(p.Type, AlertKind.Prayer, p.Time, p.Time);
                if (p.Time >= now - Grace && seen.Add(prayerAlert.Key)) alerts.Add(prayerAlert);

                // Sunrise has no adhan, so skip pre-prayer reminders.
                if (lead > 0 && p.Type != PrayerType.Sunrise)
                {
                    var at = p.Time.AddMinutes(-lead);
                    var reminder = new PlannedAlert(p.Type, AlertKind.Reminder, at, p.Time);
                    if (at >= now - Grace && at < p.Time && seen.Add(reminder.Key)) alerts.Add(reminder);
                }
            }
        }
        alerts.Sort((a, b) => a.At.CompareTo(b.At));
        return alerts;
    }
}

public static class PrePrayerReminder
{
    public const int Off = 0;
    public static readonly IReadOnlyList<int> Options = [Off, 5, 10, 15, 30];

    public static int Normalize(int minutes) => Options.Contains(minutes) ? minutes : Off;
}
