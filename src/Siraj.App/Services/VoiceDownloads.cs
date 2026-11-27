using System.Net.Http;
using Siraj.Core.Adhan;

namespace Siraj.App.Services;

internal static class VoiceDownloads
{
    public static readonly string Folder = Path.Combine(AppPaths.Data, "adhan");

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static event Action? Changed;

    public static bool Has(OnlineRecording recording) => File.Exists(Path.Combine(Folder, recording.LocalFile));

    public static void EnsureFor(AdhanVoice voice, bool fajrWanted)
    {
        var missing = AdhanVoices.WantedDownloads(voice, fajrWanted).Where(r => !Has(r)).ToList();
        if (missing.Count == 0) return;
        _ = Task.Run(async () =>
        {
            foreach (var recording in missing) await DownloadAsync(recording);
        });
    }

    public static async Task<bool> DownloadAsync(OnlineRecording recording, CancellationToken cancel = default)
    {
        await Gate.WaitAsync(cancel);
        try
        {
            if (Has(recording)) return true;
            Directory.CreateDirectory(Folder);
            foreach (var url in new[] { recording.Url, recording.Fallback })
            {
                if (await TryDownload(url, recording, cancel))
                {
                    Log.Info($"downloaded {recording.LocalFile}");
                    App.Dispatcher.TryEnqueue(() => Changed?.Invoke());
                    return true;
                }
            }
            return false;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<bool> TryDownload(Uri url, OnlineRecording recording, CancellationToken cancel)
    {
        var final = Path.Combine(Folder, recording.LocalFile);
        var temp = final + ".part";
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
            if (!response.IsSuccessStatusCode) return false;
            await using (var source = await response.Content.ReadAsStreamAsync(cancel))
            await using (var target = File.Create(temp))
            {
                await source.CopyToAsync(target, cancel);
            }
            var info = new FileInfo(temp);
            // Reject truncated or corrupt downloads by checking size tolerance and MP3 header/frame sync.
            if (info.Length < recording.Bytes * 0.8 || info.Length > recording.Bytes * 1.25 || !LooksLikeMp3(temp))
            {
                Log.Error($"rejected {url}: {info.Length} bytes");
                File.Delete(temp);
                return false;
            }
            File.Move(temp, final, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Log.Error($"download {url}", e);
            try { File.Delete(temp); } catch { }
            return false;
        }
    }

    private static bool LooksLikeMp3(string path)
    {
        Span<byte> head = stackalloc byte[3];
        using var file = File.OpenRead(path);
        if (file.Read(head) < 3) return false;
        return (head[0] == 'I' && head[1] == 'D' && head[2] == '3') || (head[0] == 0xFF && (head[1] & 0xE0) == 0xE0);
    }
}
