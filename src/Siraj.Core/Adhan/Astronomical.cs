namespace Siraj.Core.Adhan;

// Based on batoulapps/adhan-java 1.2.1 (MIT). Equations follow Jean Meeus, Astronomical Algorithms.

internal static class DoubleUtil
{
    public static double NormalizeWithBound(double value, double max) => value - max * Math.Floor(value / max);

    public static double UnwindAngle(double value) => NormalizeWithBound(value, 360);

    public static double ClosestAngle(double angle)
    {
        if (angle >= -180 && angle <= 180) return angle;
        return angle - 360 * JavaRound(angle / 360);
    }

    public static long JavaRound(double value) => (long)Math.Floor(value + 0.5);

    public static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    public static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}

internal static class CalendricalHelper
{
    /* Equation from Astronomical Algorithms page 60 */
    public static double JulianDay(int year, int month, int day, double hours = 0.0)
    {
        // Integer conversion is intentional, for the decimal truncation.
        int y = month > 2 ? year : year - 1;
        int m = month > 2 ? month : month + 12;
        double d = day + hours / 24;
        int a = y / 100;
        int b = 2 - a + a / 4;
        int i0 = (int)(365.25 * (y + 4716));
        int i1 = (int)(30.6001 * (m + 1));
        return i0 + i1 + d + b - 1524.5;
    }

    /* Equation from Astronomical Algorithms page 163 */
    public static double JulianCentury(double jd) => (jd - 2451545.0) / 36525;
}

internal static class Astronomical
{
    /* page 163 */
    public static double MeanSolarLongitude(double t)
    {
        double l0 = 280.4664567 + 36000.76983 * t + 0.0003032 * Math.Pow(t, 2);
        return DoubleUtil.UnwindAngle(l0);
    }

    /* page 144 */
    public static double MeanLunarLongitude(double t)
    {
        double lp = 218.3165 + 481267.8813 * t;
        return DoubleUtil.UnwindAngle(lp);
    }

    /* page 164 */
    public static double ApparentSolarLongitude(double t, double l0)
    {
        double longitude = l0 + SolarEquationOfTheCenter(t, MeanSolarAnomaly(t));
        double omega = 125.04 - 1934.136 * t;
        double lambda = longitude - 0.00569 - 0.00478 * Math.Sin(DoubleUtil.ToRadians(omega));
        return DoubleUtil.UnwindAngle(lambda);
    }

    /* page 144 */
    public static double AscendingLunarNodeLongitude(double t)
    {
        double omega = 125.04452 - 1934.136261 * t + 0.0020708 * Math.Pow(t, 2) + Math.Pow(t, 3) / 450000;
        return DoubleUtil.UnwindAngle(omega);
    }

    /* page 163 */
    public static double MeanSolarAnomaly(double t)
    {
        double m = 357.52911 + 35999.05029 * t - 0.0001537 * Math.Pow(t, 2);
        return DoubleUtil.UnwindAngle(m);
    }

    /* page 164 */
    public static double SolarEquationOfTheCenter(double t, double m)
    {
        double mrad = DoubleUtil.ToRadians(m);
        double term1 = (1.914602 - 0.004817 * t - 0.000014 * Math.Pow(t, 2)) * Math.Sin(mrad);
        double term2 = (0.019993 - 0.000101 * t) * Math.Sin(2 * mrad);
        double term3 = 0.000289 * Math.Sin(3 * mrad);
        return term1 + term2 + term3;
    }

    /* page 147 */
    public static double MeanObliquityOfTheEcliptic(double t) =>
        23.439291 - 0.013004167 * t - 0.0000001639 * Math.Pow(t, 2) + 0.0000005036 * Math.Pow(t, 3);

    /* page 165 */
    public static double ApparentObliquityOfTheEcliptic(double t, double epsilon0)
    {
        double o = 125.04 - 1934.136 * t;
        return epsilon0 + 0.00256 * Math.Cos(DoubleUtil.ToRadians(o));
    }

    /* page 165 */
    public static double MeanSiderealTime(double t)
    {
        double jd = t * 36525 + 2451545.0;
        double theta = 280.46061837 + 360.98564736629 * (jd - 2451545) + 0.000387933 * Math.Pow(t, 2) - Math.Pow(t, 3) / 38710000;
        return DoubleUtil.UnwindAngle(theta);
    }

