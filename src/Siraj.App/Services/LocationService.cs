using Siraj.Core.Places;
using Siraj.Core.Prayer;
using Windows.Devices.Geolocation;

namespace Siraj.App.Services;

internal static class LocationService
{
    public enum Outcome { Found, Denied, Unavailable }

    public static async Task<(Outcome Outcome, Place? Place)> LocateAsync()
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed) return (Outcome.Denied, null);
            var locator = new Geolocator { DesiredAccuracyInMeters = 2000 };
            var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(20));
            var point = position.Coordinate.Point.Position;
            return (Outcome.Found, FromCoordinates(point.Latitude, point.Longitude));
        }
        catch (UnauthorizedAccessException)
        {
            return (Outcome.Denied, null);
        }
        catch (Exception e)
        {
            Log.Error("location", e);
            return (Outcome.Unavailable, null);
        }
    }

    public static Place FromCoordinates(double latitude, double longitude)
    {
        var zone = LocationZones.Plausible(null, latitude, longitude, DateTimeOffset.Now);
        var city = Cities.Nearest(latitude, longitude);
        bool near = city != null && Cities.DistanceKm(latitude, longitude, city.Latitude, city.Longitude) < 40;
        return new Place(latitude, longitude,
            near ? city!.Name : null, near ? city!.NameAr : null, zone,
            Zones.CountryOf(zone) ?? (near ? city!.CountryCode : null));
    }

    public static void Use(Place place, bool manual, bool recommendMethod)
    {
        App.Settings.Update(s =>
        {
            var updated = s with { Location = place, LocationIsManual = manual };
            if (recommendMethod)
            {
                updated = updated with
                {
                    CalculationMethod = CalculationMethods.RecommendedFor(place.Country),
                    Madhab = CalculationMethods.RecommendedMadhab(place.Country),
                };
            }
            return updated;
        });
    }

    public static void OpenWindowsLocationSettings() =>
        _ = Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-location"));
}
