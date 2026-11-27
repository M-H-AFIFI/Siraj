namespace Siraj.Core.Hijri;

// Sun position follows Meeus ch. 25 (~0.01 deg); moon follows ELP-2000/82 ch. 47 (~10 arcsec).
// Times are Julian days in UT (Delta-T ignored).

internal readonly record struct Position(double RightAscension, double Declination, double Longitude, double Distance);

internal static class Ephemeris
{
    private const double Deg = Math.PI / 180.0;
    public const double J2000 = 2451545.0;
    public const double EarthRadiusKm = 6378.14;
    public const double MoonRadiusKm = 1737.4;

    public const double SunsetAltitude = -0.8333;

    public const double RefractionAtHorizon = 0.5667;

    private static double SinD(double x) => Math.Sin(x * Deg);
    private static double CosD(double x) => Math.Cos(x * Deg);
    internal static double Norm360(double x) => x - 360.0 * Math.Floor(x / 360.0);
    private static double Norm180(double x) => Norm360(x + 180.0) - 180.0;

    public static double JulianDay(long epochMillis) => epochMillis / 86_400_000.0 + 2440587.5;

    public static double JulianDay(DateTimeOffset instant) => JulianDay(instant.ToUnixTimeMilliseconds());

    public static long EpochMillis(double julianDay) => (long)((julianDay - 2440587.5) * 86_400_000.0);

    private static double Centuries(double jd) => (jd - J2000) / 36525.0;

    private static double Obliquity(double t)
    {
        double omega = 125.04452 - 1934.136261 * t;
        double eps0 = 23.0 + 26.0 / 60.0 + 21.448 / 3600.0 - (46.8150 * t + 0.00059 * t * t - 0.001813 * t * t * t) / 3600.0;
        return eps0 + 0.00256 * CosD(omega);
    }

    private static (double Ra, double Dec) ToEquatorial(double longitude, double latitude, double epsilon)
    {
        double ra = Math.Atan2(SinD(longitude) * CosD(epsilon) - Math.Tan(latitude * Deg) * SinD(epsilon), CosD(longitude)) / Deg;
        double dec = Math.Asin(SinD(latitude) * CosD(epsilon) + CosD(latitude) * SinD(epsilon) * SinD(longitude)) / Deg;
        return (Norm360(ra), dec);
    }

    public static Position Sun(double jd)
    {
        double t = Centuries(jd);
        double l0 = Norm360(280.46646 + 36000.76983 * t + 0.0003032 * t * t);
        double m = Norm360(357.52911 + 35999.05029 * t - 0.0001537 * t * t);
        double e = 0.016708634 - 0.000042037 * t - 0.0000001267 * t * t;
        double c = (1.914602 - 0.004817 * t - 0.000014 * t * t) * SinD(m) +
                   (0.019993 - 0.000101 * t) * SinD(2 * m) + 0.000289 * SinD(3 * m);
        double trueLongitude = l0 + c;
        double anomaly = m + c;
        double r = 1.000001018 * (1 - e * e) / (1 + e * CosD(anomaly));
        double omega = 125.04 - 1934.136 * t;
        double apparent = Norm360(trueLongitude - 0.00569 - 0.00478 * SinD(omega));
        var (ra, dec) = ToEquatorial(apparent, 0.0, Obliquity(t));
        return new Position(ra, dec, apparent, r);
    }

