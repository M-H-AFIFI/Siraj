namespace Siraj.Core.Adhan;

public readonly record struct Coordinates(double Latitude, double Longitude);

public enum Madhab { Shafi, Hanafi }

public enum HighLatitudeRule { MiddleOfTheNight, SeventhOfTheNight, TwilightAngle }

public enum CalculationMethod
{
    MuslimWorldLeague,
    Egyptian,
    Karachi,
    UmmAlQura,
    Dubai,
    MoonSightingCommittee,
    NorthAmerica,
    Kuwait,
    Qatar,
    Singapore,
    Other
}

public sealed class PrayerAdjustments
{
    public int Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha;

    public PrayerAdjustments() { }

    public PrayerAdjustments(int fajr, int sunrise, int dhuhr, int asr, int maghrib, int isha)
    {
        Fajr = fajr; Sunrise = sunrise; Dhuhr = dhuhr; Asr = asr; Maghrib = maghrib; Isha = isha;
    }
}

public sealed class CalculationParameters
{
    public CalculationMethod Method = CalculationMethod.Other;
    public double FajrAngle;
    public double IshaAngle;
    public int IshaInterval;
    public Madhab Madhab = Madhab.Shafi;
    public HighLatitudeRule HighLatitudeRule = HighLatitudeRule.MiddleOfTheNight;
    public PrayerAdjustments Adjustments = new();
    public PrayerAdjustments MethodAdjustments = new();

    public CalculationParameters(double fajrAngle, double ishaAngle, CalculationMethod method)
    {
        FajrAngle = fajrAngle;
        IshaAngle = ishaAngle;
        Method = method;
    }

    public static CalculationParameters WithInterval(double fajrAngle, int ishaInterval, CalculationMethod method) =>
        new(fajrAngle, 0.0, method) { IshaInterval = ishaInterval };

    public CalculationParameters WithMethodAdjustments(PrayerAdjustments adjustments)
    {
        MethodAdjustments = adjustments;
        return this;
    }

    internal (double Fajr, double Isha) NightPortions() => HighLatitudeRule switch
    {
        HighLatitudeRule.MiddleOfTheNight => (1.0 / 2.0, 1.0 / 2.0),
        HighLatitudeRule.SeventhOfTheNight => (1.0 / 7.0, 1.0 / 7.0),
        _ => (FajrAngle / 60.0, IshaAngle / 60.0)
    };

    public static CalculationParameters For(CalculationMethod method) => method switch
    {
        CalculationMethod.MuslimWorldLeague => new CalculationParameters(18.0, 17.0, method).WithMethodAdjustments(new(0, 0, 1, 0, 0, 0)),
        CalculationMethod.Egyptian => new CalculationParameters(19.5, 17.5, method).WithMethodAdjustments(new(0, 0, 1, 0, 0, 0)),
        CalculationMethod.Karachi => new CalculationParameters(18.0, 18.0, method).WithMethodAdjustments(new(0, 0, 1, 0, 0, 0)),
        CalculationMethod.UmmAlQura => WithInterval(18.5, 90, method),
        CalculationMethod.Dubai => new CalculationParameters(18.2, 18.2, method).WithMethodAdjustments(new(0, -3, 3, 3, 3, 0)),
        CalculationMethod.MoonSightingCommittee => new CalculationParameters(18.0, 18.0, method).WithMethodAdjustments(new(0, 0, 5, 0, 3, 0)),
        CalculationMethod.NorthAmerica => new CalculationParameters(15.0, 15.0, method).WithMethodAdjustments(new(0, 0, 1, 0, 0, 0)),
        CalculationMethod.Kuwait => new CalculationParameters(18.0, 17.5, method),
        CalculationMethod.Qatar => WithInterval(18.0, 90, method),
        CalculationMethod.Singapore => new CalculationParameters(20.0, 18.0, method).WithMethodAdjustments(new(0, 0, 1, 0, 0, 0)),
        _ => new CalculationParameters(0.0, 0.0, CalculationMethod.Other)
    };
}

public sealed class PrayerTimes
{
    public readonly DateTime? Fajr;
    public readonly DateTime? Sunrise;
    public readonly DateTime? Dhuhr;
    public readonly DateTime? Asr;
    public readonly DateTime? Maghrib;
    public readonly DateTime? Isha;

