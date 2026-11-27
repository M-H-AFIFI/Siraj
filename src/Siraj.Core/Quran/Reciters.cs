namespace Siraj.Core.Quran;

public enum ReciterMode
{
    AyahByAyah,
    SurahSegments
}

public sealed record Reciter(
    string Id, string NameAr, string NameEn, string? NameUr, string? NameTr,
    string? Style, string? StyleAr, string? StyleUr, string? StyleTr,
    ReciterMode Mode, string? EveryAyahFolder, string? SurahServer, string Riwayah)
{
    public string Name(string language) => language switch
    {
        "ar" => NameAr,
        "ur" => NameUr ?? NameAr,
        "tr" => NameTr ?? NameEn,
        _ => NameEn
    };

    public string? StyleName(string language) => language switch
    {
        "ar" => StyleAr ?? Style,
        "ur" => StyleUr ?? StyleAr ?? Style,
        "tr" => StyleTr ?? Style,
        _ => Style
    };
}

public static partial class Reciters
{
    public const string DefaultId = "ar.alafasy";

    public static Reciter Find(string? id) => All.FirstOrDefault(r => r.Id == id) ?? All[0];

    public static Uri AyahUrl(Reciter reciter, int surah, int ayah) =>
        reciter.EveryAyahFolder is { } folder
            ? new Uri($"https://everyayah.com/data/{folder}/{surah:000}{ayah:000}.mp3")
            : SurahUrl(reciter, surah);

    public static Uri SurahUrl(Reciter reciter, int surah)
    {
        var server = reciter.SurahServer ?? (SurahServers.TryGetValue(reciter.Id, out var s) ? s : "https://server8.mp3quran.net/afs/");
        // quranicaudio /qdc/ uses unpadded surah numbers (1.mp3 instead of 001.mp3).
        var name = server.Contains("quranicaudio.com/qdc/") ? surah.ToString() : surah.ToString("000");
        return new Uri(server + name + ".mp3");
    }
}
