using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.UI.Dispatching;
using Siraj.Core.Updates;

namespace Siraj.App.Services;

internal enum UpdateState
{
    Idle,
    Checking,
    UpToDate,
    Available,
    Downloading,
    Verifying,
    ReadyToInstall,
    Installing,
    Failed,
}

internal sealed record UpdateSnapshot(
    UpdateState State,
    UpdateRelease? Release = null,
    double Progress = 0,
    long DownloadedBytes = 0,
    long TotalBytes = 0,
    string? DownloadedPath = null,
    string? ErrorKey = null,
    bool BannerDismissed = false,
    bool JustUpdated = false);

internal sealed class UpdateService : IDisposable
{
    private static readonly TimeSpan AutoCheckInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupCheckDelay = TimeSpan.FromSeconds(8);

    private readonly DispatcherQueue _dispatcher;
    private readonly HttpClient _http;
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _downloadCts;
    private bool _disposed;

    public AppVersion CurrentVersion { get; }
    public string Architecture { get; }
    public string FeedSource { get; }
    public UpdateSnapshot Snapshot { get; private set; }
    public UpdateRelease? AvailableRelease => Snapshot.Release;

    public event Action? StateChanged;

    public UpdateService(DispatcherQueue dispatcher, bool justUpdated)
    {
        _dispatcher = dispatcher;
        CurrentVersion = AppVersion.FromAssemblyVersion(Assembly.GetExecutingAssembly().GetName().Version);
        Architecture = RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64
            ? "arm64"
            : "x64";
        FeedSource = ResolveFeedSource();
        Snapshot = new UpdateSnapshot(UpdateState.Idle, JustUpdated: justUpdated);

        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Siraj-Windows/{CurrentVersion}");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static string UpdatesDirectory => Path.Combine(AppPaths.Data, "updates");

    private static string ResolveFeedSource()
    {
        var env = Environment.GetEnvironmentVariable("SIRAJ_UPDATE_URL");
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim();

        var attr = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "SirajUpdateSource")?.Value;
        return string.IsNullOrWhiteSpace(attr) ? "M-H-AFIFI/Siraj" : attr.Trim();
    }

