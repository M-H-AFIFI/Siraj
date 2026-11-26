using System.Text;

namespace Siraj.Core.Quran;

public static class ArabicSearch
{
    public const int MinQueryLength = 2;

    public static bool IsDropped(char ch) => ch switch
    {
        'ـ' => true, // tatweel
        'ء' => true, // standalone hamza
        'ٰ' => true, // dagger alef
        >= 'ً' and <= 'ٟ' => true, // tashkeel and the marks after it
        >= 'ۖ' and <= 'ۭ' => true, // Quranic annotation and waqf signs
        'ࣰ' or 'ࣱ' or 'ࣲ' => true, // open tanween
        '​' or '‌' or '‍' or '‎' or '‏' => true,
        _ => false
    };

    public static char Fold(char ch) => ch switch
    {
        'آ' or 'أ' or 'إ' or 'ٱ' or 'ٲ' or 'ٳ' => 'ا',
        'ى' or 'ئ' or 'ی' => 'ي',
        'ؤ' => 'و',
        'ة' => 'ه',
        'ک' or 'ڪ' => 'ك',
        'ھ' => 'ه',
        _ => char.ToLowerInvariant(ch)
    };

    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new StringBuilder(text.Length);
        foreach (char ch in text)
        {
            if (IsDropped(ch)) continue;
            sb.Append(Fold(ch));
        }
        return sb.ToString();
    }

    public static string NormalizeQuery(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var folded = Normalize(raw);
        var sb = new StringBuilder(folded.Length);
        bool lastSpace = false;
        foreach (char ch in folded)
        {
            bool space = char.IsWhiteSpace(ch);
            if (space)
            {
                if (!lastSpace && sb.Length > 0) sb.Append(' ');
            }
            else sb.Append(ch);
            lastSpace = space;
        }
        return sb.ToString().TrimEnd();
    }

    public static IReadOnlyList<(int Start, int End)> MatchRanges(string displayText, string normalizedQuery, int limit = 8)
    {
        if (string.IsNullOrEmpty(displayText) || string.IsNullOrEmpty(normalizedQuery) || limit <= 0) return [];
        var folded = new StringBuilder(displayText.Length);
        var source = new int[displayText.Length + 1];
        int kept = 0;
        for (int i = 0; i < displayText.Length; i++)
        {
            char ch = displayText[i];
            if (IsDropped(ch)) continue;
            source[kept++] = i;
            folded.Append(Fold(ch));
        }
        source[kept] = displayText.Length;
        var haystack = folded.ToString();
        var hits = new List<(int, int)>();
        int from = 0;
        while (hits.Count < limit)
        {
            int at = haystack.IndexOf(normalizedQuery, from, StringComparison.Ordinal);
            if (at < 0) break;
            int end = at + normalizedQuery.Length;
            hits.Add((source[at], source[end]));
            from = end;
        }
        return hits;
    }
}
