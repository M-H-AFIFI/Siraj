namespace Siraj.Core.Devotions;

public sealed class Hadith
{
    public int Id { get; set; }
    public string TitleAr { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string? TitleTr { get; set; }
    public string? TitleUr { get; set; }
    public string CategoryAr { get; set; } = "";
    public string CategoryEn { get; set; } = "";
    public string MatnAr { get; set; } = "";
    public string TranslationEn { get; set; } = "";
    public string? TranslationTr { get; set; }
    public string? TranslationUr { get; set; }
    public string NarratorAr { get; set; } = "";
    public string NarratorEn { get; set; } = "";
    public string ReferenceAr { get; set; } = "";
    public string ReferenceEn { get; set; } = "";
    public string? ExplanationAr { get; set; }
    public string? ExplanationEn { get; set; }
    public string? ExplanationTr { get; set; }
    public string? ExplanationUr { get; set; }

    public string Title(string language) => language switch
    {
        "ar" => TitleAr,
        "ur" => Or(TitleUr, TitleEn),
        "tr" => Or(TitleTr, TitleEn),
        _ => TitleEn,
    };

    public string? Translation(string language) => language switch
    {
        "ar" => null,
        "ur" => Or(TranslationUr, TranslationEn),
        "tr" => Or(TranslationTr, TranslationEn),
        _ => TranslationEn,
    };

    public bool HasOwnTranslation(string language) => language switch
    {
        "ur" => !string.IsNullOrWhiteSpace(TranslationUr),
        "tr" => !string.IsNullOrWhiteSpace(TranslationTr),
        _ => true,
    };

    public string? Explanation(string language) => language switch
    {
        "ar" => ExplanationAr,
        "ur" => ExplanationUr ?? ExplanationAr,
        "tr" => ExplanationTr ?? ExplanationEn ?? ExplanationAr,
        _ => ExplanationEn ?? ExplanationAr,
    };

    public string Narrator(string language) => language is "ar" or "ur" ? NarratorAr : NarratorEn;

    public string Reference(string language) => language is "ar" or "ur" ? ReferenceAr : ReferenceEn;

    public string Category(string language) => Hadiths.Category(CategoryEn, CategoryAr, language);

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var q = query.Trim();
        bool Has(string? s) => s != null && s.Contains(q, StringComparison.OrdinalIgnoreCase);
        if (Has(TitleEn) || Has(TitleTr) || Has(TitleUr) || Has(TranslationEn) || Has(TranslationTr) || Has(TranslationUr) ||
            Has(NarratorEn) || Has(ReferenceEn) || Has(CategoryEn)) return true;
        var key = Quran.SurahNames.SearchKey(q);
        return key.Length > 0 && (Quran.SurahNames.SearchKey(MatnAr).Contains(key, StringComparison.Ordinal) ||
                                  Quran.SurahNames.SearchKey(TitleAr).Contains(key, StringComparison.Ordinal));
    }

    private static string Or(string? own, string fallback) => string.IsNullOrWhiteSpace(own) ? fallback : own;
}

public sealed class HadithFile
{
    public Dictionary<string, Dictionary<string, string>> Categories { get; set; } = [];
    public List<Hadith> Hadiths { get; set; } = [];
}

public static class Hadiths
{
    private static readonly Lazy<HadithFile> Data = new(() =>
        AdhkarBook.Read(typeof(Hadiths).Assembly, "Siraj.Core.Data.hadith.json", DevotionsJson.Default.HadithFile) ?? new HadithFile());

    public static IReadOnlyList<Hadith> All => Data.Value.Hadiths;

    public static IReadOnlyList<(string En, string Ar)> Categories =>
        All.Select(h => (h.CategoryEn, h.CategoryAr)).Distinct().ToList();

    public static string Category(string en, string ar, string language) => language switch
    {
        "ar" => ar,
        "ur" => Data.Value.Categories.GetValueOrDefault("ur")?.GetValueOrDefault(en) ?? ar,
        "tr" => Data.Value.Categories.GetValueOrDefault("tr")?.GetValueOrDefault(en) ?? en,
        _ => en,
    };

    public static Hadith ForDay(DateOnly date)
    {
        long epochDay = date.DayNumber - DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber;
        long count = All.Count;
        return All[(int)(((epochDay % count) + count) % count)];
    }
}

public sealed class AllahName
{
    public int Number { get; set; }
    public string NameAr { get; set; } = "";
    public string Transliteration { get; set; } = "";
    public string MeaningEn { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string MeaningAr { get; set; } = "";
    public string DescriptionAr { get; set; } = "";
    public string MeaningTr { get; set; } = "";
    public string DescriptionTr { get; set; } = "";
    public string MeaningUr { get; set; } = "";
    public string DescriptionUr { get; set; } = "";

    public string Meaning(string language) => language switch
    {
        "ar" => MeaningAr, "tr" => MeaningTr, "ur" => MeaningUr, _ => MeaningEn,
    };

    public string Description(string language) => language switch
    {
        "ar" => DescriptionAr, "tr" => DescriptionTr, "ur" => DescriptionUr, _ => DescriptionEn,
    };

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var q = query.Trim();
        if (int.TryParse(Text.Digits.ToWestern(q), out var n)) return n == Number;
        bool Has(string s) => s.Contains(q, StringComparison.OrdinalIgnoreCase);
        if (Has(Transliteration) || Has(MeaningEn) || Has(MeaningTr) || Has(MeaningUr)) return true;
        var key = Quran.SurahNames.SearchKey(q);
        return key.Length > 0 && (Quran.SurahNames.SearchKey(NameAr).Contains(key, StringComparison.Ordinal) ||
                                  Quran.SurahNames.SearchKey(MeaningAr).Contains(key, StringComparison.Ordinal));
    }
}

public static class AllahNames
{
    private static readonly Lazy<List<AllahName>> Data = new(() =>
        AdhkarBook.Read(typeof(AllahNames).Assembly, "Siraj.Core.Data.names_of_allah.json", DevotionsJson.Default.ListAllahName) ?? []);

    public static IReadOnlyList<AllahName> All => Data.Value;

    public static AllahName ForDay(DateOnly date) => All[date.DayNumber % All.Count];
}
