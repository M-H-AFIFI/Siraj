using Siraj.Core.Settings;
using Xunit;

namespace Siraj.Core.Tests;

public class SettingsTest
{
    [Fact]
    public void AnOlderSettingsFileLoadsWithEmptyLists()
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
}
