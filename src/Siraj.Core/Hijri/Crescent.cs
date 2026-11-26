using Siraj.Core.Places;

namespace Siraj.Core.Hijri;

internal sealed record CrescentEvening(
    long SunsetMillis,
    long? MoonsetMillis,
    double LagMinutes,
    double CentreLagMinutes,
    bool ConjunctionBeforeSunset,
    double AgeHours,
    double MoonAltitude,
    double Elongation,
    double? OdehV);

internal static class Crescent
{
    private const double Deg = Math.PI / 180.0;

    public static CrescentEvening? Evening(DateOnly date, TimeZoneInfo zone, double latitude, double longitude)
    {
        double noon = Ephemeris.JulianDay(Zones.AtLocal(date, new TimeOnly(12, 0), zone));
        var sunset = SunsetOn(noon, latitude, longitude);
        return sunset == null ? null : AtSunset(sunset.Value, latitude, longitude);
    }

    public static CrescentEvening AtSunset(double sunset, double latitude, double longitude)
    {
        double moonset = Setting(sunset, latitude, longitude, upperLimb: true);
        double centreSet = Setting(sunset, latitude, longitude, upperLimb: false);
        double lagMinutes = (moonset - sunset) * 1440.0;

        double conjunction = Ephemeris.PreviousConjunction(sunset);
        var sun = Ephemeris.Sun(sunset);
        var moon = Ephemeris.Moon(sunset);
        var topo = Ephemeris.TopocentricMoon(sunset, moon, latitude, longitude);
        double moonAltitude = Ephemeris.Altitude(sunset, topo.RightAscension, topo.Declination, latitude, longitude);

        return new CrescentEvening(
            SunsetMillis: Ephemeris.EpochMillis(sunset),
            MoonsetMillis: Ephemeris.EpochMillis(moonset),
            LagMinutes: lagMinutes,
            CentreLagMinutes: (centreSet - sunset) * 1440.0,
            ConjunctionBeforeSunset: conjunction < sunset && sunset - conjunction < 20.0,
            AgeHours: (sunset - conjunction) * 24.0,
            MoonAltitude: moonAltitude,
            Elongation: Ephemeris.Separation(sun, moon),
            OdehV: lagMinutes > 0 ? Odeh(sunset + lagMinutes * 4.0 / 9.0 / 1440.0, latitude, longitude) : null);
    }

    public static double? SunsetOn(double noonJd, double latitude, double longitude) =>
        Ephemeris.SunAtAltitude(noonJd, latitude, longitude, Ephemeris.SunsetAltitude);

    private static double Setting(double sunset, double latitude, double longitude, bool upperLimb)
    {
        double F(double jd) => MoonSetFunction(jd, latitude, longitude, upperLimb);
        return F(sunset) > 0
            ? Ephemeris.DescendingCrossing(sunset, sunset + 0.25, F) ?? sunset + 0.25
            : Ephemeris.DescendingCrossing(sunset - 0.25, sunset, F) ?? sunset - 0.25;
    }

    private static double MoonSetFunction(double jd, double latitude, double longitude, bool upperLimb)
    {
        var moon = Ephemeris.Moon(jd);
        var topo = Ephemeris.TopocentricMoon(jd, moon, latitude, longitude);
        double altitude = Ephemeris.Altitude(jd, topo.RightAscension, topo.Declination, latitude, longitude);
        double semiDiameter = upperLimb ? Math.Asin(Ephemeris.MoonRadiusKm / topo.Distance) / Deg : 0.0;
        return altitude + semiDiameter + Ephemeris.RefractionAtHorizon;
    }

    private static double Odeh(double jd, double latitude, double longitude)
    {
        var sun = Ephemeris.Sun(jd);
        var moon = Ephemeris.Moon(jd);
        var topo = Ephemeris.TopocentricMoon(jd, moon, latitude, longitude);
        double sunAltitude = Ephemeris.Altitude(jd, sun.RightAscension, sun.Declination, latitude, longitude);
        double moonAltitude = Ephemeris.Altitude(jd, topo.RightAscension, topo.Declination, latitude, longitude);
        double arcv = moonAltitude - sunAltitude;
        double arcl = Ephemeris.Separation(sun, topo);
        double semiDiameterArcmin = Math.Asin(Ephemeris.MoonRadiusKm / topo.Distance) / Deg * 60.0;
        double width = semiDiameterArcmin * (1 - Math.Cos(arcl * Deg));
        return arcv - (-0.1018 * width * width * width + 0.7319 * width * width - 6.3226 * width + 7.1651);
    }
}

public sealed record MoonPhase(double Illumination, bool Waxing, double AgeDays)
{
    public enum PhaseName { New, WaxingCrescent, FirstQuarter, WaxingGibbous, Full, WaningGibbous, LastQuarter, WaningCrescent }

    private const double Deg = Math.PI / 180.0;
    private const double KmPerAu = 149_597_870.7;

    public PhaseName Name => Illumination switch
    {
        < 0.03 => PhaseName.New,
        > 0.97 => PhaseName.Full,
        >= 0.45 and <= 0.55 => Waxing ? PhaseName.FirstQuarter : PhaseName.LastQuarter,
        < 0.45 => Waxing ? PhaseName.WaxingCrescent : PhaseName.WaningCrescent,
        _ => Waxing ? PhaseName.WaxingGibbous : PhaseName.WaningGibbous
    };

    public float PhaseFraction
    {
        get
        {
            double half = Math.Acos(Math.Clamp(1.0 - 2.0 * Illumination, -1.0, 1.0)) / (2.0 * Math.PI);
            return (float)(Waxing ? half : 1.0 - half);
        }
    }

    public static MoonPhase At(DateTimeOffset instant)
    {
        double jd = Ephemeris.JulianDay(instant);
        var sun = Ephemeris.Sun(jd);
        var moon = Ephemeris.Moon(jd);
        double psi = Ephemeris.Separation(moon, sun) * Deg;
        double sunKm = sun.Distance * KmPerAu;
        double phaseAngle = Math.Atan2(sunKm * Math.Sin(psi), moon.Distance - sunKm * Math.Cos(psi));
        double illumination = (1 + Math.Cos(phaseAngle)) / 2;
        double ahead = moon.Longitude - sun.Longitude;
        bool waxing = ahead - 360.0 * Math.Floor(ahead / 360.0) < 180.0;
        double age = jd - Ephemeris.PreviousConjunction(jd);
        return new MoonPhase(Math.Clamp(illumination, 0.0, 1.0), waxing, age);
    }
}
