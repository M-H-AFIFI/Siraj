using Siraj.Core.Updates;
using Xunit;

namespace Siraj.Core.Tests;

public class UpdaterTest
{
    [Fact]
    public void AppVersionParsesAndCompares()
    {
        Assert.True(AppVersion.TryParse("v1.2.3", out var v123));
        Assert.Equal(new AppVersion(1, 2, 3), v123);

        Assert.True(AppVersion.TryParse("win-v1.10.0", out var v110));
        Assert.True(v110.IsNewerThan(v123));
        Assert.False(v123.IsNewerThan(v110));
        Assert.False(v123.IsNewerThan(v123));

        Assert.True(AppVersion.TryParse("1.4", out var v140));
        Assert.Equal("1.4.0", v140.ToString());
        Assert.False(AppVersion.TryParse("no-version", out _));
    }

    [Fact]
    public void UpdateFeedParserParsesGitHubReleasesManifestAndChecksums()
    {
        Assert.Equal(
            "https://api.github.com/repos/M-H-AFIFI/Siraj/releases?per_page=10",
            UpdateFeedParser.BuildFeedUrl("M-H-AFIFI/Siraj"));
        Assert.Equal(
            "https://siraj.app/windows-latest.json",
            UpdateFeedParser.BuildFeedUrl("https://siraj.app/windows-latest.json"));

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

        var x64 = UpdateFeedParser.ParseGitHubReleasesJson(ghJson, "x64");
        Assert.NotNull(x64);
        Assert.Equal("1.1.0", x64!.Version.ToString());
        Assert.Equal("SirajSetup-1.1.0-x64.exe", x64.FileName);
        Assert.Equal("https://example.com/110-x64.exe", x64.DownloadUrl);
        Assert.Equal("https://example.com/110-SHA256SUMS.txt", x64.Sha256SumsUrl);
        Assert.Equal("Taskbar bar & updater", x64.Notes);

        var arm64 = UpdateFeedParser.ParseGitHubReleasesJson(ghJson, "arm64");
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
        var manifest = UpdateFeedParser.ParseManifestJson("\uFEFF" + manifestJson, "x64");
        Assert.NotNull(manifest);
        Assert.Equal("1.2.0", manifest!.Version.ToString());
        Assert.Equal(sha, manifest.Sha256);
        Assert.Null(UpdateFeedParser.ParseManifestJson(manifestJson, "arm64"));

        string sums = $"{sha}  SirajSetup-1.2.0-x64.exe\n{new string('b', 64)} *SirajSetup-1.2.0-arm64.exe\n";
        Assert.Equal(sha, UpdateFeedParser.ParseSha256Sums(sums, "SirajSetup-1.2.0-x64.exe"));
        Assert.Equal(new string('b', 64), UpdateFeedParser.ParseSha256Sums(sums, "sirajsetup-1.2.0-arm64.exe"));
        Assert.Null(UpdateFeedParser.ParseSha256Sums("not-a-hash  SirajSetup-1.2.0-x64.exe", "SirajSetup-1.2.0-x64.exe"));
    }
}
