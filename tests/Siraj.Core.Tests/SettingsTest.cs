using Siraj.Core.Settings;
using Xunit;

namespace Siraj.Core.Tests;

public class SettingsTest
{
    [Fact]
    public void LoadsPartialSettingsWithDefaultCollections()
    {
        var dir = Path.Combine(Path.GetTempPath(), "siraj-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "settings.json"), """{ "language": "ar", "bookmarks": null, "widgets": [ { "face": "day" } ] }""");
            var settings = new SettingsStore(dir).Current;
            Assert.Equal("ar", settings.Language);
            Assert.NotNull(settings.Bookmarks);
            var widget = Assert.Single(settings.Widgets);
            Assert.Equal("glass", widget.Background);
            Assert.Equal(1.0, widget.Scale);
            Assert.True(double.IsNaN(widget.X));
            Assert.NotNull(settings.MinuteOffsets);
            Assert.NotNull(settings.PrayerAlerts);
            Assert.True(settings.QuranSpread);
            Assert.Equal(80, settings.AdhanVolume);
            Assert.Equal(Quran.Reciters.DefaultId, settings.ReciterId);
            Assert.Equal("Emerald", settings.Palette);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PersistsAppearanceMediaAndTaskbarSettings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "siraj-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            store.Update(s => s with
            {
                GlassUi = true,
                GlassFinish = "Frosted",
                LiveBackground = true,
                LiveScene = "Aurora",
                LiveIntensity = "Vivid",
                LiveSkySync = true,
                PinnedRadioIds = ["quran_cairo", "quran_saudi"],
                LastListenReciterId = "ar.alafasy",
                LastListenSurah = 18,
                AmbientSound = "RAIN",
                AmbientVolume = 0.45,
            });
            var reloaded = new SettingsStore(dir).Current;
            Assert.Equal("Frosted", reloaded.GlassFinish);
            Assert.Equal("Aurora", reloaded.LiveScene);
            Assert.Equal("Vivid", reloaded.LiveIntensity);
            Assert.True(reloaded.LiveSkySync);
            Assert.Equal(["quran_cairo", "quran_saudi"], reloaded.PinnedRadioIds);
            Assert.Equal(18, reloaded.LastListenSurah);
            Assert.Equal("RAIN", reloaded.AmbientSound);
            Assert.Equal(0.45, reloaded.AmbientVolume, 4);

            store.Update(s => s with
            {
                TaskbarWidget = s.TaskbarWidget with
                {
                    Layout = "compact",
                    Style = "none",
                    Palette = "Sapphire",
                    TextColor = "gold",
                    UrgentStyle = "coral",
                    Position = "custom",
                    CustomPositionPercent = 42.5,
                    OffsetX = -48,
                    Locked = true,
                    CornerRadius = 18,
                    Scale = 1.15,
                    Opacity = 75,
                    ShowBorder = false,
                    ShowPrefix = false,
                    ShowSeconds = true,
                    UrgentMinutes = 10,
                    ShowFlyoutVerse = false,
                },
            });
            var tb = new SettingsStore(dir).Current.TaskbarWidget;
            Assert.True(tb.Enabled);
            Assert.Equal("compact", tb.Layout);
            Assert.Equal("none", tb.Style);
            Assert.Equal("Sapphire", tb.Palette);
            Assert.Equal("gold", tb.TextColor);
            Assert.Equal("coral", tb.UrgentStyle);
            Assert.Equal("custom", tb.Position);
            Assert.Equal(42.5, tb.CustomPositionPercent, 2);
            Assert.Equal(-48, tb.OffsetX);
            Assert.True(tb.Locked);
            Assert.Equal(18, tb.CornerRadius);
            Assert.Equal(1.15, tb.Scale, 3);
            Assert.Equal(75, tb.Opacity);
            Assert.False(tb.ShowBorder);
            Assert.False(tb.ShowPrefix);
            Assert.True(tb.ShowSeconds);
            Assert.Equal(10, tb.UrgentMinutes);
            Assert.False(tb.ShowFlyoutVerse);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PersistsUpdatePreferences()
    {
        var dir = Path.Combine(Path.GetTempPath(), "siraj-update-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            Assert.True(store.Current.AutoCheckUpdates);
            var checkedAt = new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
            store.Update(s => s with
            {
                AutoCheckUpdates = false,
                SkippedUpdateVersion = "1.1.0",
                LastUpdateCheckUtc = checkedAt,
            });
            var reloaded = new SettingsStore(dir).Current;
            Assert.False(reloaded.AutoCheckUpdates);
            Assert.Equal("1.1.0", reloaded.SkippedUpdateVersion);
            Assert.Equal(checkedAt, reloaded.LastUpdateCheckUtc);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
