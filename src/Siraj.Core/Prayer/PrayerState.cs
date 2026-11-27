namespace Siraj.Core.Prayer;

public abstract record PrayerState(PrayerTime Prayer)
{
    public sealed record UpcomingAzan(PrayerTime Prayer, TimeSpan Remaining) : PrayerState(Prayer);
    public sealed record AzanCalled(PrayerTime Prayer, TimeSpan SinceAzan, DateTimeOffset IqamaTime) : PrayerState(Prayer);
    public sealed record WaitingForIqama(PrayerTime Prayer, DateTimeOffset IqamaTime, TimeSpan UntilIqama, double Progress) : PrayerState(Prayer);
    public sealed record InPrayer(PrayerTime Prayer, TimeSpan SinceIqama) : PrayerState(Prayer);

    public static PrayerState? At(PrayerDay day, DateTimeOffset now)
    {
        foreach (var pt in day.Times)
        {
            if (pt.Type == PrayerType.Sunrise) continue;
            var start = pt.Time;
            var iqama = pt.Iqama ?? start.AddMinutes(20);
            if (now < start || now >= iqama.AddMinutes(15)) continue;

            if (now < start.AddMinutes(2)) return new AzanCalled(pt, Max0(now - start), iqama);
            if (now < iqama)
            {
                double total = Math.Max(1, (iqama - start).TotalMilliseconds);
                var remaining = Max0(iqama - now);
                return new WaitingForIqama(pt, iqama, remaining, Math.Clamp((total - remaining.TotalMilliseconds) / total, 0, 1));
            }
            return new InPrayer(pt, Max0(now - iqama));
        }

        // Prefer a future prayer over a stale IsNext flag.
        var next = day.Times.FirstOrDefault(t => t.IsNext && t.Time > now) ?? day.Times.FirstOrDefault(t => t.Time > now);
        return next == null ? null : new UpcomingAzan(next, next.Time - now);
    }

    private static TimeSpan Max0(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;
}
