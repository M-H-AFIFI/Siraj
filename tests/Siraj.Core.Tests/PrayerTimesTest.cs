using System.Globalization;
using Siraj.Core.Adhan;
using Siraj.Core.Places;
using Siraj.Core.Prayer;
using Xunit;

namespace Siraj.Core.Tests;

public class PrayerTimesTest
{
    [Fact]
    public void MatchesReferencePrayerTimes()
    {
        int rows = 0;
        var mismatches = new List<string>();
        foreach (var r in Reference.Rows("prayer_times.csv.gz"))
        {
            rows++;
            double lat = double.Parse(r[1], CultureInfo.InvariantCulture), lon = double.Parse(r[2], CultureInfo.InvariantCulture);
            var settings = new CalculationSettings(r[3], r[4], r[5]);
            var date = DateOnly.Parse(r[6], CultureInfo.InvariantCulture);
            var t = new PrayerTimes(new Coordinates(lat, lon), date, PrayerCalculator.Parameters(settings));
            var actual = new[] { t.Fajr, t.Sunrise, t.Dhuhr, t.Asr, t.Maghrib, t.Isha };
            for (int i = 0; i < 6; i++)
            {
                string expected = r[7 + i];
                string got = actual[i] is { } d ? (new DateTimeOffset(d, TimeSpan.Zero).ToUnixTimeMilliseconds() / 60_000).ToString() : "";
                if (expected != got) mismatches.Add($"{string.Join(',', r[..7])} #{i}: expected {expected} got {got}");
            }
        }
        Assert.True(rows > 20_000, $"only {rows} reference rows");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches, first: {string.Join("\n", mismatches.Take(10))}");
    }

    [Fact]
    public void CairoPrayerTimesUseLocalTimeZone()
    {
        var cairo = Place.FromCity(Cities.Search("Cairo")[0]);
        var now = new DateTimeOffset(2026, 10, 30, 6, 0, 0, TimeSpan.Zero);
        var day = PrayerCalculator.Day(cairo, new DateOnly(2026, 10, 30), new CalculationSettings("Egyptian"), now);
        var dhuhr = day[PrayerType.Dhuhr].Time;
        // Egypt ends DST at the end of October; Oct 30 is UTC+2.
        Assert.Equal(TimeSpan.FromHours(2), dhuhr.Offset);
        Assert.InRange(dhuhr.Hour, 11, 12);
        Assert.Equal(dhuhr.AddMinutes(20), day[PrayerType.Dhuhr].Iqama);
    }

    [Fact]
    public void SelectsTomorrowsFajrAfterIshaWindow()
    {
        var mecca = Place.FromCity(Cities.Search("Mecca")[0]);
        var date = new DateOnly(2026, 3, 1);
        var settings = new CalculationSettings("UmmAlQura");
        var isha = PrayerCalculator.Raw(mecca, date, settings)[5];
        var late = isha.WindowEnd.AddMinutes(1);
        var day = PrayerCalculator.Day(mecca, date, settings, late);
        var next = day.Next!;
        Assert.Equal(PrayerType.Fajr, next.Type);
        Assert.Equal(date.AddDays(1), DateOnly.FromDateTime(next.Time.DateTime));
    }

    [Fact]
    public void PrayerStateTransitionsThroughWindow()
    {
        var mecca = Place.FromCity(Cities.Search("Mecca")[0]);
        var date = new DateOnly(2026, 3, 1);
        var settings = new CalculationSettings("UmmAlQura");
        var asr = PrayerCalculator.Raw(mecca, date, settings)[3];

        PrayerState? At(DateTimeOffset t) => PrayerState.At(PrayerCalculator.Day(mecca, date, settings, t), t);

        Assert.IsType<PrayerState.UpcomingAzan>(At(asr.Time.AddMinutes(-10)));
        Assert.IsType<PrayerState.AzanCalled>(At(asr.Time.AddMinutes(1)));
        var waiting = Assert.IsType<PrayerState.WaitingForIqama>(At(asr.Time.AddMinutes(10)));
        Assert.Equal(0.5, waiting.Progress, 2);
        Assert.IsType<PrayerState.InPrayer>(At(asr.Iqama!.Value.AddMinutes(5)));
        var after = Assert.IsType<PrayerState.UpcomingAzan>(At(asr.Iqama!.Value.AddMinutes(16)));
        Assert.Equal(PrayerType.Maghrib, after.Prayer.Type);
    }

    [Fact]
    public void QiblaDirectionFromCairo()
    {
        Assert.InRange(Qibla.Direction(new Coordinates(30.0444, 31.2357)), 135.5, 136.5);
    }

    [Fact]
    public void RecommendsMethodAndMadhabByCountry()
    {
        Assert.Equal("Egyptian", CalculationMethods.RecommendedFor("eg"));
        Assert.Equal("Karachi", CalculationMethods.RecommendedFor("PK"));
        Assert.Equal("Hanafi", CalculationMethods.RecommendedMadhab("PK"));
        Assert.Equal("Shafi", CalculationMethods.RecommendedMadhab("TR"));
        Assert.Equal("MuslimWorldLeague", CalculationMethods.RecommendedFor(null));
        Assert.Equal("NorthAmerica", CalculationMethods.ById("ISNA")!.Id);
    }
}
