using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Siraj.Core.Quran;

public static partial class SurahNames
{
    private const string OldConvention = "ĥĤşŞţŢđĐ`'á‘’";

    [GeneratedRegex(@"(^|[\s-])'")]
    private static partial Regex WordInitialApostrophe();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public static string Transliteration(string raw)
    {
        if (raw.Length == 0 || raw.IndexOfAny(OldConvention.ToCharArray()) < 0) return raw;
        var respelled = raw.Replace("aá", "ā").Replace('á', 'ā').Replace("Anbyā", "Anbiyā");
        var trimmed = WordInitialApostrophe().Replace(respelled, m => m.Groups[1].Value);
        var sb = new StringBuilder(trimmed.Length);
        foreach (char ch in trimmed)
        {
            sb.Append(ch switch
            {
                'ĥ' => 'ḥ', 'Ĥ' => 'Ḥ',
                'ş' => 'ṣ', 'Ş' => 'Ṣ',
                'ţ' => 'ṭ', 'Ţ' => 'Ṭ',
                'đ' => 'ḍ', 'Đ' => 'Ḍ',
                '`' or '‘' => 'ʿ',
                '\'' or '’' => 'ʾ',
                _ => ch,
            });
        }
        return sb.ToString();
    }

    public static string ForLanguage(Surah surah, string language) =>
        IsArabicScript(language) ? surah.Name : (surah.EnglishName.Length > 0 ? surah.EnglishName : surah.Name);

    public static bool IsArabicScript(string language) => language.StartsWith("ar") || language.StartsWith("ur");

    private static readonly HashSet<string> SurahWords = ["surah", "sura", "surat", "sure", "suresi", "سوره", "سورت", "سورۃ"];

    public static bool Matches(Surah surah, string query)
    {
        var key = SearchKey(query);
        if (key.Length == 0) return true;
        if (key.All(char.IsAsciiDigit)) return int.TryParse(key, out int n) && n == surah.Number;
        return SearchKey(surah.EnglishName).Contains(key) || SearchKey(surah.Name).Contains(key) ||
               SearchKey(surah.EnglishNameTranslation).Contains(key);
    }

    internal static string SearchKey(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var words = Whitespace().Split(text.Trim().ToLowerInvariant());
        var kept = words.Where(w => !SurahWords.Contains(FoldWord(w))).ToArray();
        if (kept.Length == 0) kept = words;
        return string.Concat(kept.Select(FoldWord));
    }

    private static string FoldWord(string word)
    {
        var decomposed = word.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (char ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            char mapped = ch switch
            {
                'đ' => 'd',
                'ı' => 'i',
                'ٱ' => 'ا',
                'ى' or 'ی' => 'ي',
                'ة' or 'ۃ' or 'ہ' or 'ھ' => 'ه',
                'ک' => 'ك',
                >= '٠' and <= '٩' => (char)('0' + (ch - '٠')),
                >= '۰' and <= '۹' => (char)('0' + (ch - '۰')),
                _ => ch,
            };
            if (char.IsLetterOrDigit(mapped) && mapped is not ('ـ' or 'ʾ' or 'ʿ')) sb.Append(mapped);
        }
        return sb.ToString();
    }
}