    public PrayerTimes(Coordinates coordinates, DateOnly date, CalculationParameters parameters)
    {
        DateTime? tempFajr = null, tempSunrise = null, tempDhuhr = null, tempAsr = null, tempMaghrib = null, tempIsha = null;

        var prayerDate = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        int dayOfYear = prayerDate.DayOfYear;
        var tomorrow = DateOnly.FromDateTime(prayerDate.AddDays(1));

        var solarTime = new SolarTime(date, coordinates);
        var transit = AtTime(solarTime.Transit, date);
        var sunriseComponents = AtTime(solarTime.Sunrise, date);
        var sunsetComponents = AtTime(solarTime.Sunset, date);
        var tomorrowSolarTime = new SolarTime(tomorrow, coordinates);
        var tomorrowSunrise = AtTime(tomorrowSolarTime.Sunrise, tomorrow);

        bool error = transit == null || sunriseComponents == null || sunsetComponents == null || tomorrowSunrise == null;
        if (!error)
        {
            tempDhuhr = transit;
            tempSunrise = sunriseComponents;
            tempMaghrib = sunsetComponents;

            tempAsr = AtTime(solarTime.Afternoon(parameters.Madhab == Madhab.Hanafi ? 2.0 : 1.0), date);

            // The night's length, to the millisecond as the original counts it.
            long night = (long)(tomorrowSunrise!.Value - sunsetComponents!.Value).TotalMilliseconds;

            tempFajr = AtTime(solarTime.HourAngle(-parameters.FajrAngle, false), date);
            if (parameters.Method == CalculationMethod.MoonSightingCommittee && coordinates.Latitude >= 55)
                tempFajr = sunriseComponents!.Value.AddSeconds(-1 * (int)(night / 7000));

            var portions = parameters.NightPortions();
            DateTime safeFajr;
            if (parameters.Method == CalculationMethod.MoonSightingCommittee)
            {
                safeFajr = SeasonAdjustedMorningTwilight(coordinates.Latitude, dayOfYear, date.Year, sunriseComponents!.Value);
            }
            else
            {
                long nightFraction = (long)(portions.Fajr * night / 1000);
                safeFajr = sunriseComponents!.Value.AddSeconds(-1 * (int)nightFraction);
            }
            if (tempFajr == null || tempFajr.Value < safeFajr) tempFajr = safeFajr;

            if (parameters.IshaInterval > 0)
            {
                tempIsha = tempMaghrib!.Value.AddSeconds(parameters.IshaInterval * 60);
            }
            else
            {
                tempIsha = AtTime(solarTime.HourAngle(-parameters.IshaAngle, true), date);
                if (parameters.Method == CalculationMethod.MoonSightingCommittee && coordinates.Latitude >= 55)
                    tempIsha = sunsetComponents!.Value.AddSeconds((int)(night / 7000));

                DateTime safeIsha;
                if (parameters.Method == CalculationMethod.MoonSightingCommittee)
                {
                    safeIsha = SeasonAdjustedEveningTwilight(coordinates.Latitude, dayOfYear, date.Year, sunsetComponents!.Value);
                }
                else
                {
                    long nightFraction = (long)(portions.Isha * night / 1000);
                    safeIsha = sunsetComponents!.Value.AddSeconds((int)nightFraction);
                }
                if (tempIsha == null || tempIsha.Value > safeIsha) tempIsha = safeIsha;
            }
        }

        if (error || tempAsr == null) return;

        var a = parameters.Adjustments;
        var m = parameters.MethodAdjustments;
        Fajr = Final(tempFajr!.Value, a.Fajr, m.Fajr);
        Sunrise = Final(tempSunrise!.Value, a.Sunrise, m.Sunrise);
        Dhuhr = Final(tempDhuhr!.Value, a.Dhuhr, m.Dhuhr);
        Asr = Final(tempAsr.Value, a.Asr, m.Asr);
        Maghrib = Final(tempMaghrib!.Value, a.Maghrib, m.Maghrib);
        Isha = Final(tempIsha!.Value, a.Isha, m.Isha);
    }

