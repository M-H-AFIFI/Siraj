using Siraj.Core.Hijri;
using Siraj.Core.Places;
using Siraj.Core.Prayer;
using Siraj.Core.Settings;
using Siraj.Core.Sky;
using Siraj.Core.Theme;
using Xunit;

namespace Siraj.Core.Tests;

public class SkyTest
{
    private static readonly Place Cairo = Place.FromCity(Cities.Search("Cairo")[0]);
    private static readonly AppSettings Settings = new() { Location = Cairo, CalculationMethod = "Egyptian" };

    [Fact]
    public void SkyTimelineAndSolarArcCoverFullDay()
    {
        var date = new DateOnly(2026, 6, 21);
        var times = PrayerCalculator.Raw(Cairo, date, Settings.Calculation);
        for (var t = Zones.AtLocal(date, TimeOnly.MinValue, Cairo.Zone); t < Zones.AtLocal(date.AddDays(1), TimeOnly.MinValue, Cairo.Zone); t = t.AddMinutes(7))
        {
            var day = PrayerCalculator.Day(Cairo, date, Settings.Calculation, t);
            var sky = SkyTimeline.At(t, day.Times);
            Assert.Equal(6, sky.Colors.Sky.Count);
            var arc = SolarArc.At(t, day.Times, day.Next);
            Assert.InRange(arc.Progress, 0.08f, 0.92f);
            Assert.Equal(t >= times[1].Time && t < times[4].Time, arc.IsDaytime);
        }
    }

    [Fact]
    public void AdhanSkySpecCoversAllPrayers()
    {
        foreach (var prayer in Enum.GetValues<PrayerType>())
        {
            var spec = AdhanSkySpec.For(prayer);
            var stops = spec.BuildStops();
            Assert.Equal(7, stops.Length);
            Assert.Equal(0f, stops[0].Position);
            Assert.Equal(1f, stops[^1].Position);
            Assert.Equal(AdhanSkySpec.DeepBase, stops[^1].Color);
        }
        Assert.False(AdhanSkySpec.For(PrayerType.Fajr).IsDaytime);
        Assert.True(AdhanSkySpec.For(PrayerType.Fajr).IsCrescent);
        Assert.True(AdhanSkySpec.For(PrayerType.Dhuhr).IsDaytime);
        Assert.False(AdhanSkySpec.For(PrayerType.Dhuhr).IsCrescent);
        Assert.True(AdhanSkySpec.For(PrayerType.Asr).MoteStrength > 0.5f);
        Assert.True(AdhanSkySpec.For(PrayerType.Maghrib).DuskUndersideGlow > 0.5f);
    }

    [Fact]
    public void SkyPaletteTintsNightSky()
    {
        var night = SkyTimeline.NightColors;
        var emerald = Palettes.Scheme("Emerald", true, false).Primary;
        var tinted = SkyPalette.Tint(night, emerald);
        Assert.NotEqual(night.Sky[0], tinted.Sky[0]);
        Assert.Equal(night.OrbGlow, tinted.OrbGlow);
    }

    [Fact]
    public void MoonPhaseFractionMatchesPhase()
    {
        var fullMoon = new MoonPhase(1.0, true, 14.8);
        Assert.InRange(fullMoon.PhaseFraction, 0.48f, 0.52f);
        var waxingQuarter = new MoonPhase(0.5, true, 7.4);
        Assert.InRange(waxingQuarter.PhaseFraction, 0.24f, 0.26f);
        var waningQuarter = new MoonPhase(0.5, false, 22.1);
        Assert.InRange(waningQuarter.PhaseFraction, 0.74f, 0.76f);
    }
}