    /* page 144 */
    public static double NutationInLongitude(double t, double l0, double lp, double omega)
    {
        double term1 = -17.2 / 3600 * Math.Sin(DoubleUtil.ToRadians(omega));
        double term2 = 1.32 / 3600 * Math.Sin(2 * DoubleUtil.ToRadians(l0));
        double term3 = 0.23 / 3600 * Math.Sin(2 * DoubleUtil.ToRadians(lp));
        double term4 = 0.21 / 3600 * Math.Sin(2 * DoubleUtil.ToRadians(omega));
        return term1 - term2 - term3 + term4;
    }

    /* page 144 */
    public static double NutationInObliquity(double t, double l0, double lp, double omega)
    {
        double term1 = 9.2 / 3600 * Math.Cos(DoubleUtil.ToRadians(omega));
        double term2 = 0.57 / 3600 * Math.Cos(2 * DoubleUtil.ToRadians(l0));
        double term3 = 0.10 / 3600 * Math.Cos(2 * DoubleUtil.ToRadians(lp));
        double term4 = 0.09 / 3600 * Math.Cos(2 * DoubleUtil.ToRadians(omega));
        return term1 + term2 + term3 - term4;
    }

    /* page 93 */
    public static double AltitudeOfCelestialBody(double phi, double delta, double h)
    {
        double term1 = Math.Sin(DoubleUtil.ToRadians(phi)) * Math.Sin(DoubleUtil.ToRadians(delta));
        double term2 = Math.Cos(DoubleUtil.ToRadians(phi)) * Math.Cos(DoubleUtil.ToRadians(delta)) * Math.Cos(DoubleUtil.ToRadians(h));
        return DoubleUtil.ToDegrees(Math.Asin(term1 + term2));
    }

    /* page 102 */
    public static double ApproximateTransit(double l, double theta0, double alpha2)
    {
        double lw = l * -1;
        return DoubleUtil.NormalizeWithBound((alpha2 + lw - theta0) / 360, 1);
    }

    /* page 102 */
    public static double CorrectedTransit(double m0, double l, double theta0, double alpha2, double alpha1, double alpha3)
    {
        double lw = l * -1;
        double theta = DoubleUtil.UnwindAngle(theta0 + 360.985647 * m0);
        double alpha = DoubleUtil.UnwindAngle(InterpolateAngles(alpha2, alpha1, alpha3, m0));
        double h = DoubleUtil.ClosestAngle(theta - lw - alpha);
        double dm = h / -360;
        return (m0 + dm) * 24;
    }

    /* page 102 */
    public static double CorrectedHourAngle(double m0, double h0, Coordinates coordinates, bool afterTransit,
        double theta0, double alpha2, double alpha1, double alpha3, double delta2, double delta1, double delta3)
    {
        double lw = coordinates.Longitude * -1;
        double term1 = Math.Sin(DoubleUtil.ToRadians(h0)) - Math.Sin(DoubleUtil.ToRadians(coordinates.Latitude)) * Math.Sin(DoubleUtil.ToRadians(delta2));
        double term2 = Math.Cos(DoubleUtil.ToRadians(coordinates.Latitude)) * Math.Cos(DoubleUtil.ToRadians(delta2));
        double bigH0 = DoubleUtil.ToDegrees(Math.Acos(term1 / term2));
        double m = afterTransit ? m0 + bigH0 / 360 : m0 - bigH0 / 360;
        double theta = DoubleUtil.UnwindAngle(theta0 + 360.985647 * m);
        double alpha = DoubleUtil.UnwindAngle(InterpolateAngles(alpha2, alpha1, alpha3, m));
        double delta = Interpolate(delta2, delta1, delta3, m);
        double hh = theta - lw - alpha;
        double h = AltitudeOfCelestialBody(coordinates.Latitude, delta, hh);
        double term3 = h - h0;
        double term4 = 360 * Math.Cos(DoubleUtil.ToRadians(delta)) * Math.Cos(DoubleUtil.ToRadians(coordinates.Latitude)) * Math.Sin(DoubleUtil.ToRadians(hh));
        double dm = term3 / term4;
        return (m + dm) * 24;
    }

    /* page 24 */
    public static double Interpolate(double y2, double y1, double y3, double n)
    {
        double a = y2 - y1;
        double b = y3 - y2;
        double c = b - a;
        return y2 + n / 2 * (a + b + n * c);
    }