    private static DateTime Final(DateTime time, int adjustment, int methodAdjustment) =>
        RoundedMinute(time.AddMinutes(adjustment).AddMinutes(methodAdjustment));

    internal static DateTime RoundedMinute(DateTime when)
    {
        var minute = new DateTime(when.Year, when.Month, when.Day, when.Hour, when.Minute, 0, DateTimeKind.Utc);
        return when.Second >= 30 ? minute.AddMinutes(1) : minute;
    }

    internal static DateTime? AtTime(double value, DateOnly date)
    {
        if (double.IsInfinity(value) || double.IsNaN(value)) return null;
        double hours = Math.Floor(value);
        double minutes = Math.Floor((value - hours) * 60.0);
        double seconds = Math.Floor((value - (hours + minutes / 60.0)) * 60 * 60);
        return date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            .AddMinutes((int)minutes).AddSeconds((int)seconds).AddHours((int)hours);
    }

    private static DateTime SeasonAdjustedMorningTwilight(double latitude, int day, int year, DateTime sunrise)
    {
        double a = 75 + 28.65 / 55.0 * Math.Abs(latitude);
        double b = 75 + 19.44 / 55.0 * Math.Abs(latitude);
        double c = 75 + 32.74 / 55.0 * Math.Abs(latitude);
        double d = 75 + 48.10 / 55.0 * Math.Abs(latitude);
        double adjustment = SeasonCurve(a, b, c, d, DaysSinceSolstice(day, year, latitude));
        return sunrise.AddSeconds(-(int)DoubleUtil.JavaRound(adjustment * 60.0));
    }

    private static DateTime SeasonAdjustedEveningTwilight(double latitude, int day, int year, DateTime sunset)
    {
        double a = 75 + 25.60 / 55.0 * Math.Abs(latitude);
        double b = 75 + 2.050 / 55.0 * Math.Abs(latitude);
        double c = 75 - 9.210 / 55.0 * Math.Abs(latitude);
        double d = 75 + 6.140 / 55.0 * Math.Abs(latitude);
        double adjustment = SeasonCurve(a, b, c, d, DaysSinceSolstice(day, year, latitude));
        return sunset.AddSeconds((int)DoubleUtil.JavaRound(adjustment * 60.0));
    }

    private static double SeasonCurve(double a, double b, double c, double d, int dyy)
    {
        if (dyy < 91) return a + (b - a) / 91.0 * dyy;
        if (dyy < 137) return b + (c - b) / 46.0 * (dyy - 91);
        if (dyy < 183) return c + (d - c) / 46.0 * (dyy - 137);
        if (dyy < 229) return d + (c - d) / 46.0 * (dyy - 183);
        if (dyy < 275) return c + (b - c) / 46.0 * (dyy - 229);
        return b + (a - b) / 91.0 * (dyy - 275);
    }

    internal static int DaysSinceSolstice(int dayOfYear, int year, double latitude)
    {
        bool isLeapYear = DateTime.IsLeapYear(year);
        const int northernOffset = 10;
        int southernOffset = isLeapYear ? 173 : 172;
        int daysInYear = isLeapYear ? 366 : 365;
        int days;
        if (latitude >= 0)
        {
            days = dayOfYear + northernOffset;
            if (days >= daysInYear) days -= daysInYear;
        }
        else
        {
            days = dayOfYear - southernOffset;
            if (days < 0) days += daysInYear;
        }
        return days;
    }
}

public static class Qibla
{
    private static readonly Coordinates Makkah = new(21.4225241, 39.8261818);

    public static double Direction(Coordinates coordinates)
    {
        double longitudeDelta = DoubleUtil.ToRadians(Makkah.Longitude) - DoubleUtil.ToRadians(coordinates.Longitude);
        double latitudeRadians = DoubleUtil.ToRadians(coordinates.Latitude);
        double term1 = Math.Sin(longitudeDelta);
        double term2 = Math.Cos(latitudeRadians) * Math.Tan(DoubleUtil.ToRadians(Makkah.Latitude));
        double term3 = Math.Sin(latitudeRadians) * Math.Cos(longitudeDelta);
        double angle = Math.Atan2(term1, term2 - term3);
        return DoubleUtil.UnwindAngle(DoubleUtil.ToDegrees(angle));
    }
}
