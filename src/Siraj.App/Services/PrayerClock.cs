using Microsoft.UI.Dispatching;
using Siraj.Core.Hijri;
using Siraj.Core.Places;
using Siraj.Core.Prayer;
using Siraj.Core.Settings;

namespace Siraj.App.Services;

internal sealed class PrayerClock
{
    private readonly DispatcherQueueTimer _timer;
    private int _secondSubscribers;
    private DateTimeOffset _lastCompute;
    private long _lastMinute = -1;

    public TimeSpan PreviewOffset { get; set; }

    public DateTimeOffset Now => DateTimeOffset.Now + PreviewOffset;

    public Place? Place { get; private set; }
    public PrayerDay? Day { get; private set; }
    public PrayerState? State { get; private set; }
    public HijriDay Hijri { get; private set; }

    public event Action? DayChanged;

    public event Action? Tick;

    public PrayerClock(DispatcherQueue dispatcher)
    {
        _timer = dispatcher.CreateTimer();
        _timer.Tick += (_, _) => OnTick();
        App.Settings.Changed += (old, updated) =>
        {
            if (old.Location != updated.Location || old.Calculation != updated.Calculation ||
                !old.MinuteOffsets.SequenceEqual(updated.MinuteOffsets) ||
                old.HijriMethod != updated.HijriMethod || old.HijriAdjustment != updated.HijriAdjustment)
                dispatcher.TryEnqueue(Recompute);
        };
        Recompute();
        Schedule();
    }

    public IDisposable SubscribeSeconds()
    {
        _secondSubscribers++;
        Schedule();
        return new Release(this);
    }

    private sealed class Release(PrayerClock clock) : IDisposable
    {
        private bool _done;
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            clock._secondSubscribers--;
            clock.Schedule();
        }
    }

    private void Schedule()
    {
        _timer.Stop();
        if (_secondSubscribers > 0)
        {
            // Aligned to the second, so a countdown never shows the same second twice.
            _timer.Interval = TimeSpan.FromMilliseconds(1000 - DateTime.Now.Millisecond + 5);
        }
        else
        {
            _timer.Interval = TimeSpan.FromSeconds(60 - DateTime.Now.Second + 0.05);
        }
        _timer.IsRepeating = false;
        _timer.Start();
    }

    private void OnTick()
    {
        var now = Now;
        if (Day == null || Place == null) { Recompute(); }
        else
        {
            var state = PrayerState.At(Day, now);
            long minute = now.ToUnixTimeSeconds() / 60;
            // A new date at the place, a prayer window that closed (so "next" moves on), or a
            // state that ran off the end of the list: work the day out again. At most every 5 s.
            bool stale = Place.Today(now) != Day.Date || state == null ||
                         (State is PrayerState.InPrayer && state is PrayerState.UpcomingAzan) ||
                         (State is PrayerState.UpcomingAzan u && now >= u.Prayer.Time);
            if ((stale || minute != _lastMinute) && now - _lastCompute > TimeSpan.FromSeconds(5)) Recompute();
            else State = state;
            _lastMinute = minute;
        }
        Tick?.Invoke();
        Schedule();
    }

    public void Recompute()
    {
        var settings = App.Settings.Current;
        var now = Now;
        _lastCompute = now;
        Place = settings.Location;
        if (Place == null)
        {
            Day = null;
            State = null;
            DayChanged?.Invoke();
            return;
        }
        var day = PrayerCalculator.Today(Place, settings.Calculation, now);
        var calendar = HijriCalendars.ForSettings(Place.Country, settings.HijriMethod, settings.HijriAdjustment);
        // The calendar works out the coming year's months the first time it is asked; off the UI thread.
        if (!ReferenceEquals(calendar, HijriCalendars.Current))
            Task.Run(() => HijriCalendars.Select(calendar, day.Date)).ContinueWith(_ => App.Dispatcher.TryEnqueue(() =>
            {
                Hijri = calendar.ToHijri(Place?.Today(Now) ?? day.Date);
                DayChanged?.Invoke();
            }));
        bool changed = Day == null || Day.Date != day.Date || !Day.Times.SequenceEqual(day.Times) || Day.Place != day.Place;
        Day = day;
        State = PrayerState.At(day, now);
        Hijri = HijriCalendars.Current.ToHijri(day.Date);
        if (changed) DayChanged?.Invoke();
    }

    public PrayerTime? Focus => State?.Prayer ?? Day?.Next;
}