    public static Position Moon(double jd)
    {
        double t = Centuries(jd);
        double t2 = t * t, t3 = t2 * t, t4 = t3 * t;
        double lp = Norm360(218.3164477 + 481267.88123421 * t - 0.0015786 * t2 + t3 / 538841.0 - t4 / 65194000.0);
        double d = Norm360(297.8501921 + 445267.1114034 * t - 0.0018819 * t2 + t3 / 545868.0 - t4 / 113065000.0);
        double m = Norm360(357.5291092 + 35999.0502909 * t - 0.0001536 * t2 + t3 / 24490000.0);
        double mp = Norm360(134.9633964 + 477198.8675055 * t + 0.0087414 * t2 + t3 / 69699.0 - t4 / 14712000.0);
        double f = Norm360(93.2720950 + 483202.0175233 * t - 0.0036539 * t2 - t3 / 3526000.0 + t4 / 863310000.0);
        double a1 = Norm360(119.75 + 131.849 * t);
        double a2 = Norm360(53.09 + 479264.290 * t);
        double a3 = Norm360(313.45 + 481266.484 * t);
        double e = 1 - 0.002516 * t - 0.0000074 * t2;

        double sumL = 0.0, sumR = 0.0;
        foreach (var row in LrTerms)
        {
            double arg = row[0] * d + row[1] * m + row[2] * mp + row[3] * f;
            double factor = Math.Abs((int)row[1]) switch { 1 => e, 2 => e * e, _ => 1.0 };
            sumL += row[4] * factor * SinD(arg);
            sumR += row[5] * factor * CosD(arg);
        }
        double sumB = 0.0;
        foreach (var row in BTerms)
        {
            double arg = row[0] * d + row[1] * m + row[2] * mp + row[3] * f;
            double factor = Math.Abs((int)row[1]) switch { 1 => e, 2 => e * e, _ => 1.0 };
            sumB += row[4] * factor * SinD(arg);
        }
        sumL += 3958 * SinD(a1) + 1962 * SinD(lp - f) + 318 * SinD(a2);
        sumB += -2235 * SinD(lp) + 382 * SinD(a3) + 175 * SinD(a1 - f) + 175 * SinD(a1 + f) +
                127 * SinD(lp - mp) - 115 * SinD(lp + mp);

        // Top 4 nutation-in-longitude terms.
        double omega = 125.04452 - 1934.136261 * t;
        double l0 = 280.4665 + 36000.7698 * t;
        double dPsi = (-17.20 * SinD(omega) - 1.32 * SinD(2 * l0) - 0.23 * SinD(2 * lp) + 0.21 * SinD(2 * omega)) / 3600.0;

        double longitude = Norm360(lp + sumL / 1_000_000.0 + dPsi);
        double latitude = sumB / 1_000_000.0;
        double distance = 385000.56 + sumR / 1000.0;
        var (ra, dec) = ToEquatorial(longitude, latitude, Obliquity(t));
        return new Position(ra, dec, longitude, distance);
    }

    public static double SiderealTime(double jd)
    {
        double t = Centuries(jd);
        return Norm360(280.46061837 + 360.98564736629 * (jd - J2000) + 0.000387933 * t * t - t * t * t / 38710000.0);
    }

    public static double Altitude(double jd, double ra, double dec, double latitude, double longitude)
    {
        double hourAngle = SiderealTime(jd) + longitude - ra;
        return Math.Asin(SinD(latitude) * SinD(dec) + CosD(latitude) * CosD(dec) * CosD(hourAngle)) / Deg;
    }

    public static (double Altitude, double Azimuth) Horizontal(double jd, double ra, double dec, double latitude, double longitude)
    {
        double h = (SiderealTime(jd) + longitude - ra) * Deg;
        double phi = latitude * Deg, delta = dec * Deg;
        double altitude = Math.Asin(Math.Sin(phi) * Math.Sin(delta) + Math.Cos(phi) * Math.Cos(delta) * Math.Cos(h));
        double azimuth = Math.Atan2(-Math.Sin(h), Math.Tan(delta) * Math.Cos(phi) - Math.Sin(phi) * Math.Cos(h));
        return (altitude / Deg, Norm360(azimuth / Deg));
    }

    public static double MoonParallax(double distanceKm) => Math.Asin(EarthRadiusKm / distanceKm) / Deg;

    public static Position TopocentricMoon(double jd, Position moon, double latitude, double longitude, double elevationM = 0.0)
    {
        double parallax = MoonParallax(moon.Distance);
        double rho = 1.0 + elevationM / (EarthRadiusKm * 1000.0);
        double rhoCos = rho * CosD(latitude);
        double rhoSin = rho * SinD(latitude);
        double hourAngle = SiderealTime(jd) + longitude - moon.RightAscension;
        double sinP = SinD(parallax);
        double dRa = Math.Atan2(-rhoCos * sinP * SinD(hourAngle), CosD(moon.Declination) - rhoCos * sinP * CosD(hourAngle));
        double dec = Math.Atan2(
            (SinD(moon.Declination) - rhoSin * sinP) * Math.Cos(dRa),
            CosD(moon.Declination) - rhoCos * sinP * CosD(hourAngle));
        double x = moon.Distance * CosD(moon.Declination) * CosD(hourAngle) - EarthRadiusKm * rhoCos;
        double y = moon.Distance * CosD(moon.Declination) * SinD(hourAngle);
        double z = moon.Distance * SinD(moon.Declination) - EarthRadiusKm * rhoSin;
        double distance = Math.Sqrt(x * x + y * y + z * z);
        return new Position(Norm360(moon.RightAscension + dRa / Deg), dec / Deg, moon.Longitude, distance);
    }

