using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Siraj.Core.Places;

public sealed record City(
    string Name, string NameAr, string NameUr,
    string Country, string CountryAr, string CountryUr,
    double Latitude, double Longitude, string TimeZone, string CountryCode)
{
    public string DisplayName(string language) => language switch
    {
        "ar" => NameAr,
        "ur" => NameUr,
        _ => Name
    };

    public string DisplayCountry(string language) => language switch
    {
        "ar" => CountryAr,
        "ur" => CountryUr,
        _ => Country
    };
}

public sealed record Place(
    double Latitude,
    double Longitude,
    string? CityName = null,
    string? CityNameAr = null,
    string TimeZone = "UTC",
    string? CountryCode = null)
{
    public string? DisplayName(string language)
    {
        bool arabicScript = language is "ar" or "ur";
        if (arabicScript && !string.IsNullOrWhiteSpace(CityNameAr)) return CityNameAr;
        return string.IsNullOrWhiteSpace(CityName) ? null : CityName;
    }

    [JsonIgnore]
    public TimeZoneInfo Zone => Zones.Find(TimeZone);

    public DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);

    [JsonIgnore]
    public string? Country => CountryCode ?? Zones.CountryOf(TimeZone);

    public static Place FromCity(City c) =>
        new(c.Latitude, c.Longitude, c.Name, c.NameAr, c.TimeZone, c.CountryCode);
}

public static class Zones
{
    private static readonly ConcurrentDictionary<string, TimeZoneInfo> Cache = new();

    public static TimeZoneInfo Find(string? ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId)) return TimeZoneInfo.Local;
        return Cache.GetOrAdd(ianaId, id =>
        {
            if (id is "UTC" or "GMT" or "Etc/UTC") return TimeZoneInfo.Utc;
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (Exception) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var win))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(win); } catch { return TimeZoneInfo.Local; }
            }
            catch { return TimeZoneInfo.Local; }
        });
    }

    public static string LocalIanaId()
    {
        var local = TimeZoneInfo.Local;
        if (local.HasIanaId) return local.Id;
        string? region = null;
        try { region = RegionInfo.CurrentRegion.TwoLetterISORegionName; } catch { }
        if (region != null && TimeZoneInfo.TryConvertWindowsIdToIanaId(local.Id, region, out var regional)) return regional!;
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(local.Id, out var iana) ? iana! : "UTC";
    }

    public static string? CountryOf(string? ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId) || ianaId is "UTC" or "GMT") return null;
        return ZoneCountries.ByZone.TryGetValue(ianaId, out var code) ? code : null;
    }

    public static DateTimeOffset AtLocal(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) local = local.AddHours(1);
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    public static DateTimeOffset On(DateTimeOffset instant, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(instant, zone);
}

public static class LocationZones
{
    private const double MaxHoursFromSun = 3.5;

    public static string Plausible(string? zone, double latitude, double longitude, DateTimeOffset now)
    {
        string id = string.IsNullOrWhiteSpace(zone) || zone is "UTC" or "GMT" ? Zones.LocalIanaId() : zone;
        var kept = Zones.Find(id);
        if (Fits(kept, longitude, now)) return id;
        return Cities.Nearest(latitude, longitude)?.TimeZone ?? id;
    }

    private static bool Fits(TimeZoneInfo zone, double longitude, DateTimeOffset now)
    {
        double offsetHours = zone.GetUtcOffset(now).TotalHours;
        // Round the date line: Samoa's +13 at 172 W is an hour from the sun, not twenty-three.
        double apart = (offsetHours - longitude / 15.0) % 24.0;
        if (apart > 12.0) apart -= 24.0;
        if (apart < -12.0) apart += 24.0;
        return Math.Abs(apart) <= MaxHoursFromSun;
    }
}

public static class Cities
{
    public static IReadOnlyList<City> All => CityData.All;

    public static IReadOnlyList<City> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return CityData.All;
        var q = query.Trim();
        var compare = CultureInfo.InvariantCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        bool Has(string s) => compare.IndexOf(s, q, options) >= 0;
        return CityData.All
            .Where(c => Has(c.Name) || Has(c.NameAr) || Has(c.NameUr) || Has(c.Country) || Has(c.CountryAr) || Has(c.CountryUr))
            .OrderBy(c => compare.IsPrefix(c.Name, q, options) || compare.IsPrefix(c.NameAr, q, options) ? 0 : 1)
            .ToList();
    }

    public static City? Nearest(double latitude, double longitude) =>
        CityData.All.MinBy(c => DistanceKm(latitude, longitude, c.Latitude, c.Longitude));

    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 6371.0 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0.0, 1.0)));
    }
}
