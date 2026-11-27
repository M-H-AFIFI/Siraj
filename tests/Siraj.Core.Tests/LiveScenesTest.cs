using Siraj.Core.Theme;
using Xunit;

namespace Siraj.Core.Tests;

public class LiveScenesTest
{
    [Fact]
    public void CatalogContainsAll17Scenes()
    {
        Assert.Equal(17, LiveScenes.All.Count);
        Assert.Equal(LiveScenes.All.Count, LiveScenes.All.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(3, LiveScenes.Intensities.Count);
    }

    [Theory]
    [InlineData("DesertDunes", "DesertDunes")]
    [InlineData("purplebutterflies", "PurpleButterflies")]
    [InlineData("PurpleButterflies", "PurpleButterflies")]
    [InlineData("unknown_scene", "DesertDunes")]
    [InlineData("", "DesertDunes")]
    [InlineData(null, "DesertDunes")]
    public void NormalizeIdHandlesKnownAndUnknownIds(string? raw, string expected)
    {
        Assert.Equal(expected, LiveScenes.NormalizeId(raw));
    }

    [Fact]
    public void ColorsForRespectsLightDarkAndBlackModes()
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

        var nightColors = LiveScenes.ColorsFor(amoled, blackMode: true, timeSync: true, minutesOfDay: 1320);
        Assert.True(nightColors.IsBlack);

        var light = Palettes.Scheme("RoyalIndigo", dark: false, black: false);
        var lightColors = LiveScenes.ColorsFor(light, blackMode: false, timeSync: false);
        Assert.False(lightColors.IsDark);
        Assert.Equal(255, lightColors.Highlight.R);

        var dayColors = LiveScenes.ColorsFor(Palettes.Scheme("Rose", dark: false, black: false), blackMode: false, timeSync: true, minutesOfDay: 720);
        Assert.False(dayColors.IsDark);
    }

    [Fact]
    public void StarVisibilityTracksTimeOfDay()
    {
        Assert.Equal(1f, LiveScenes.StarVisibility(60), 3);
        Assert.Equal(0f, LiveScenes.StarVisibility(720), 3);
        Assert.InRange(LiveScenes.StarVisibility(300), 0.1f, 0.5f);
        Assert.InRange(LiveScenes.StarVisibility(1125), 0.5f, 1.0f);
    }

    [Fact]
    public void GlassSpecCoversAllFinishes()
    {
        var light = Palettes.Scheme("Rose", dark: false, black: false);
        var dark = Palettes.Scheme("Emerald", dark: true, black: false);

        var clearLight = GlassSpec.For(light, blackMode: false, finish: "Clear");
        var satinDark = GlassSpec.For(dark, blackMode: false, finish: "Satin");
        var frostedDark = GlassSpec.For(dark, blackMode: false, finish: "Frosted");

        Assert.Equal("Clear", clearLight.Finish);
        Assert.Equal("Satin", satinDark.Finish);
        Assert.Equal("Frosted", frostedDark.Finish);
        Assert.True(frostedDark.TintOpacity > clearLight.TintOpacity);
        Assert.True(clearLight.BlurAmount < satinDark.BlurAmount);
        Assert.True(satinDark.BlurAmount < frostedDark.BlurAmount);
        Assert.True(frostedDark.ChromeBlurAmount > frostedDark.BlurAmount);
        Assert.True(clearLight.SpecularTopStart.A > clearLight.SpecularMid.A);
    }
}