    public static double Separation(Position a, Position b)
    {
        double cosSep = SinD(a.Declination) * SinD(b.Declination) +
                        CosD(a.Declination) * CosD(b.Declination) * CosD(a.RightAscension - b.RightAscension);
        return Math.Acos(Math.Clamp(cosSep, -1.0, 1.0)) / Deg;
    }

    public static double ElongationInLongitude(double jd) => Norm180(Moon(jd).Longitude - Sun(jd).Longitude);

    public static double PreviousConjunction(double jd)
    {
        double delta = ElongationInLongitude(jd);
        // Mean lunar-solar synodic rate (~12.190749 deg/day).
        double guess = jd - Norm360(delta) / 12.190749;
        for (int i = 0; i < 8; i++)
        {
            delta = ElongationInLongitude(guess);
            guess -= delta / 12.190749;
        }
        return guess;
    }

    public static double? SunAtAltitude(double noonJd, double latitude, double longitude, double altitude, bool evening = true)
    {
        double jd = noonJd;
        for (int i = 0; i < 4; i++)
        {
            var sun = Sun(jd);
            double cosH0 = (SinD(altitude) - SinD(latitude) * SinD(sun.Declination)) / (CosD(latitude) * CosD(sun.Declination));
            if (cosH0 < -1.0 || cosH0 > 1.0) return null;
            double h0 = Math.Acos(cosH0) / Deg;
            double hourAngle = Norm180(SiderealTime(jd) + longitude - sun.RightAscension);
            double target = evening ? h0 : -h0;
            jd += Norm180(target - hourAngle) / 360.98564736629;
        }
        return jd;
    }

    public static double? DescendingCrossing(double from, double to, Func<double, double> f, double step = 10.0 / 1440.0)
    {
        double a = from;
        double fa = f(a);
        while (a < to)
        {
            double b = Math.Min(a + step, to);
            double fb = f(b);
            if (fa > 0 && fb <= 0)
            {
                double lo = a, hi = b;
                for (int i = 0; i < 12; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (f(mid) > 0)
                        lo = mid;
                    else
                        hi = mid;
                }
                return (lo + hi) / 2;
            }
            a = b;
            fa = fb;
        }
        return null;
    }