    public void StartBackgroundCheck()
    {
        _ = Task.Run(CleanupOldInstallers);

        if (Snapshot.JustUpdated && !string.IsNullOrEmpty(App.Settings.Current.SkippedUpdateVersion)
            && AppVersion.TryParse(App.Settings.Current.SkippedUpdateVersion, out var skipped)
            && !skipped.IsNewerThan(CurrentVersion))
        {
            App.Settings.Update(s => s with { SkippedUpdateVersion = null });
        }

        if (!App.Settings.Current.AutoCheckUpdates) return;

        if (App.Settings.Current.LastUpdateCheckUtc is { } last
            && DateTimeOffset.UtcNow - last < AutoCheckInterval)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(StartupCheckDelay, _disposeCts.Token);
                await CheckAsync(manual: false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Log.Error("background update check", e); }
        });
    }

    public async Task CheckAsync(bool manual)
    {
        if (_disposed) return;
        if (Snapshot.State is UpdateState.Checking or UpdateState.Downloading or UpdateState.Verifying or UpdateState.Installing)
            return;

        if (manual)
            Publish(new UpdateSnapshot(UpdateState.Checking));

        try
        {
            string feedUrl = UpdateFeedParser.BuildFeedUrl(FeedSource);
            string json = await ReadTextAsync(feedUrl, _disposeCts.Token);

            UpdateRelease? release = UpdateFeedParser.IsDirectManifestUrl(FeedSource)
                ? UpdateFeedParser.ParseManifestJson(json, Architecture)
                  ?? UpdateFeedParser.ParseGitHubReleasesJson(json, Architecture)
                : UpdateFeedParser.ParseGitHubReleasesJson(json, Architecture);

            App.Settings.Update(s => s with { LastUpdateCheckUtc = DateTimeOffset.UtcNow });

            if (release == null || !release.Version.IsNewerThan(CurrentVersion))
            {
                Publish(manual
                    ? new UpdateSnapshot(UpdateState.UpToDate)
                    : Snapshot with { State = UpdateState.Idle });
                return;
            }

            if (!manual && string.Equals(App.Settings.Current.SkippedUpdateVersion, release.Version.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                Publish(Snapshot with { State = UpdateState.Idle });
                return;
            }

            Log.Info($"update available: v{release.Version} ({release.FileName})");
            Publish(new UpdateSnapshot(UpdateState.Available, Release: release));

            if (!manual && App.MainWindowInstance == null)
            {
                _dispatcher.TryEnqueue(() => Notifier.ShowUpdateAvailable(release.Version.ToString()));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Log.Error("update check failed", e);
            if (manual)
                Publish(new UpdateSnapshot(UpdateState.Failed, ErrorKey: "win_update_failed"));
            else
                Publish(Snapshot with { State = UpdateState.Idle });
        }
    }

    public async Task DownloadAndInstallAsync(bool autoInstall = true)
    {
        if (_disposed) return;

        if (Snapshot.State == UpdateState.ReadyToInstall
            && !string.IsNullOrEmpty(Snapshot.DownloadedPath)
            && File.Exists(Snapshot.DownloadedPath))
        {
            InstallNow();
            return;
        }

        var release = Snapshot.Release;
        if (release == null) return;
        if (Snapshot.State is UpdateState.Downloading or UpdateState.Verifying or UpdateState.Installing)
            return;

        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        var ct = _downloadCts.Token;

        string safeName = Path.GetFileName(release.FileName);
        if (string.IsNullOrWhiteSpace(safeName) || !safeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            safeName = $"SirajSetup-{release.Version}-{Architecture}.exe";

        Directory.CreateDirectory(UpdatesDirectory);
        string finalPath = Path.Combine(UpdatesDirectory, safeName);
        string partPath = finalPath + ".part";

        Publish(new UpdateSnapshot(UpdateState.Downloading, Release: release, Progress: 0, DownloadedBytes: 0, TotalBytes: release.SizeBytes));

        try
        {
            string? expectedSha = release.Sha256;
            if (expectedSha == null && !string.IsNullOrWhiteSpace(release.Sha256SumsUrl))
            {
                try
                {
                    string sums = await ReadTextAsync(release.Sha256SumsUrl, ct);
                    expectedSha = UpdateFeedParser.ParseSha256Sums(sums, release.FileName);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Log.Error("reading SHA256SUMS.txt", e);
                }
            }

            await DownloadFileAsync(release.DownloadUrl, partPath, release, ct);

            Publish(Snapshot with { State = UpdateState.Verifying, Progress = 1.0 });

            if (expectedSha != null)
            {
                string actualSha;
                await using (var verifyStream = File.OpenRead(partPath))
                {
                    byte[] hash = await SHA256.HashDataAsync(verifyStream, ct);
                    actualSha = Convert.ToHexString(hash).ToLowerInvariant();
                }

                if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Error($"update checksum mismatch: expected {expectedSha}, got {actualSha}", null);
                    TryDelete(partPath);
                    Publish(new UpdateSnapshot(UpdateState.Failed, Release: release, ErrorKey: "win_update_checksum_failed"));
                    return;
                }
            }

            File.Move(partPath, finalPath, overwrite: true);
            Publish(new UpdateSnapshot(UpdateState.ReadyToInstall, Release: release, Progress: 1.0, DownloadedPath: finalPath));

            if (autoInstall)
                InstallNow();
        }
        catch (OperationCanceledException)
        {
            TryDelete(partPath);
            Publish(new UpdateSnapshot(UpdateState.Available, Release: release));
        }
        catch (Exception e)
        {
            TryDelete(partPath);
            Log.Error("update download failed", e);
            Publish(new UpdateSnapshot(UpdateState.Failed, Release: release, ErrorKey: "win_update_failed"));
        }
    }

    public void CancelDownload()
    {
        try { _downloadCts?.Cancel(); } catch { }
    }

    public void InstallNow()
    {
        var path = Snapshot.DownloadedPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        try
        {
            Publish(Snapshot with { State = UpdateState.Installing });
            string installDir = AppPaths.Install.TrimEnd('\\', '/');
            string args = $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1 /DIR=\"{installDir}\"";
            Log.Info($"launching updater: {path} {args}");
            Process.Start(new ProcessStartInfo(path, args) { UseShellExecute = true });
            _dispatcher.TryEnqueue(App.Quit);
        }
        catch (Exception e)
        {
            Log.Error("launching installer", e);
            Publish(new UpdateSnapshot(UpdateState.Failed, Release: Snapshot.Release, ErrorKey: "win_update_failed"));
        }
    }

    public void DismissBanner()
    {
        if (Snapshot.JustUpdated)
            Publish(Snapshot with { JustUpdated = false });
        else
            Publish(Snapshot with { BannerDismissed = true });
    }

    public void SkipAvailableVersion()
    {
        if (Snapshot.Release is { } rel)
            App.Settings.Update(s => s with { SkippedUpdateVersion = rel.Version.ToString() });
        Publish(Snapshot with { BannerDismissed = true });
    }

#if DEBUG
    public void LoadDemoState(string mode = "available")
    {
        var demo = new UpdateRelease(
            new AppVersion(CurrentVersion.Major, CurrentVersion.Minor + 1, 0),
            Architecture,
            $"SirajSetup-{CurrentVersion.Major}.{CurrentVersion.Minor + 1}.0-{Architecture}.exe",
            "https://example.invalid/SirajSetup-demo.exe",
            null,
            null,
            76_400_000,
            "Smoother animations, customizable taskbar prayer bar, and faster Quran page loading.",
            null,
            DateTimeOffset.UtcNow);

        Publish(mode.ToLowerInvariant() switch
        {
            "downloading" => new UpdateSnapshot(UpdateState.Downloading, demo, Progress: 0.62, DownloadedBytes: 47_368_000, TotalBytes: 76_400_000),
            "ready" => new UpdateSnapshot(UpdateState.ReadyToInstall, demo, Progress: 1.0, DownloadedPath: Path.Combine(UpdatesDirectory, demo.FileName)),
            "updated" => new UpdateSnapshot(UpdateState.Idle, JustUpdated: true),
            _ => new UpdateSnapshot(UpdateState.Available, demo),
        });
    }
#endif

    private async Task<string> ReadTextAsync(string url, CancellationToken ct)
    {
        if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return await File.ReadAllTextAsync(new Uri(url).LocalPath, ct);

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task DownloadFileAsync(string url, string destinationPath, UpdateRelease release, CancellationToken ct)
    {
        if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            string localSrc = new Uri(url).LocalPath;
            long totalLocal = new FileInfo(localSrc).Length;
            await using var src = File.OpenRead(localSrc);
            await using var dst = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
            await CopyWithProgressAsync(src, dst, totalLocal, release, ct);
            return;
        }

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? release.SizeBytes;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await CopyWithProgressAsync(stream, file, total, release, ct);
    }

    private async Task CopyWithProgressAsync(Stream src, Stream dst, long total, UpdateRelease release, CancellationToken ct)
    {
        byte[] buffer = new byte[65536];
        long copied = 0;
        var lastReport = Stopwatch.StartNew();

        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            copied += read;
            if (lastReport.ElapsedMilliseconds >= 120)
            {
                double progress = total > 0 ? Math.Clamp((double)copied / total, 0, 1) : 0;
                Publish(new UpdateSnapshot(UpdateState.Downloading, release, progress, copied, total));
                lastReport.Restart();
            }
        }

        double finalProgress = total > 0 ? Math.Clamp((double)copied / total, 0, 1) : 1;
        Publish(new UpdateSnapshot(UpdateState.Downloading, release, finalProgress, copied, total));
    }

    private void Publish(UpdateSnapshot next)
    {
        Snapshot = next;
        if (_disposed) return;
        _dispatcher.TryEnqueue(() => StateChanged?.Invoke());
    }

    private static void CleanupOldInstallers()
    {
        try
        {
            if (!Directory.Exists(UpdatesDirectory)) return;
            foreach (var file in Directory.GetFiles(UpdatesDirectory))
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _http.Dispose();
    }
}
