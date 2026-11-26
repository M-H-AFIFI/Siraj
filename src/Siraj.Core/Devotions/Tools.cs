using Siraj.Core.Hijri;

namespace Siraj.Core.Devotions;

public sealed class TasbihPhrase
{
    public string Arabic { get; set; } = "";
    public string Transliteration { get; set; } = "";
    public string TranslationEn { get; set; } = "";
    public string TranslationTr { get; set; } = "";
    public string TranslationUr { get; set; } = "";
    public int DefaultGoal { get; set; } = 33;
    public bool IsCustom { get; set; }

    public string Meaning(string language) => language switch
    {
        "tr" => TranslationTr,
        "ur" => string.IsNullOrWhiteSpace(TranslationUr) ? TranslationEn : TranslationUr,
        "ar" => "",
        _ => TranslationEn,
    };
}

public static class TasbihPhrases
{
    private static readonly Lazy<List<TasbihPhrase>> Data = new(() =>
        AdhkarBook.Read(typeof(TasbihPhrases).Assembly, "Siraj.Core.Data.tasbih.json", DevotionsJson.Default.ListTasbihPhrase) ?? []);

    public static IReadOnlyList<TasbihPhrase> BuiltIn => Data.Value;

    public static (int Count, bool Completed) Increment(int count, int goal)
    {
        int next = goal > 0 && count >= goal ? 1 : count + 1;
        return (next, goal > 0 && next == goal);
    }
}

public sealed class ZakatCurrency
{
    public string Code { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Label { get; set; } = "";
    public string Name { get; set; } = "";
    public double DefaultGoldPrice { get; set; }
    public double DefaultSilverPrice { get; set; }
}

public sealed record ZakatResult(double Total, double Debts, double Net, double Nisab, bool Eligible, double Due);

public static class Zakat
{
    public const double Rate = 0.025;
    public const double GoldNisabGrams = 85;
    public const double SilverNisabGrams = 595;

    private static readonly Lazy<List<ZakatCurrency>> Data = new(() =>
        AdhkarBook.Read(typeof(Zakat).Assembly, "Siraj.Core.Data.zakat_currencies.json", DevotionsJson.Default.ListZakatCurrency) ?? []);

    public static IReadOnlyList<ZakatCurrency> Currencies => Data.Value;

    public static ZakatCurrency Currency(string? code) => Currencies.FirstOrDefault(c => c.Code == code) ?? Currencies[0];

    public static double Nisab(double goldPrice, double silverPrice) =>
        silverPrice > 0 ? SilverNisabGrams * silverPrice : goldPrice > 0 ? GoldNisabGrams * goldPrice : 0;

    public static ZakatResult Calculate(ZakatInputs i)
    {
        double gold = Math.Max(0, i.GoldGrams) * Math.Max(0, i.GoldPrice);
        double silver = Math.Max(0, i.SilverGrams) * Math.Max(0, i.SilverPrice);
        double total = Math.Max(0, i.Cash) + Math.Max(0, i.Investments) + Math.Max(0, i.Business) + gold + silver;
        double debts = Math.Max(0, i.Debts);
        double net = Math.Max(0, total - debts);
        double nisab = Nisab(i.GoldPrice, i.SilverPrice);
        bool eligible = nisab > 0 && net >= nisab;
        return new ZakatResult(total, debts, net, nisab, eligible, eligible ? net * Rate : 0);
    }
}

public sealed record ZakatInputs
{
    public string Currency { get; set; } = "SAR";
    public double Cash { get; set; }
    public double Investments { get; set; }
    public double Business { get; set; }
    public double Debts { get; set; }
    public double GoldGrams { get; set; }
    public double SilverGrams { get; set; }
    public double GoldPrice { get; set; } = 280;
    public double SilverPrice { get; set; } = 3.75;
}

public enum FastOccasion { Monday, Thursday, WhiteDays, Ashura, Arafah, Ramadan }

public sealed record UpcomingFast(FastOccasion Occasion, DateOnly Date, int DaysAway, int HijriDay, int HijriMonth);

public static class Fasting
{
    public static IReadOnlyList<UpcomingFast> Upcoming(DateOnly today, HijriCalendar calendar)
    {
        var results = new List<UpcomingFast>();
        var hijri = calendar.ToHijri(today);
        void Weekly(DayOfWeek day, FastOccasion occasion)
        {
            int away = ((int)day - (int)today.DayOfWeek + 7) % 7;
            var date = today.AddDays(away);
            var h = calendar.ToHijri(date);
            results.Add(new UpcomingFast(occasion, date, away, h.Day, h.Month));
        }
        Weekly(DayOfWeek.Monday, FastOccasion.Monday);
        Weekly(DayOfWeek.Thursday, FastOccasion.Thursday);
        void Yearly(int month, int day, FastOccasion occasion)
        {
            if (NextHijri(month, day, hijri, calendar, today) is { } date)
                results.Add(new UpcomingFast(occasion, date, date.DayNumber - today.DayNumber, day, month));
        }
        Yearly(1, 10, FastOccasion.Ashura);
        Yearly(12, 9, FastOccasion.Arafah);
        Yearly(9, 1, FastOccasion.Ramadan);

        // The nearest of the white days, the 13th to the 15th of a month, within ninety days.
        UpcomingFast? white = null;
        for (int y = hijri.Year; y <= hijri.Year + 1 && white == null; y++)
            for (int m = 1; m <= 12 && white == null; m++)
                for (int d = 13; d <= 15; d++)
                {
                    var date = calendar.ToGregorian(y, m, d);
                    int away = date.DayNumber - today.DayNumber;
                    if (away is >= 0 and <= 90) { white = new UpcomingFast(FastOccasion.WhiteDays, date, away, d, m); break; }
                }
        if (white != null) results.Add(white);
        return results.OrderBy(r => r.DaysAway).ThenBy(r => r.Occasion).ToList();
    }

    private static DateOnly? NextHijri(int month, int day, HijriDay now, HijriCalendar calendar, DateOnly today)
    {
        for (int y = now.Year; y <= now.Year + 1; y++)
        {
            var date = calendar.ToGregorian(y, month, day);
            if (date >= today) return date;
        }
        return null;
    }

    public static int Streak(IReadOnlyDictionary<string, string> log, DateOnly today)
    {
        var day = log.ContainsKey(Key(today)) ? today : today.AddDays(-1);
        int streak = 0;
        while (log.ContainsKey(Key(day))) { streak++; day = day.AddDays(-1); }
        return streak;
    }

    public static string Key(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public static string TypeOf(DateOnly date, HijriCalendar calendar) => calendar.ToHijri(date).Month == 9 ? "ramadan" : "voluntary";
}
