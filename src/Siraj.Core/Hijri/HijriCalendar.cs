using System.Collections.Concurrent;
using System.Globalization;
using Siraj.Core.Places;

namespace Siraj.Core.Hijri;

public enum HijriMethod
{
    UmmAlQura,
    Egypt,
    Turkey,
    Sighting,
    NakedEye,
    Mabims
}

public sealed record HijriRegion(string? CountryCode, HijriMethod Method, double Latitude = 0.0, double Longitude = 0.0, string ZoneId = "UTC")
{
    public static readonly HijriRegion Saudi = new("SA", HijriMethod.UmmAlQura);

    private static KeyValuePair<string, HijriRegion> Place(string code, HijriMethod method, double lat, double lon, string zone) =>
        new(code, new HijriRegion(code, method, lat, lon, zone));

    private static readonly Dictionary<string, HijriRegion> ByCountry = new([
        Place("EG", HijriMethod.Egypt, 30.0444, 31.2357, "Africa/Cairo"),
        new("TR", new HijriRegion("TR", HijriMethod.Turkey)),
        Place("JO", HijriMethod.Sighting, 31.9539, 35.9106, "Asia/Amman"),
        Place("AE", HijriMethod.Sighting, 24.4539, 54.3773, "Asia/Dubai"),
        Place("OM", HijriMethod.Sighting, 23.5880, 58.3829, "Asia/Muscat"),
        Place("IQ", HijriMethod.Sighting, 33.3152, 44.3661, "Asia/Baghdad"),
        Place("SY", HijriMethod.Sighting, 33.5138, 36.2765, "Asia/Damascus"),
        Place("MA", HijriMethod.Sighting, 34.0209, -6.8416, "Africa/Casablanca"),
        Place("DZ", HijriMethod.Sighting, 36.7538, 3.0588, "Africa/Algiers"),
        Place("TN", HijriMethod.Sighting, 36.8065, 10.1815, "Africa/Tunis"),
        Place("LY", HijriMethod.Sighting, 32.8872, 13.1913, "Africa/Tripoli"),
        Place("IR", HijriMethod.Sighting, 35.6892, 51.3890, "Asia/Tehran"),
        Place("PK", HijriMethod.NakedEye, 24.8607, 67.0011, "Asia/Karachi"),
        Place("IN", HijriMethod.NakedEye, 28.6139, 77.2090, "Asia/Kolkata"),
        Place("BD", HijriMethod.NakedEye, 23.8103, 90.4125, "Asia/Dhaka"),
        Place("MY", HijriMethod.Mabims, 6.1184, 100.3685, "Asia/Kuala_Lumpur"),
        Place("ID", HijriMethod.Mabims, 5.5483, 95.3238, "Asia/Jakarta"),
        Place("BN", HijriMethod.Mabims, 4.9031, 114.9398, "Asia/Brunei"),
        Place("SG", HijriMethod.Mabims, 1.3521, 103.8198, "Asia/Singapore"),
    ]);

    public static HijriRegion ForCountry(string? countryCode)
    {
        var code = countryCode?.ToUpperInvariant();
        return code != null && ByCountry.TryGetValue(code, out var region) ? region : new HijriRegion(code, HijriMethod.UmmAlQura);
    }
}

public readonly record struct HijriDay(int Year, int Month, int Day, int LengthOfMonth);

public sealed class HijriCalendar
{
    private const int MinYear = 1301;
    private const int MaxYear = 1599;
    private const int ChainMonths = 8;

    private static readonly DateOnly MinDate = UmmAlQuraFirst(MinYear, 1);
    private static readonly DateOnly MaxDate = UmmAlQuraFirst(MaxYear, 12);

    private static DateOnly UmmAlQuraFirst(int year, int month) =>
        DateOnly.FromDayNumber(UmmAlQuraTable.MonthStarts[(year - UmmAlQuraTable.FirstYear) * 12 + month - 1]);

    private static HijriDay UmmAlQuraDate(DateOnly date)
    {
        var starts = UmmAlQuraTable.MonthStarts;
        int i = Array.BinarySearch(starts, date.DayNumber);
        if (i < 0) i = ~i - 1;
        i = Math.Clamp(i, 0, starts.Length - 2);
        return new HijriDay(UmmAlQuraTable.FirstYear + i / 12, i % 12 + 1, date.DayNumber - starts[i] + 1, starts[i + 1] - starts[i]);
    }

    public static readonly HijriCalendar UmmAlQura = new(HijriRegion.Saudi);

    public HijriRegion Region { get; }
    public int Adjustment { get; }

