using System.Globalization;
using System.Text;

namespace Siraj.Core.Quran;

public static class DigitalKhattText
{
    private const string WordGap = "   ";

    public static string ForDisplay(string text) => ForDisplay(text, []).Text;

    public static (string Text, IReadOnlyList<(int Start, int End)> Ranges) ForDisplay(string text, IEnumerable<(int Start, int End)> ranges)
    {
        // Map each source char index to its display index (-1 for spaces).
        var at = new int[text.Length + 1];
        Array.Fill(at, -1);
        var shown = new StringBuilder(text.Length + text.Length / 2);
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == ' ')
            {
                i++;
                continue;
            }
            int start = i;
            while (i < text.Length && text[i] != ' ') i++;
            var word = text.AsSpan(start, i - start);
            if (shown.Length > 0 && !IsPauseSign(word)) shown.Append(WordGap);
            for (int k = start; k < i; k++) at[k] = shown.Length + (k - start);
            shown.Append(word);
        }
        at[text.Length] = shown.Length;

        // Trim spans starting or ending on spaces inward to the nearest character.
        int From(int s)
        {
            s = Math.Clamp(s, 0, text.Length);
            while (s < text.Length && at[s] < 0) s++;
            return at[s];
        }
        int To(int e)
        {
            e = Math.Clamp(e, 0, text.Length);
            while (e > 0 && at[e - 1] < 0) e--;
            return e == 0 ? 0 : at[e - 1] + 1;
        }
        var moved = ranges.Select(r => (From(r.Start), To(r.End))).Where(r => r.Item1 < r.Item2).ToList();
        return (shown.ToString(), moved);
    }

    private static bool IsPauseSign(ReadOnlySpan<char> word)
    {
        bool letter = false, mark = false;
        foreach (char c in word)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            letter |= category == UnicodeCategory.OtherLetter;
            mark |= category == UnicodeCategory.NonSpacingMark;
        }
        return !letter && mark;
    }
}
