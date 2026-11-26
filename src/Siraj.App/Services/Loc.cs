using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Siraj.Core.Prayer;
using Siraj.Core.Text;

namespace Siraj.App.Services;

internal sealed class Loc
{
    public static readonly string[] Languages = ["en", "ar", "tr", "ur"];

    private static readonly Dictionary<string, Loc> Cache = new();

    public static Loc Current { get; private set; } = For("en");

    public string Language { get; }
    public bool IsRtl => Language is "ar" or "ur";
    public CultureInfo Culture { get; }

    private readonly Dictionary<string, string> _strings;
    private readonly Dictionary<string, Dictionary<string, string>> _plurals;
    private readonly Loc? _fallback;

    private Loc(string language, Loc? fallback)
    {
        Language = language;
        _fallback = fallback;
        Culture = CultureInfo.GetCultureInfo(language switch { "ar" => "ar-SA", "tr" => "tr-TR", "ur" => "ur-PK", _ => "en-US" });
        (_strings, _plurals) = Read(language);
    }

    public static Loc For(string language)
    {
        if (!Languages.Contains(language)) language = "en";
        lock (Cache)
        {
            if (Cache.TryGetValue(language, out var loc)) return loc;
            var english = language == "en" ? null : For("en");
            return Cache[language] = new Loc(language, english);
        }
    }

    public static void Use(string language) => Current = For(language);

    public static string SystemLanguage()
    {
        var two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Languages.Contains(two) ? two : "en";
    }

    public string this[string key] =>
        _strings.TryGetValue(key, out var s) ? s : _fallback?[key] ?? key;

    public bool Has(string key) => _strings.ContainsKey(key) || (_fallback?.Has(key) ?? false);

    public string Format(string key, params object[] args)
    {
        try { return string.Format(Culture, this[key], args); }
        catch (FormatException) { return this[key]; }
    }

    public string Plural(string key, int count, params object[] args)
    {
        var forms = _plurals.TryGetValue(key, out var p) ? p : null;
        if (forms == null) return _fallback?.Plural(key, count, args) ?? key;
        var category = PluralCategory(count);
        if (!forms.TryGetValue(category, out var text) && !forms.TryGetValue("other", out text)) return key;
        object[] all = args.Length > 0 ? args : [Number(count)];
        try { return string.Format(Culture, text, all); } catch (FormatException) { return text; }
    }

    private string PluralCategory(int n) => Language switch
    {
        "ar" => n == 0 ? "zero" : n == 1 ? "one" : n == 2 ? "two" : n % 100 is >= 3 and <= 10 ? "few" : n % 100 is >= 11 and <= 99 ? "many" : "other",
        _ => n == 1 ? "one" : "other"
    };

    public string Sep => IsRtl ? "، " : " · ";

    public string Percent(double value)
    {
        var digits = Number((long)Math.Round(value));
        return Core.Text.Digits.UseEastern(App.Settings.Current.NumeralSystem, Language) ? digits + "٪" : digits + "%";
    }

    public string Number(long n) => Digits.Number(n, Digits.UseEastern(App.Settings.Current.NumeralSystem, Language), Language);

    public string Digitize(string text) => Digits.Localize(text, Digits.UseEastern(App.Settings.Current.NumeralSystem, Language), Language);

    public string PrayerName(PrayerType type) => this[type switch
    {
        PrayerType.Fajr => "prayer_fajr",
        PrayerType.Sunrise => "prayer_sunrise",
        PrayerType.Dhuhr => "prayer_dhuhr",
        PrayerType.Asr => "prayer_asr",
        PrayerType.Maghrib => "prayer_maghrib",
        _ => "prayer_isha"
    }];

    public string PrayerName(PrayerTime prayer) =>
        prayer.Type == PrayerType.Dhuhr && prayer.Time.DayOfWeek == DayOfWeek.Friday ? this["prayer_jummah"] : PrayerName(prayer.Type);

    public string Time(DateTimeOffset time) =>
        TimeText.Format(time, App.Settings.Current.HourFormat, App.Settings.Current.NumeralSystem, Language);

    private static (Dictionary<string, string>, Dictionary<string, Dictionary<string, string>>) Read(string language)
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        var plurals = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Siraj.App.Strings.{language}.json");
        if (stream == null) return (strings, plurals);
        using var doc = JsonDocument.Parse(stream);
        foreach (var p in doc.RootElement.GetProperty("strings").EnumerateObject()) strings[p.Name] = p.Value.GetString() ?? "";
        foreach (var p in doc.RootElement.GetProperty("plurals").EnumerateObject())
        {
            var forms = new Dictionary<string, string>();
            foreach (var f in p.Value.EnumerateObject()) forms[f.Name] = f.Value.GetString() ?? "";
            plurals[p.Name] = forms;
        }
        return (strings, plurals);
    }
}