    // Meeus table 47.A: D, M, M', F, sum-l coefficient (1e-6 degree), sum-r coefficient (1e-3 km).
    private static readonly double[][] LrTerms =
    [
        [0, 0, 1, 0, 6288774, -20905355], [2, 0, -1, 0, 1274027, -3699111], [2, 0, 0, 0, 658314, -2955968],
        [0, 0, 2, 0, 213618, -569925], [0, 1, 0, 0, -185116, 48888], [0, 0, 0, 2, -114332, -3149],
        [2, 0, -2, 0, 58793, 246158], [2, -1, -1, 0, 57066, -152138], [2, 0, 1, 0, 53322, -170733],
        [2, -1, 0, 0, 45758, -204586], [0, 1, -1, 0, -40923, -129620], [1, 0, 0, 0, -34720, 108743],
        [0, 1, 1, 0, -30383, 104755], [2, 0, 0, -2, 15327, 10321], [0, 0, 1, 2, -12528, 0],
        [0, 0, 1, -2, 10980, 79661], [4, 0, -1, 0, 10675, -34782], [0, 0, 3, 0, 10034, -23210],
        [4, 0, -2, 0, 8548, -21636], [2, 1, -1, 0, -7888, 24208], [2, 1, 0, 0, -6766, 30824],
        [1, 0, -1, 0, -5163, -8379], [1, 1, 0, 0, 4987, -16675], [2, -1, 1, 0, 4036, -12831],
        [2, 0, 2, 0, 3994, -10445], [4, 0, 0, 0, 3861, -11650], [2, 0, -3, 0, 3665, 14403],
        [0, 1, -2, 0, -2689, -7003], [2, 0, -1, 2, -2602, 0], [2, -1, -2, 0, 2390, 10056],
        [1, 0, 1, 0, -2348, 6322], [2, -2, 0, 0, 2236, -9884], [0, 1, 2, 0, -2120, 5751],
        [0, 2, 0, 0, -2069, 0], [2, -2, -1, 0, 2048, -4950], [2, 0, 1, -2, -1773, 4130],
        [2, 0, 0, 2, -1595, 0], [4, -1, -1, 0, 1215, -3958], [0, 0, 2, 2, -1110, 0],
        [3, 0, -1, 0, -892, 3258], [2, 1, 1, 0, -810, 2616], [4, -1, -2, 0, 759, -1897],
        [0, 2, -1, 0, -713, -2117], [2, 2, -1, 0, -700, 2354], [2, 1, -2, 0, 691, 0],
        [2, -1, 0, -2, 596, 0], [4, 0, 1, 0, 549, -1423], [0, 0, 4, 0, 537, -1117],
        [4, -1, 0, 0, 520, -1571], [1, 0, -2, 0, -487, -1739], [2, 1, 0, -2, -399, 0],
        [0, 0, 2, -2, -381, -4421], [1, 1, 1, 0, 351, 0], [3, 0, -2, 0, -340, 0],
        [4, 0, -3, 0, 330, 0], [2, -1, 2, 0, 327, 0], [0, 2, 1, 0, -323, 1165],
        [1, 1, -1, 0, 299, 0], [2, 0, 3, 0, 294, 0], [2, 0, -1, -2, 0, 8752],
    ];

    // Meeus table 47.B: D, M, M', F, sum-b coefficient (1e-6 degree).
    private static readonly double[][] BTerms =
    [
        [0, 0, 0, 1, 5128122], [0, 0, 1, 1, 280602], [0, 0, 1, -1, 277693], [2, 0, 0, -1, 173237],
        [2, 0, -1, 1, 55413], [2, 0, -1, -1, 46271], [2, 0, 0, 1, 32573], [0, 0, 2, 1, 17198],
        [2, 0, 1, -1, 9266], [0, 0, 2, -1, 8822], [2, -1, 0, -1, 8216], [2, 0, -2, -1, 4324],
        [2, 0, 1, 1, 4200], [2, 1, 0, -1, -3359], [2, -1, -1, 1, 2463], [2, -1, 0, 1, 2211],
        [2, -1, -1, -1, 2065], [0, 1, -1, -1, -1870], [4, 0, -1, -1, 1828], [0, 1, 0, 1, -1794],
        [0, 0, 0, 3, -1749], [0, 1, -1, 1, -1565], [1, 0, 0, 1, -1491], [0, 1, 1, 1, -1475],
        [0, 1, 1, -1, -1410], [0, 1, 0, -1, -1344], [1, 0, 0, -1, -1335], [0, 0, 3, 1, 1107],
        [4, 0, 0, -1, 1021], [4, 0, -1, 1, 833], [0, 0, 1, -3, 777], [4, 0, -2, 1, 671],
        [2, 0, 0, -3, 607], [2, 0, 2, -1, 596], [2, -1, 1, -1, 491], [2, 0, -2, 1, -451],
        [0, 0, 3, -1, 439], [2, 0, 2, 1, 422], [2, 0, -3, -1, 421], [2, 1, -1, 1, -366],
        [2, 1, 0, 1, -351], [4, 0, 0, 1, 331], [2, -1, 1, 1, 315], [2, -2, 0, -1, 302],
        [0, 0, 1, 3, -283], [2, 1, 1, -1, -229], [1, 1, 0, -1, 223], [1, 1, 0, 1, 223],
        [0, 1, -2, -1, -220], [2, 1, -1, -1, -220], [1, 0, 1, 1, -185], [2, -1, -2, -1, 181],
        [0, 1, 2, 1, -177], [4, 0, -2, -1, 176], [4, -1, -1, -1, 166], [1, 0, 1, -1, -164],
        [4, 0, 1, -1, 132], [1, 0, -1, -1, -119], [4, -1, 0, -1, 115], [2, -2, 0, 1, 107],
    ];
}
