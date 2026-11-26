using Siraj.Core.Theme;
using Xunit;

namespace Siraj.Core.Tests;

public class LiveScenesTest
{
    [Fact]
    public void CatalogHasAllSeventeenScenes()
    {
        Assert.Equal(17, LiveScenes.All.Count);
        Assert.Equal(LiveScenes.All.Count, LiveScenes.All.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("FloatingLanterns", "DesertDunes")]
    [InlineData("Lanterns", "DesertDunes")]
    [InlineData("Bokeh", "Circles")]
    [InlineData("Constellation", "Particles")]
    [InlineData("Ripples", "Rings")]
    [InlineData("PurpleButterflies", "PurpleButterflies")]
    [InlineData("unknown_scene", "DesertDunes")]
    [InlineData(null, "DesertDunes")]
    public void LegacyAndUnknownIdsNormalizeCleanly(string? raw, string expected)
    {
        Assert.Equal(expected, LiveScenes.NormalizeId(raw));
    }

    [Fact]
    public void ColorsForMatchesLightDarkAndTrueBlackContracts()
    {
        var dark = Palettes.Scheme("Emerald", dark: true, black: false);
        var darkColors = LiveScenes.ColorsFor(dark, blackMode: false, timeSync: false);
        Assert.True(darkColors.IsDark);
        Assert.False(darkColors.IsBlack);
        Assert.Equal(dark.Primary.R, darkColors.Accent.R);
        Assert.Equal(dark.Tertiary.R, darkColors.Secondary.R);

        var amoled = Palettes.Scheme("Emerald", dark: true, black: true);
        var amoledColors = LiveScenes.ColorsFor(amoled, blackMode: true, timeSync: true, minutesOfDay: 720);
        Assert.True(amoledColors.IsBlack);
        Assert.Equal(0, amoledColors.SkyTop.R);
        Assert.Equal(0, amoledColors.SkyTop.G);
        Assert.Equal(0, amoledColors.SkyTop.B);

        var light = Palettes.Scheme("RoyalIndigo", dark: false, black: false);
        var lightColors = LiveScenes.ColorsFor(light, blackMode: false, timeSync: false);
        Assert.False(lightColors.IsDark);
        Assert.Equal(255, lightColors.Highlight.R);
    }

    [Fact]
    public void StarVisibilityTracksSolarPhases()
    {
        Assert.Equal(1f, LiveScenes.StarVisibility(60), 3);      // Deep night (01:00)
        Assert.Equal(0f, LiveScenes.StarVisibility(720), 3);     // Midday (12:00)
        Assert.InRange(LiveScenes.StarVisibility(300), 0.1f, 0.5f);  // Fajr dawn (05:00)
        Assert.InRange(LiveScenes.StarVisibility(1125), 0.5f, 1.0f); // Maghrib -> Isha (18:45)
    }
}