    private readonly Dictionary<int, DateOnly> _starts = new();
    private readonly object _lock = new();
    private readonly Lazy<TimeZoneInfo> _zone;

    public HijriCalendar(HijriRegion region, int adjustment = 0)
    {
        Region = region;
        Adjustment = adjustment;
        _zone = new Lazy<TimeZoneInfo>(() => Zones.Find(region.ZoneId));
    }

    public HijriDay ToHijri(DateOnly date)
    {
        var shifted = date.AddDays(Adjustment);
        var uq = UmmAlQuraDate(Clamp(shifted));
        if (Region.Method == HijriMethod.UmmAlQura) return uq;

        // Use Umm al-Qura as an initial month guess, then adjust if sighting shifts the boundary.
        int index = IndexOf(uq.Year, uq.Month);
        while (Start(index) > shifted) index--;
        while (Start(index + 1) <= shifted) index++;
        var first = Start(index);
        int length = Start(index + 1).DayNumber - first.DayNumber;
        return new HijriDay(YearOf(index), MonthOf(index), shifted.DayNumber - first.DayNumber + 1, length);
    }

    public DateOnly ToGregorian(int year, int month, int day)
    {
        int index = IndexOf(year, month);
        int length = LengthOfMonth(year, month);
        return Start(index).AddDays(Math.Clamp(day, 1, length) - 1).AddDays(-Adjustment);
    }

    public DateOnly FirstDayOf(int year, int month) => ToGregorian(year, month, 1);

    public int LengthOfMonth(int year, int month)
    {
        int index = IndexOf(year, month);
        return Start(index + 1).DayNumber - Start(index).DayNumber;
    }

    private static int IndexOf(int year, int month) => year * 12 + (month - 1);
    private static int YearOf(int index) => index / 12;
    private static int MonthOf(int index) => index % 12 + 1;

    private static DateOnly UmmAlQuraStart(int index)
    {
        int year = Math.Clamp(YearOf(index), MinYear, MaxYear);
        return UmmAlQuraFirst(year, MonthOf(index));
    }

    private DateOnly Start(int index)
    {
        lock (_lock)
        {
            if (_starts.TryGetValue(index, out var cached)) return cached;
            if (Region.Method == HijriMethod.UmmAlQura || YearOf(index) <= MinYear || YearOf(index) >= MaxYear)
                return _starts[index] = UmmAlQuraStart(index);
            if (Region.Method == HijriMethod.Turkey)
                return _starts[index] = TurkishStart(index);

            // Step forward from the nearest cached month, or seed from Umm al-Qura 8 months back.
            int? known = null;
            for (int i = index - 1; i >= index - ChainMonths; i--)
            {
                if (_starts.ContainsKey(i))
                {
                    known = i;
                    break;
                }
            }
            DateOnly date;
            if (known == null)
            {
                known = index - ChainMonths;
                date = UmmAlQuraStart(known.Value);
                _starts[known.Value] = date;
            }
            else
            {
                date = _starts[known.Value];
            }
            for (int i = known.Value; i < index; i++)
            {
                var day29 = date.AddDays(28);
                date = CrescentAccepted(day29) ? day29.AddDays(1) : day29.AddDays(2);
                _starts[i + 1] = date;
            }
            return date;
        }
    }

    private bool CrescentAccepted(DateOnly day29)
    {
        var e = Crescent.Evening(day29, _zone.Value, Region.Latitude, Region.Longitude);
        if (e == null || !e.ConjunctionBeforeSunset) return false;
        return Region.Method switch
        {
            HijriMethod.Egypt => e.CentreLagMinutes >= 5.0,
            HijriMethod.Sighting => (e.OdehV ?? double.NegativeInfinity) >= -0.96,
            HijriMethod.NakedEye => (e.OdehV ?? double.NegativeInfinity) >= 2.0,
            HijriMethod.Mabims => e.MoonAltitude >= 3.0 && e.Elongation >= 6.4,
            _ => e.LagMinutes > 0
        };
    }

    private static DateOnly TurkishStart(int index)
    {
        var uq = UmmAlQuraStart(index);
        double conjunction = Ephemeris.PreviousConjunction(Ephemeris.JulianDay(UtcMillis(uq.AddDays(1), TimeOnly.MinValue)));
        var conjunctionDate = DateOnly.FromDayNumber(DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber +
                                                     (int)Math.Floor(Ephemeris.EpochMillis(conjunction) / 86_400_000.0));
        for (int offset = 0; offset <= 2; offset++)
        {
            var evening = conjunctionDate.AddDays(offset);
            if (GlobalCrescent(evening, conjunction)) return evening.AddDays(1);
        }
        return conjunctionDate.AddDays(3);
    }

