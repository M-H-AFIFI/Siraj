using Siraj.Core.Audio;
using Siraj.Core.Hijri;
using Siraj.Core.Prayer;
using Siraj.Core.Quran;
using Siraj.Core.Settings;
using Siraj.Core.Sky;
using Siraj.Core.Theme;
using Xunit;

namespace Siraj.Core.Tests;

public class FeaturesTest
{
    [Fact]
    public void KhatmaPlannerSplitsPagesAcrossWholeMushaf()
    {
        var start = new DateOnly(2026, 11, 1);
        var state = KhatmaPlanner.Create(start, targetDays: 30, startPage: 1);
        var summary = KhatmaPlanner.Summarize(state, start);

        Assert.Equal(30, summary.Portions.Count);
        Assert.Equal(1, summary.Portions[0].FromPage);
        Assert.Equal(QuranDatabase.PageCount, summary.Portions[^1].ToPage);
        Assert.Equal(QuranDatabase.PageCount, summary.TotalPages);
        Assert.Equal(0, summary.PagesRead);
        Assert.False(summary.IsFinished);
        for (int i = 1; i < summary.Portions.Count; i++)
            Assert.Equal(summary.Portions[i - 1].ToPage + 1, summary.Portions[i].FromPage);

        // Marking days 1 and 2 done updates progress, pagesRead, and streak.
        state.CompletedDays.AddRange([1, 2]);
        var day2 = KhatmaPlanner.Summarize(state, start.AddDays(1));
        Assert.Equal(2, day2.CompletedDays);
        Assert.Equal(2, day2.Streak);
        Assert.Equal(3, day2.TodayPortion.DayNumber);
        Assert.True(day2.PagesRead > 35);
    }

    [Fact]
    public void KhatmaPlannerCanStartFromLastReadPage()
    {
        var start = new DateOnly(2026, 11, 1);
        var state = KhatmaPlanner.Create(start, targetDays: 10, startPage: 305);
        var summary = KhatmaPlanner.Summarize(state, start);

        Assert.Equal(305, summary.Portions[0].FromPage);
        Assert.Equal(604, summary.Portions[^1].ToPage);
        Assert.Equal(300, summary.TotalPages);
        Assert.Equal(30, KhatmaPlanner.PagesPerDay(10, 305));
    }