    /* page 24 */
    public static double InterpolateAngles(double y2, double y1, double y3, double n)
    {
        double a = DoubleUtil.UnwindAngle(y2 - y1);
        double b = DoubleUtil.UnwindAngle(y3 - y2);
        double c = b - a;
        return y2 + n / 2 * (a + b + n * c);
    }
}

internal sealed class SolarCoordinates
{
    public readonly double Declination;
    public readonly double RightAscension;
    public readonly double ApparentSiderealTime;

    public SolarCoordinates(double julianDay)
    {
        double t = CalendricalHelper.JulianCentury(julianDay);
        double l0 = Astronomical.MeanSolarLongitude(t);
        double lp = Astronomical.MeanLunarLongitude(t);
        double omega = Astronomical.AscendingLunarNodeLongitude(t);
        double lambda = DoubleUtil.ToRadians(Astronomical.ApparentSolarLongitude(t, l0));
        double theta0 = Astronomical.MeanSiderealTime(t);
        double dPsi = Astronomical.NutationInLongitude(t, l0, lp, omega);
        double dEpsilon = Astronomical.NutationInObliquity(t, l0, lp, omega);
        double epsilon0 = Astronomical.MeanObliquityOfTheEcliptic(t);
        double epsilonApp = DoubleUtil.ToRadians(Astronomical.ApparentObliquityOfTheEcliptic(t, epsilon0));

        /* page 165 */
        Declination = DoubleUtil.ToDegrees(Math.Asin(Math.Sin(epsilonApp) * Math.Sin(lambda)));
        /* page 165 */
        RightAscension = DoubleUtil.UnwindAngle(DoubleUtil.ToDegrees(Math.Atan2(Math.Cos(epsilonApp) * Math.Sin(lambda), Math.Cos(lambda))));
        /* page 88 */
        ApparentSiderealTime = theta0 + dPsi * 3600 * Math.Cos(DoubleUtil.ToRadians(epsilon0 + dEpsilon)) / 3600;
    }
}

internal sealed class SolarTime
{
    public readonly double Transit;
    public readonly double Sunrise;
    public readonly double Sunset;

    private readonly Coordinates _observer;
    private readonly SolarCoordinates _solar;
    private readonly SolarCoordinates _prevSolar;
    private readonly SolarCoordinates _nextSolar;
    private readonly double _approximateTransit;

    public SolarTime(DateOnly today, Coordinates coordinates)
    {
        double julianDate = CalendricalHelper.JulianDay(today.Year, today.Month, today.Day);
        _prevSolar = new SolarCoordinates(julianDate - 1);
        _solar = new SolarCoordinates(julianDate);
        _nextSolar = new SolarCoordinates(julianDate + 1);
        _approximateTransit = Astronomical.ApproximateTransit(coordinates.Longitude, _solar.ApparentSiderealTime, _solar.RightAscension);
        const double solarAltitude = -50.0 / 60.0;
        _observer = coordinates;
        Transit = Astronomical.CorrectedTransit(_approximateTransit, coordinates.Longitude, _solar.ApparentSiderealTime,
            _solar.RightAscension, _prevSolar.RightAscension, _nextSolar.RightAscension);
        Sunrise = Astronomical.CorrectedHourAngle(_approximateTransit, solarAltitude, coordinates, false,
            _solar.ApparentSiderealTime, _solar.RightAscension, _prevSolar.RightAscension, _nextSolar.RightAscension,
            _solar.Declination, _prevSolar.Declination, _nextSolar.Declination);
        Sunset = Astronomical.CorrectedHourAngle(_approximateTransit, solarAltitude, coordinates, true,
            _solar.ApparentSiderealTime, _solar.RightAscension, _prevSolar.RightAscension, _nextSolar.RightAscension,
            _solar.Declination, _prevSolar.Declination, _nextSolar.Declination);
    }

    public double HourAngle(double angle, bool afterTransit) =>
        Astronomical.CorrectedHourAngle(_approximateTransit, angle, _observer, afterTransit,
            _solar.ApparentSiderealTime, _solar.RightAscension, _prevSolar.RightAscension, _nextSolar.RightAscension,
            _solar.Declination, _prevSolar.Declination, _nextSolar.Declination);

    public double Afternoon(double shadowLength)
    {
        double tangent = Math.Abs(_observer.Latitude - _solar.Declination);
        double inverse = shadowLength + Math.Tan(DoubleUtil.ToRadians(tangent));
        double angle = DoubleUtil.ToDegrees(Math.Atan(1.0 / inverse));
        return HourAngle(angle, true);
    }
}