    private static long UtcMillis(DateOnly date, TimeOnly time) =>
        new DateTimeOffset(date.ToDateTime(time), TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static bool GlobalCrescent(DateOnly date, double conjunction)
    {
        double midnight = Ephemeris.JulianDay(UtcMillis(date.AddDays(1), TimeOnly.MinValue));
        // At each latitude, find the longitude whose sunset falls just before 00:00 UTC (oldest moon in that UTC day).
        for (double latitude = -60.0; latitude <= 60.0; latitude += 5.0)
        {
            double longitude = -60.0;
            for (int i = 0; i < 4; i++)
            {
                var sunset = SunsetAt(date, latitude, longitude);
                if (sunset == null) continue;
                // Bias 0.5 deg east so sunset stays before 00:00 UTC.
                longitude = Math.Clamp(longitude + (sunset.Value - midnight) * 360.0 + 0.5, -180.0, 180.0);
            }
            var s = SunsetAt(date, latitude, longitude);
            if (s != null && s < midnight && conjunction < s && MeetsIstanbulCriterion(s.Value, latitude, longitude)) return true;
        }
        // Americas exception: allow western-hemisphere sighting if conjunction precedes dawn in New Zealand.
        var auckland = Zones.Find("Pacific/Auckland");
        double? newZealandDawn = Ephemeris.SunAtAltitude(
            Ephemeris.JulianDay(Zones.AtLocal(date.AddDays(1), new TimeOnly(12, 0), auckland)),
            -41.2865, 174.7762, -18.0, evening: false);
        if (newZealandDawn == null || conjunction >= newZealandDawn) return false;
        for (double latitude = -55.0; latitude <= 60.0; latitude += 5.0)
        {
            foreach (var longitude in new[] { -70.0, -90.0, -110.0, -125.0 })
            {
                var s = SunsetAt(date, latitude, longitude);
                if (s == null) continue;
                if (conjunction < s && MeetsIstanbulCriterion(s.Value, latitude, longitude)) return true;
            }
        }
        return false;
    }

    private static bool MeetsIstanbulCriterion(double sunset, double latitude, double longitude)
    {
        var sun = Ephemeris.Sun(sunset);
        var moon = Ephemeris.Moon(sunset);
        var topo = Ephemeris.TopocentricMoon(sunset, moon, latitude, longitude);
        double altitude = Ephemeris.Altitude(sunset, topo.RightAscension, topo.Declination, latitude, longitude);
        return altitude >= 5.0 && Ephemeris.Separation(sun, moon) >= 8.0;
    }

    private static double? SunsetAt(DateOnly date, double latitude, double longitude)
    {
        int offsetSeconds = Math.Clamp((int)Math.Floor(longitude / 15.0 * 3600 + 0.5), -18 * 3600, 18 * 3600);
        // Compute local solar noon with second precision (DateTimeOffset only stores minute offsets).
        long noonMillis = UtcMillis(date, new TimeOnly(12, 0)) - offsetSeconds * 1000L;
        return Crescent.SunsetOn(Ephemeris.JulianDay(noonMillis), latitude, longitude);
    }

    private static DateOnly Clamp(DateOnly date) => date < MinDate ? MinDate : date > MaxDate ? MaxDate : date;
}

public static class HijriCalendars
{
    public const string MethodAuto = "auto";
    public const string MethodUmmAlQura = "umm_al_qura";
    public const int MaxAdjustment = 2;

    private static readonly ConcurrentDictionary<string, HijriCalendar> Cache = new();

    public static HijriCalendar Current { get; private set; } = HijriCalendar.UmmAlQura;

    public static event Action<HijriCalendar>? Changed;

    public static HijriCalendar ForSettings(string? countryCode, string method, int adjustment)
    {
        var region = method == MethodUmmAlQura
            ? HijriRegion.Saudi with { CountryCode = countryCode?.ToUpperInvariant() }
            : HijriRegion.ForCountry(countryCode);
        int days = Math.Clamp(adjustment, -MaxAdjustment, MaxAdjustment);
        return Cache.GetOrAdd($"{region.Method}|{region.CountryCode}|{days}", _ => new HijriCalendar(region, days));
    }

    public static void Select(HijriCalendar calendar, DateOnly today)
    {
        var now = calendar.ToHijri(today);
        int year = now.Year, month = now.Month;
        for (int i = 0; i < 14; i++)
        {
            calendar.FirstDayOf(year, month);
            if (++month > 12)
            {
                month = 1;
                year++;
            }
        }
        if (!ReferenceEquals(Current, calendar))
        {
            Current = calendar;
            Changed?.Invoke(calendar);
        }
    }
}
