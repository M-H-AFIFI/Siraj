using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Siraj.Core.Text;

public static partial class Digits
{
    private const string Arabic = "٠١٢٣٤٥٦٧٨٩";
    private const string Urdu = "۰۱۲۳۴۵۶۷۸۹";

    public static readonly string Lri = ((char)0x2066).ToString();
    public static readonly string Pdi = ((char)0x2069).ToString();
    public static readonly string Fsi = ((char)0x2068).ToString();

    public static string Localize(string input, bool eastern, string? language = null)
    {
        if (!eastern || string.IsNullOrEmpty(input)) return input;
        var digits = language != null && language.StartsWith("ur") ? Urdu : Arabic;
        var sb = new StringBuilder(input.Length);
        foreach (char c in input) sb.Append(c is >= '0' and <= '9' ? digits[c - '0'] : c);
        return sb.ToString();
    }

    public static string ToWestern(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            int a = Arabic.IndexOf(c), u = Urdu.IndexOf(c);
            sb.Append(a >= 0 ? (char)('0' + a) : u >= 0 ? (char)('0' + u) : c);
        }
        return sb.ToString();
    }

    public static string Number(long n, bool eastern, string? language = null) =>
        Localize(n.ToString(CultureInfo.InvariantCulture), eastern, language);

    public static bool UseEastern(string numeralSystem, string language) => numeralSystem switch
    {
        "Arabic" => true,
        "Western" => false,
        _ => language.StartsWith("ar") || language.StartsWith("ur")
    };

    [GeneratedRegex("[٠-٩]+")]
    private static partial Regex ArabicDigitRun();

    public static string IsolateRuns(string text, string language)
    {
        if (language.StartsWith("ar") || language.StartsWith("ur")) return text;
        return ArabicDigitRun().Replace(text, m => Lri + m.Value + Pdi);
    }
}

public static class TimeText
{
    private static readonly string Nbsp = ((char)0xA0).ToString();

    public static bool Is24Hour(string hourFormat) => hourFormat switch
    {
        "24" => true,
        "12" => false,
        _ => CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('H')
    };

    public static string AmPm(bool pm, string language) => language switch
    {
        "ar" => pm ? "م" : "ص",
        "ur" => pm ? "شام" : "صبح",
        "tr" => pm ? "ÖS" : "ÖÖ",
        _ => pm ? "PM" : "AM"
    };

    public static string Format(int hour, int minute, string hourFormat, string numeralSystem, string language)
    {
        bool eastern = Digits.UseEastern(numeralSystem, language);
        if (Is24Hour(hourFormat))
            return Digits.Localize($"{hour:00}:{minute:00}", eastern, language);
        int hour12 = hour == 0 ? 12 : hour > 12 ? hour - 12 : hour;
        return Digits.Localize($"{hour12:00}:{minute:00}", eastern, language) + Nbsp + AmPm(hour >= 12, language);
    }

    public static string Format(DateTimeOffset time, string hourFormat, string numeralSystem, string language) =>
        Format(time.Hour, time.Minute, hourFormat, numeralSystem, language);

    public static string Countdown(TimeSpan span, bool withHours, bool eastern, string language)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        long total = (long)span.TotalSeconds;
        string raw = withHours
            ? $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}"
            : $"{total / 60:00}:{total % 60:00}";
        return Digits.Localize(raw, eastern, language);
    }
}