    [Fact]
    public void DailyAyahRotatesCleanlyWithAllLanguages()
    {
        var a = DailyAyah.ForDay(new DateOnly(2026, 11, 23));
        var b = DailyAyah.ForDay(new DateOnly(2026, 11, 24));
        Assert.NotEqual(a, b);
        Assert.All(DailyAyah.Pool, v =>
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Arabic));
            Assert.InRange(v.Surah, 1, 114);
            Assert.InRange(v.Page, 1, QuranDatabase.PageCount);
            Assert.False(string.IsNullOrWhiteSpace(v.Translation("en")));
            Assert.False(string.IsNullOrWhiteSpace(v.Translation("tr")));
            Assert.False(string.IsNullOrWhiteSpace(v.Translation("ur")));
        });
    }

    [Fact]
    public void RadioStationsAndListeningLibraryCatalogIsValid()
    {
        Assert.Equal(24, RadioStations.All.Count);
        Assert.Equal(RadioStations.All.Count, RadioStations.All.Select(s => s.Id).Distinct().Count());
        Assert.All(RadioStations.All, s =>
        {
            Assert.True(Uri.TryCreate(s.StreamUrl, UriKind.Absolute, out _), s.Id);
            Assert.False(string.IsNullOrWhiteSpace(s.Name("ar")));
            Assert.False(string.IsNullOrWhiteSpace(s.Name("en")));
        });

        Assert.Equal(75, ListeningLibrary.AllReciters.Count);
        Assert.Equal(12, ListeningLibrary.AmbientSounds.Count);
        Assert.Single(ListeningLibrary.RareCollections);
        Assert.Equal(13, ListeningLibrary.RareCollections[0].Tracks.Count);
        Assert.Equal("img_makkah.jpg", ListeningLibrary.PortraitForAdhan("Makkah"));
    }

    [Fact]
    public void LiveScenesAndGlassSpecCoverAllModesAndFinishes()
    {
        Assert.Equal(17, LiveScenes.All.Count);
        Assert.Equal(17, LiveScenes.All.Select(s => s.Id).Distinct().Count());
        Assert.Equal(3, LiveScenes.Intensities.Count);

        var light = Palettes.Scheme("Rose", false, false);
        var dark = Palettes.Scheme("Emerald", true, false);
        var amoled = Palettes.Scheme("Emerald", true, true);

        var dayColors = LiveScenes.ColorsFor(light, false, timeSync: true, minutesOfDay: 720);
        var nightColors = LiveScenes.ColorsFor(amoled, true, timeSync: true, minutesOfDay: 1320);
        Assert.False(dayColors.IsDark);
        Assert.True(nightColors.IsBlack);

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

    [Fact]
    public void AdhanSkySpecCoversEveryPrayerAndSettlesIntoDeepBase()
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
    public void SettingsPersistsNewGlassLiveAndMediaFields()
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
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void SkyPaletteTintsCoolSkiesAndPreservesWarmDawn()
    {
        var night = SkyTimeline.NightColors;
        var emerald = Palettes.Scheme("Emerald", true, false).Primary;
        var tinted = SkyPalette.Tint(night, emerald);
        Assert.NotEqual(night.Sky[0], tinted.Sky[0]);
        Assert.Equal(night.OrbGlow, tinted.OrbGlow);

        var fullMoon = new MoonPhase(1.0, true, 14.8);
        Assert.InRange(fullMoon.PhaseFraction, 0.48f, 0.52f);
        var waxingQuarter = new MoonPhase(0.5, true, 7.4);
        Assert.InRange(waxingQuarter.PhaseFraction, 0.24f, 0.26f);
        var waningQuarter = new MoonPhase(0.5, false, 22.1);
        Assert.InRange(waningQuarter.PhaseFraction, 0.74f, 0.76f);
    }

    [Fact]
    public void UpdateVersionParsingAndComparison()
    {
        Assert.True(Siraj.Core.Updates.AppVersion.TryParse("v1.2.3", out var v123));
        Assert.Equal(new Siraj.Core.Updates.AppVersion(1, 2, 3), v123);

        Assert.True(Siraj.Core.Updates.AppVersion.TryParse("win-v1.10.0", out var v110));
        Assert.True(v110.IsNewerThan(v123));
        Assert.False(v123.IsNewerThan(v110));
        Assert.False(v123.IsNewerThan(v123));

        Assert.True(Siraj.Core.Updates.AppVersion.TryParse("1.4", out var v140));
        Assert.Equal("1.4.0", v140.ToString());
        Assert.False(Siraj.Core.Updates.AppVersion.TryParse("no-version", out _));
    }

    [Fact]
    public void UpdateFeedParserHandlesGitHubReleasesManifestAndChecksums()
    {
        Assert.Equal(
            "https://api.github.com/repos/M-H-AFIFI/Siraj/releases?per_page=10",
            Siraj.Core.Updates.UpdateFeedParser.BuildFeedUrl("M-H-AFIFI/Siraj"));
        Assert.Equal(
            "https://siraj.app/windows-latest.json",
            Siraj.Core.Updates.UpdateFeedParser.BuildFeedUrl("https://siraj.app/windows-latest.json"));

        string ghJson = """
        [
          {
            "tag_name": "assets-2026",
            "draft": false,
            "prerelease": false,
            "assets": [{ "name": "mushaf.zip", "browser_download_url": "https://example.com/mushaf.zip", "size": 100 }]
          },
          {
            "tag_name": "v2.0.0-beta",
            "draft": false,
            "prerelease": true,
            "assets": [{ "name": "SirajSetup-2.0.0-x64.exe", "browser_download_url": "https://example.com/beta.exe", "size": 500 }]
          },
          {
            "tag_name": "v1.0.5",
            "draft": false,
            "prerelease": false,
            "body": "Bug fixes",
            "assets": [
              { "name": "SirajSetup-1.0.5-x64.exe", "browser_download_url": "https://example.com/105-x64.exe", "size": 70000000 },
              { "name": "SHA256SUMS.txt", "browser_download_url": "https://example.com/SHA256SUMS.txt", "size": 120 }
            ]
          },
          {
            "tag_name": "win-v1.1.0",
            "draft": false,
            "prerelease": false,
            "body": "Taskbar bar & updater",
            "assets": [
              { "name": "SirajSetup-1.1.0-x64.exe", "browser_download_url": "https://example.com/110-x64.exe", "size": 75000000 },
              { "name": "SirajSetup-1.1.0-arm64.exe", "browser_download_url": "https://example.com/110-arm64.exe", "size": 72000000 },
              { "name": "SHA256SUMS.txt", "browser_download_url": "https://example.com/110-SHA256SUMS.txt", "size": 240 }
            ]
          }
        ]
        """;

        var x64 = Siraj.Core.Updates.UpdateFeedParser.ParseGitHubReleasesJson(ghJson, "x64");
        Assert.NotNull(x64);
        Assert.Equal("1.1.0", x64!.Version.ToString());
        Assert.Equal("SirajSetup-1.1.0-x64.exe", x64.FileName);
        Assert.Equal("https://example.com/110-x64.exe", x64.DownloadUrl);
        Assert.Equal("https://example.com/110-SHA256SUMS.txt", x64.Sha256SumsUrl);
        Assert.Equal("Taskbar bar & updater", x64.Notes);

        var arm64 = Siraj.Core.Updates.UpdateFeedParser.ParseGitHubReleasesJson(ghJson, "arm64");
        Assert.NotNull(arm64);
        Assert.Equal("SirajSetup-1.1.0-arm64.exe", arm64!.FileName);

        string sha = new string('a', 64);
        string manifestJson = $$"""
        {
          "version": "1.2.0",
          "notes": "Direct feed update",
          "assets": {
            "x64": {
              "fileName": "SirajSetup-1.2.0-x64.exe",
              "url": "https://cdn.example.com/SirajSetup-1.2.0-x64.exe",
              "sha256": "{{sha.ToUpperInvariant()}}",
              "size": 74000000
            }
          }
        }
        """;
        var manifest = Siraj.Core.Updates.UpdateFeedParser.ParseManifestJson("\uFEFF" + manifestJson, "x64");
        Assert.NotNull(manifest);
        Assert.Equal("1.2.0", manifest!.Version.ToString());
        Assert.Equal(sha, manifest.Sha256);
        Assert.Null(Siraj.Core.Updates.UpdateFeedParser.ParseManifestJson(manifestJson, "arm64"));

        string sums = $"{sha}  SirajSetup-1.2.0-x64.exe\n{new string('b', 64)} *SirajSetup-1.2.0-arm64.exe\n";
        Assert.Equal(sha, Siraj.Core.Updates.UpdateFeedParser.ParseSha256Sums(sums, "SirajSetup-1.2.0-x64.exe"));
        Assert.Equal(new string('b', 64), Siraj.Core.Updates.UpdateFeedParser.ParseSha256Sums(sums, "sirajsetup-1.2.0-arm64.exe"));
        Assert.Null(Siraj.Core.Updates.UpdateFeedParser.ParseSha256Sums("not-a-hash  SirajSetup-1.2.0-x64.exe", "SirajSetup-1.2.0-x64.exe"));
    }

    [Fact]
    public void SettingsPersistUpdatePreferences()
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
        finally { Directory.Delete(dir, true); }
    }
}
