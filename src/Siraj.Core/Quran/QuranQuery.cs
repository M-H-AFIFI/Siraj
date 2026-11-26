using System.Text.RegularExpressions;
using Siraj.Core.Text;

namespace Siraj.Core.Quran;

public static partial class QuranQuery
{
    public static int? Page(string query)
    {
        var m = PageQuery().Match(Clean(query));
        return m.Success && int.TryParse(m.Groups[1].Value, out int page) && page is >= 1 and <= QuranDatabase.PageCount ? page : null;
    }

    public static int? Juz(string query)
    {
        var m = JuzQuery().Match(Clean(query));
        if (!m.Success) return null;
        var number = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        return int.TryParse(number, out int juz) && juz is >= 1 and <= 30 ? juz : null;
    }

    public static (Surah Surah, int Ayah)? Ayah(string query, IReadOnlyList<Surah> surahs)
    {
        var q = Clean(query);
        var numbers = NumberedAyah().Match(q);
        if (numbers.Success)
        {
            int s = int.Parse(numbers.Groups[1].Value), a = int.Parse(numbers.Groups[2].Value);
            var surah = surahs.FirstOrDefault(x => x.Number == s);
            return surah != null && a >= 1 && a <= surah.AyahCount ? (surah, a) : null;
        }

        var named = NamedAyah().Match(q);
        if (!named.Success) return null;
        var key = SurahNames.SearchKey(named.Groups[1].Value);
        if (key.Length == 0 || key.All(char.IsAsciiDigit)) return null;
        int ayah = int.Parse(named.Groups[2].Value);
        var (best, distance) = surahs
            .Select(s => (Surah: s, Distance: Distance(s, key)))
            .Where(x => x.Distance >= 0)
            .OrderBy(x => x.Distance).ThenBy(x => x.Surah.Number)
            .FirstOrDefault();
        // One letter is part of too many names to stand for one, unless it is the whole name: ص, ق.
        if (best == null || (key.Length < 2 && distance != 0)) return null;
        return ayah >= 1 && ayah <= best.AyahCount ? (best, ayah) : null;
    }

    public static bool? Revelation(string query)
    {
        var key = SurahNames.SearchKey(query);
        if (MeccanWords.Contains(key)) return true;
        if (MedinanWords.Contains(key)) return false;
        return null;
    }

    // Folded surah names for diacritic-insensitive search.
    private static readonly HashSet<string> MeccanWords =
        Folded("مكية", "مكي", "مکی", "مکیہ", "meccan", "makki", "makkan", "mekki", "mekkî", "mekke");

    private static readonly HashSet<string> MedinanWords =
        Folded("مدنية", "مدني", "مدنی", "مدنیہ", "medinan", "madani", "madinan", "medeni", "medenî", "medine");

    private static HashSet<string> Folded(params string[] words) => words.Select(SurahNames.SearchKey).ToHashSet();

    private static int Distance(Surah surah, string key)
    {
        int best = -1;
        foreach (var name in new[] { surah.EnglishName, surah.Name, surah.EnglishNameTranslation })
        {
            var folded = SurahNames.SearchKey(name);
            if (folded.Contains(key, StringComparison.Ordinal) && (best < 0 || folded.Length - key.Length < best))
                best = folded.Length - key.Length;
        }
        return best;
    }

    private static string Clean(string query) => Digits.ToWestern(query ?? "").Trim();

    [GeneratedRegex(@"^(?:(?:page|p\.?|sayfa|s\.|صفحة|صفحه|صفحہ|ص)\s*)?(\d{1,3})$", RegexOptions.IgnoreCase)]
    private static partial Regex PageQuery();

    [GeneratedRegex(@"^(?:(?:juz'?|juzʾ|jüz|cüz|cuz|para|parah|الجزء|جزء|جز|پارہ|پاره|سپارہ|سپاره)\s*(\d{1,2})|(\d{1,2})\.?\s*(?:juz'?|jüz|cüz|cuz|para))$", RegexOptions.IgnoreCase)]
    private static partial Regex JuzQuery();

    [GeneratedRegex(@"^(\d{1,3})\s*[:/,\-\s]\s*(\d{1,3})$")]
    private static partial Regex NumberedAyah();

    [GeneratedRegex(@"^(.+?)\s*(?:[:/,\-]|آية|اية|ayah|ayet|آیت|\s)\s*(\d{1,3})$", RegexOptions.IgnoreCase)]
    private static partial Regex NamedAyah();
}
