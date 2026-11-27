using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Siraj.Core.Quran;

public readonly record struct DownloadProgress(long Received, long Total, int Item, int Items)
{
    public double Fraction => Total <= 0 ? 0 : Math.Clamp((double)Received / Total, 0, 1);
}

public sealed class QuranAssets(string root)
{
    public const string AssetVersion = "2";

    private const string ReleaseBase = "https://github.com/M-H-AFIFI/Siraj-assets/releases/download/v1.0.0/";

    private sealed record Payload(string RemoteName, long Bytes, string Sha256, bool Zip);

    private static readonly Payload[] Payloads =
    [
        new("quran_data.db.gz", 40_249_980, "0e3608ceeae6f520520082fe799ed4cd0f165f8aa3a765764bdf06382207a647", Zip: false),
        new("quran-page-fonts.zip", 54_554_047, "b289ba0ffa875f57cb89edf7c0a3871d603329dff8e89955b98c291ad3778d4a", Zip: true),
    ];

    public static long TotalBytes => Payloads.Sum(p => p.Bytes);

    public string Root { get; } = root;
    public string DatabasePath => Path.Combine(Root, "quran_data.db");
    public string FontsDirectory => Path.Combine(Root, "quran-pages");
    private string Marker => Path.Combine(Root, ".installed");

    public bool IsInstalled =>
        File.Exists(Marker) && File.ReadAllText(Marker).Trim() == AssetVersion &&
        File.Exists(DatabasePath) && File.Exists(Path.Combine(FontsDirectory, "QCF_P604.ttf")) && File.Exists(Path.Combine(FontsDirectory, "QCF_BSML.ttf"));

    public string PageFont(int page) => Path.Combine(FontsDirectory, $"QCF_P{page:000}.ttf");
    public string BasmalahFont => Path.Combine(FontsDirectory, "QCF_BSML.ttf");

    public async Task InstallAsync(HttpClient http, IProgress<DownloadProgress>? progress, CancellationToken cancel)
    {
        Directory.CreateDirectory(Root);
        long done = 0;
        for (int i = 0; i < Payloads.Length; i++)
        {
            var payload = Payloads[i];
            var archive = Path.Combine(Root, payload.RemoteName);
            long before = done;
            await DownloadAsync(http, payload, archive, received => progress?.Report(new DownloadProgress(before + received, TotalBytes, i + 1, Payloads.Length)), cancel);
            done += payload.Bytes;
            if (payload.Zip) UnzipFonts(archive, cancel);
            else Gunzip(archive, DatabasePath, cancel);
            File.Delete(archive);
        }
        File.WriteAllText(Marker, AssetVersion);
    }

    private static async Task DownloadAsync(HttpClient http, Payload payload, string target, Action<long> onBytes, CancellationToken cancel)
    {
        var part = target + ".part";
        if (File.Exists(target) && await HashMatches(target, payload.Sha256, cancel))
        {
            onBytes(payload.Bytes);
            return;
        }

        for (int attempt = 1; ; attempt++)
        {
            long have = File.Exists(part) ? new FileInfo(part).Length : 0;
            if (have > payload.Bytes)
            {
                File.Delete(part);
                have = 0;
            }
            try
            {
                if (have < payload.Bytes)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, ReleaseBase + payload.RemoteName);
                    if (have > 0) request.Headers.Range = new RangeHeaderValue(have, null);
                    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
                    // Server ignored Range header; restart from byte 0.
                    if (have > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent) have = 0;
                    response.EnsureSuccessStatusCode();
                    await using var source = await response.Content.ReadAsStreamAsync(cancel);
                    await using var file = new FileStream(part, have > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
                    var buffer = new byte[1 << 16];
                    long received = have;
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancel)) > 0)
                    {
                        await file.WriteAsync(buffer.AsMemory(0, read), cancel);
                        received += read;
                        onBytes(received);
                    }
                }
                if (!await HashMatches(part, payload.Sha256, cancel))
                {
                    File.Delete(part);
                    throw new InvalidDataException($"SHA-256 mismatch for {payload.RemoteName}.");
                }
                File.Move(part, target, overwrite: true);
                return;
            }
            catch (Exception e) when (e is HttpRequestException or IOException && attempt < 4 && !cancel.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1.5 * attempt), cancel);
            }
        }
    }

    private static async Task<bool> HashMatches(string path, string expected, CancellationToken cancel)
    {
        await using var file = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(file, cancel);
        return Convert.ToHexString(hash).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void Gunzip(string archive, string target, CancellationToken cancel)
    {
        var temp = target + ".tmp";
        using (var source = new GZipStream(File.OpenRead(archive), CompressionMode.Decompress))
        using (var file = File.Create(temp))
        {
            source.CopyTo(file);
        }
        cancel.ThrowIfCancellationRequested();
        File.Move(temp, target, overwrite: true);
    }

    private void UnzipFonts(string archive, CancellationToken cancel)
    {
        var staging = FontsDirectory + ".tmp";
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);
        var stagingFull = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
        using (var zip = ZipFile.OpenRead(archive))
        {
            foreach (var entry in zip.Entries)
            {
                cancel.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var destination = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                if (!destination.StartsWith(stagingFull, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Invalid archive entry path: {entry.FullName}");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true);
            }
        }
        if (Directory.Exists(FontsDirectory)) Directory.Delete(FontsDirectory, recursive: true);
        Directory.Move(staging, FontsDirectory);
    }
}
