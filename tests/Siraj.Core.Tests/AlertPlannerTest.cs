using Siraj.Core.Adhan;
using Siraj.Core.Places;
using Siraj.Core.Prayer;
using Siraj.Core.Settings;
using Xunit;

namespace Siraj.Core.Tests;

public class AlertPlannerTest
{
    private static readonly Place Cairo = Place.FromCity(Cities.Search("Cairo")[0]);
    private static readonly AppSettings Settings = new() { Location = Cairo, CalculationMethod = "Egyptian" };

    [Fact]
    public void PlansEnabledPrayersInOrder()
    {
        var now = new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
        var plan = AlertPlanner.Plan(Settings, Cairo, now);
        Assert.Equal(plan.OrderBy(a => a.At), plan);
        Assert.Equal(plan.Count, plan.Select(a => a.Key).Distinct().Count());
        Assert.DoesNotContain(plan, a => a.Prayer == PrayerType.Sunrise);
        Assert.All(plan, a => Assert.True(a.At >= now - AlertPlanner.Grace));
        Assert.Equal(PrayerType.Fajr, plan[0].Prayer);
        Assert.True(plan.Count >= 10, $"only {plan.Count} alerts over two days");
    }

    [Fact]
    public void DropsLateAdhanPastGraceWindow()
    {
        var dhuhr = PrayerCalculator.Raw(Cairo, new DateOnly(2026, 10, 30), Settings.Calculation)[2].Time;
        Assert.Contains(AlertPlanner.Plan(Settings, Cairo, dhuhr.AddSeconds(90)), a => a.Prayer == PrayerType.Dhuhr && a.At == dhuhr);
        Assert.DoesNotContain(AlertPlanner.Plan(Settings, Cairo, dhuhr.AddMinutes(3)), a => a.Prayer == PrayerType.Dhuhr && a.At == dhuhr);
    }

    [Fact]
    public void PlansRemindersForEnabledPrayersOnly()
    {
        var settings = (Settings with { PrePrayerReminderMinutes = 10 }).WithAlert(PrayerType.Asr, false);
        var plan = AlertPlanner.Plan(settings, Cairo, new DateTimeOffset(2026, 10, 30, 3, 0, 0, TimeSpan.Zero));
        Assert.DoesNotContain(plan, a => a.Prayer == PrayerType.Asr);
        var reminder = plan.First(a => a.Kind == AlertKind.Reminder);
        Assert.Equal(reminder.PrayerTime.AddMinutes(-10), reminder.At);
        Assert.Equal(0, PrePrayerReminder.Normalize(7));
    }
}
