using System.Text.Json;
using System.Text.RegularExpressions;

namespace Siraj.Core.Updates;

public readonly record struct AppVersion(int Major, int Minor, int Patch) : IComparable<AppVersion>
{
    private static readonly Regex Pattern = new(@"(?:^|[^\d])v?(\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public int CompareTo(AppVersion other)
    {
        int c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        return c != 0 ? c : Patch.CompareTo(other.Patch);
    }

    public bool IsNewerThan(AppVersion current) => CompareTo(current) > 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static bool TryParse(string? raw, out AppVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var m = Pattern.Match(raw.Trim());
        if (!m.Success) return false;
        if (!int.TryParse(m.Groups[1].Value, out int maj) || !int.TryParse(m.Groups[2].Value, out int min))
            return false;
        int pat = m.Groups[3].Success && int.TryParse(m.Groups[3].Value, out int p) ? p : 0;
        version = new AppVersion(maj, min, pat);
        return true;
    }

    public static AppVersion FromAssemblyVersion(Version? v) =>
        v == null ? new AppVersion(1, 0, 0) : new AppVersion(Math.Max(0, v.Major), Math.Max(0, v.Minor), Math.Max(0, v.Build));
}

public sealed record UpdateRelease(
    AppVersion Version,
    string Architecture,
    string FileName,
    string DownloadUrl,
    string? Sha256,
    string? Sha256SumsUrl,
    long SizeBytes,
    string Notes,
    string? ReleasePageUrl,
    DateTimeOffset? PublishedAt);

public static class UpdateFeedParser
{
    private static readonly Regex AssetPattern = new(
        @"^SirajSetup-(\d+\.\d+(?:\.\d+)?)-(x64|arm64)\.exe$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsDirectManifestUrl(string source) =>
        source.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || source.StartsWith("file://", StringComparison.OrdinalIgnoreCase);

    public static string BuildFeedUrl(string source)
    {
        var trimmed = (source ?? "").Trim();
        if (IsDirectManifestUrl(trimmed)) return trimmed;
        return $"https://api.github.com/repos/{trimmed.Trim('/')}/releases?per_page=10";
    }

    public static UpdateRelease? ParseManifestJson(string json, string arch)
    {
        using var doc = JsonDocument.Parse(json.TrimStart('\uFEFF'));
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!TryGetString(root, "version", out var verStr) || !AppVersion.TryParse(verStr, out var version))
            return null;

        string normArch = NormalizeArch(arch);
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Object)
            return null;
        if (!TryGetPropertyIgnoreCase(assets, normArch, out var asset) || asset.ValueKind != JsonValueKind.Object)
            return null;

        if (!TryGetString(asset, "url", out var url) || string.IsNullOrWhiteSpace(url))
            return null;
        TryGetString(asset, "fileName", out var fileName);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"SirajSetup-{version}-{normArch}.exe";

        TryGetString(asset, "sha256", out var sha256);
        long size = asset.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var s) ? s : 0;
        TryGetString(root, "notes", out var notes);
        TryGetString(root, "releaseUrl", out var releaseUrl);
        DateTimeOffset? published = TryGetString(root, "publishedAt", out var pubStr) && DateTimeOffset.TryParse(pubStr, out var dt) ? dt : null;

        return new UpdateRelease(
            version,
            normArch,
            fileName,
            url,
            NormalizeSha(sha256),
            null,
            size,
            (notes ?? "").Trim(),
            releaseUrl,
            published);
    }

    public static UpdateRelease? ParseGitHubReleasesJson(string json, string arch)
    {
        using var doc = JsonDocument.Parse(json.TrimStart('\uFEFF'));
        var root = doc.RootElement;
        string normArch = NormalizeArch(arch);

        if (root.ValueKind == JsonValueKind.Object)
            return ParseOneGitHubRelease(root, normArch);

        if (root.ValueKind != JsonValueKind.Array) return null;

        UpdateRelease? best = null;
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var candidate = ParseOneGitHubRelease(item, normArch);
            if (candidate == null) continue;
            if (best == null || candidate.Version.IsNewerThan(best.Version))
                best = candidate;
        }
        return best;
    }

    private static UpdateRelease? ParseOneGitHubRelease(JsonElement rel, string normArch)
    {
        if (rel.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) return null;
        if (rel.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;
        if (!rel.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;

        string? installerName = null, installerUrl = null, sumsUrl = null;
        long installerSize = 0;
        AppVersion? assetVersion = null;

        foreach (var asset in assets.EnumerateArray())
        {
            if (!TryGetString(asset, "name", out var name) || !TryGetString(asset, "browser_download_url", out var url))
                continue;
            if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
            {
                sumsUrl = url;
                continue;
            }
            var m = AssetPattern.Match(name);
            if (m.Success && m.Groups[2].Value.Equals(normArch, StringComparison.OrdinalIgnoreCase))
            {
                if (AppVersion.TryParse(m.Groups[1].Value, out var parsed))
                {
                    installerName = name;
                    installerUrl = url;
                    assetVersion = parsed;
                    installerSize = asset.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var s) ? s : 0;
                }
            }
        }

        if (installerUrl == null || installerName == null) return null;

        AppVersion version;
        if (assetVersion is { } av)
        {
            version = av;
        }
        else if (TryGetString(rel, "tag_name", out var tag) && AppVersion.TryParse(tag, out var tv))
        {
            version = tv;
        }
        else
        {
            return null;
        }

        TryGetString(rel, "body", out var body);
        TryGetString(rel, "html_url", out var pageUrl);
        DateTimeOffset? published = TryGetString(rel, "published_at", out var pubStr) && DateTimeOffset.TryParse(pubStr, out var dt) ? dt : null;

        return new UpdateRelease(
            version,
            normArch,
            installerName,
            installerUrl,
            null,
            sumsUrl,
            installerSize,
            (body ?? "").Trim(),
            pageUrl,
            published);
    }

    public static string? ParseSha256Sums(string content, string fileName)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(fileName)) return null;
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length < 66) continue;
            int sep = line.IndexOfAny([' ', '\t']);
            if (sep != 64) continue;
            string hash = line[..64];
            string target = line[sep..].Trim().TrimStart('*');
            if (target.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return NormalizeSha(hash);
        }
        return null;
    }

    public static string NormalizeArch(string arch) =>
        arch.Equals("arm64", StringComparison.OrdinalIgnoreCase) ? "arm64" : "x64";

    private static string? NormalizeSha(string? sha)
    {
        if (string.IsNullOrWhiteSpace(sha)) return null;
        string s = sha.Trim().ToLowerInvariant();
        if (s.Length != 64) return null;
        foreach (char c in s)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return null;
        return s;
    }

    private static bool TryGetString(JsonElement obj, string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        value = null;
        if (!obj.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String)
            return false;
        value = prop.GetString();
        return value != null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
}
