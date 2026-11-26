using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Siraj.Core.Devotions;

public sealed record Dhikr(
    int Id, string Chapter, string Text, int Count,
    string? TransliterationEn, string? TranslationEn, string? TranslationTr,
    string? Reference, string? Commentary)
{
    public string? Translation(string language) => language switch
    {
        "ar" => null,
        "tr" => TranslationTr ?? TranslationEn,
        _ => TranslationEn,
    };

    public string? LocalizedReference(string language) => Tagged.Pick(Reference, language);

    public string? LocalizedCommentary(string language) => Tagged.Pick(Commentary, language);

    public bool IsQuranic => Text.AsSpan().IndexOfAny('ٱ', 'ۡ', 'ۥ') >= 0 || Text.Contains('ۦ');
}

public sealed record AdhkarChapter(int Number, string Title, int Count);

public enum AdhkarGroup { Morning, Evening, Sleep, WakeUp, AfterPrayer, Travel, Ruqiya, Hajj, Essential, Rabbana, Grief, Debt, Mosque, MissedRakah }

public static class AdhkarGroups
{
    public static readonly AdhkarGroup[] Daily = [AdhkarGroup.Morning, AdhkarGroup.Evening, AdhkarGroup.Sleep, AdhkarGroup.WakeUp];
    public static readonly AdhkarGroup[] Prayer = [AdhkarGroup.AfterPrayer, AdhkarGroup.MissedRakah, AdhkarGroup.Mosque];
    public static readonly AdhkarGroup[] Protection = [AdhkarGroup.Ruqiya, AdhkarGroup.Grief, AdhkarGroup.Debt];
    public static readonly AdhkarGroup[] Supplications = [AdhkarGroup.Rabbana, AdhkarGroup.Essential, AdhkarGroup.Hajj, AdhkarGroup.Travel];

    public static string Fragment(this AdhkarGroup group) => group switch
    {
        AdhkarGroup.Morning => "الصباح",
        AdhkarGroup.Evening => "المساء",
        AdhkarGroup.Sleep => "أذكار النوم",
        AdhkarGroup.WakeUp => "الاستيقاظ",
        AdhkarGroup.AfterPrayer => "بعد السلام من الصلاة",
        AdhkarGroup.Travel => "السفر",
        AdhkarGroup.Ruqiya => "الرقية الشرعية",
        AdhkarGroup.Hajj => "الحج",
        AdhkarGroup.Essential => "الاستغفار",
        AdhkarGroup.Rabbana => "ربنا",
        AdhkarGroup.Grief => "الهم والحزن",
        AdhkarGroup.Debt => "قضاء الدين",
        AdhkarGroup.Mosque => "المسجد",
        _ => "المسبوق",
    };

    public static string Title(this AdhkarGroup group, string language)
    {
        string[] t = group switch
        {
            AdhkarGroup.Morning => ["أذكار الصباح", "صبح کے اذکار", "Sabah Zikirleri", "Morning Remembrances"],
            AdhkarGroup.Evening => ["أذكار المساء", "شام کے اذکار", "Akşam Zikirleri", "Evening Remembrances"],
            AdhkarGroup.Sleep => ["أذكار النوم", "سونے کے اذکار", "Uyku Zikirleri", "Sleep Remembrances"],
            AdhkarGroup.WakeUp => ["أذكار الاستيقاظ", "جاگنے کے اذکار", "Uyanma Zikirleri", "Waking Up Remembrances"],
            AdhkarGroup.AfterPrayer => ["أذكار بعد الصلاة", "نماز کے بعد کے اذکار", "Namaz Sonrası Zikirler", "After Prayer Remembrances"],
            AdhkarGroup.Travel => ["أذكار السفر", "سفر کی دعائیں", "Sefer Duaları", "Travel Supplications"],
            AdhkarGroup.Grief => ["أدعية الهم والحزن وتفريج الكرب", "غم اور پریشانی سے نجات کی دعائیں", "Keder ve Sıkıntı Duaları", "Relief from Grief & Distress"],
            AdhkarGroup.Debt => ["أدعية قضاء الدين", "قرض کی ادائیگی کی دعائیں", "Borç Ödeme Duaları", "Debt Relief Supplications"],
            AdhkarGroup.Mosque => ["أذكار المسجد", "مسجد کے اذکار", "Mescid Duaları", "Masjid Supplications"],
            AdhkarGroup.MissedRakah => ["أحكام وأدعية المسبوق", "مسبوق کی نماز اور دعائیں", "Mesbuk Namazı Rehberi", "Missed Rakah Guide"],
            AdhkarGroup.Rabbana => ["أدعية قرآنية (40 ربنا)", "قرآنی ربنا دعائیں", "Kur'an'dan Rabbena Duaları", "Quranic Rabbana Duas"],
            AdhkarGroup.Ruqiya => ["الرقية الشرعية", "مسنون رقیہ شرعیہ", "Şifa ve Rukye Duaları", "Ruqyah Shari'ah"],
            AdhkarGroup.Essential => ["أدعية جامعة ومختارة", "جامع دعائیں", "Temel Dualar", "Essential Duas"],
            _ => ["أدعية الحج والعمرة", "حج اور عمرہ کی دعائیں", "Hac ve Umre Duaları", "Hajj & Umrah Duas"],
        };
        return t[language switch { "ar" => 0, "ur" => 1, "tr" => 2, _ => 3 }];
    }

    public static string ShortTitle(this AdhkarGroup group, string language)
    {
        string[] t = group switch
        {
            AdhkarGroup.Morning => ["Morning", "أذكار الصباح", "Sabah Zikirleri", "صبح کے اذکار"],
            AdhkarGroup.Evening => ["Evening", "أذكار المساء", "Akşam Zikirleri", "شام کے اذکار"],
            AdhkarGroup.Sleep => ["Sleep", "أذكار النوم", "Uyku Zikirleri", "سونے کے اذکار"],
            AdhkarGroup.WakeUp => ["Wake up", "أذكار الاستيقاظ", "Uyanma Zikirleri", "بیدار ہونے کے اذکار"],
            AdhkarGroup.AfterPrayer => ["After Prayer", "أذكار بعد الصلاة", "Namaz Sonrası Zikirler", "نماز کے بعد کے اذکار"],
            AdhkarGroup.Travel => ["Travel", "أذكار السفر", "Sefer Zikirleri", "سفر کے اذکار"],
            AdhkarGroup.Ruqiya => ["Ruqiya", "الرقية الشرعية", "Rukye", "شرعی دم اور رقیہ"],
            AdhkarGroup.Hajj => ["Hajj & Umrah", "الحج والعمرة", "Hac ve Umre", "حج اور عمرہ"],
            AdhkarGroup.Essential => ["Essential", "أدعية مختارة", "Temel Dualar", "منتخب مسنون دعائیں"],
            AdhkarGroup.Rabbana => ["40 Rabbana", "ربنا", "Rabbana Duaları", "ربنا قرآنی دعائیں"],
            AdhkarGroup.Grief => ["Grief", "الهم والحزن", "Keder ve Üzüntü", "غم اور پریشانی کی دعائیں"],
            AdhkarGroup.Debt => ["Debt", "قضاء الدين", "Borç Ödeme", "قرض کی ادائیگی کی دعا"],
            AdhkarGroup.Mosque => ["Masjid", "المسجد", "Mescid", "مسجد کے اذکار"],
            _ => ["Missed Rakah", "المسبوق في الصلاة", "Mesbuk Namazı", "مسبوق کی نماز"],
        };
        return t[language switch { "ar" => 1, "tr" => 2, "ur" => 3, _ => 0 }];
    }

    public static AdhkarGroup Suggested(TimeOnly time) => time.Hour switch
    {
        >= 4 and <= 11 => AdhkarGroup.Morning,
        >= 12 and <= 16 => AdhkarGroup.AfterPrayer,
        >= 17 and <= 21 => AdhkarGroup.Evening,
        _ => AdhkarGroup.Sleep,
    };

    public static string Key(this AdhkarGroup group) => group.ToString();
}

public enum AdhkarTab { All, Daily, Prayer, Protection, Supplications }

public static class AdhkarTabs
{
    public static string Label(this AdhkarTab tab, string language)
    {
        string[] t = tab switch
        {
            AdhkarTab.All => ["الكل", "All", "Tümü", "سب"],
            AdhkarTab.Daily => ["اليومية", "Daily", "Günlük", "روزمرہ"],
            AdhkarTab.Prayer => ["الصلوات", "Prayers", "Namazlar", "نمازیں"],
            AdhkarTab.Protection => ["الحفظ والرقية", "Protection", "Koruma & Rukye", "حفاظت اور رقیہ"],
            _ => ["الأدعية", "Supplications", "Dualar", "دعائیں"],
        };
        return t[language switch { "ar" => 0, "tr" => 2, "ur" => 3, _ => 1 }];
    }

    public static IReadOnlyList<AdhkarGroup> Groups(this AdhkarTab tab) => tab switch
    {
        AdhkarTab.Daily => AdhkarGroups.Daily,
        AdhkarTab.Prayer => AdhkarGroups.Prayer,
        AdhkarTab.Protection => AdhkarGroups.Protection,
        AdhkarTab.Supplications => AdhkarGroups.Supplications,
        _ => [.. AdhkarGroups.Daily, .. AdhkarGroups.Prayer, .. AdhkarGroups.Protection, .. AdhkarGroups.Supplications],
    };

    public static bool Shows(this AdhkarTab tab, string chapter)
    {
        string[] words = tab switch
        {
            AdhkarTab.Daily => ["صباح", "مساء", "نوم", "استيقاظ", "طعام", "لباس", "الثوب", "منزل", "خروج", "دخول"],
            AdhkarTab.Prayer => ["أذان", "اذان", "وضوء", "مسجد", "صلاة", "ركوع", "سجود", "تشهد", "قنوت", "استفتاح", "السلام من", "الوتر"],
            AdhkarTab.Protection => ["وسوسة", "كرب", "الهم", "حزن", "عدو", "شيطان", "عين", "سحر", "مرض", "المريض", "فزع", "الدين", "يعوذ", "الدجال", "الشرك"],
            AdhkarTab.Supplications => ["دعاء", "استغفار", "الاستغفار", "توبة", "سفر", "حج", "عمرة", "صوم", "الصائم", "مطر", "ريح", "رعد", "ربنا"],
            _ => [],
        };
        return tab == AdhkarTab.All || words.Any(w => chapter.Contains(w, StringComparison.Ordinal));
    }
}

public static class AdhkarBook
{
    private static readonly Lazy<(List<Dhikr> All, List<AdhkarChapter> Chapters, Dictionary<string, ChapterTitles> Titles)> Data = new(Load);

    public static IReadOnlyList<Dhikr> All => Data.Value.All;

    public static IReadOnlyList<AdhkarChapter> Chapters => Data.Value.Chapters;

    public static IReadOnlyList<Dhikr> Of(AdhkarGroup group)
    {
        var fragment = group.Fragment();
        return All.Where(d => d.Chapter.Contains(fragment, StringComparison.Ordinal)).ToList();
    }

    public static IReadOnlyList<Dhikr> OfChapter(string title) => All.Where(d => d.Chapter == title).ToList();

    public static string ChapterTitle(string arabic, string language)
    {
        if (language == "ar" || !Data.Value.Titles.TryGetValue(arabic, out var t)) return arabic;
        return language switch { "tr" => t.Tr, "ur" => t.Ur, _ => t.En };
    }

    public static bool ChapterMatches(string arabic, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var q = query.Trim();
        if (Quran.SurahNames.SearchKey(arabic).Contains(Quran.SurahNames.SearchKey(q), StringComparison.Ordinal)) return true;
        if (!Data.Value.Titles.TryGetValue(arabic, out var t)) return false;
        return t.En.Contains(q, StringComparison.OrdinalIgnoreCase) || t.Tr.Contains(q, StringComparison.OrdinalIgnoreCase) ||
               t.Ur.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private static (List<Dhikr>, List<AdhkarChapter>, Dictionary<string, ChapterTitles>) Load()
    {
        var asm = typeof(AdhkarBook).Assembly;
        var book = Read(asm, "Siraj.Core.Data.azkar.json", DevotionsJson.Default.ListBookChapter) ?? [];
        var titles = Read(asm, "Siraj.Core.Data.azkar_chapters.json", DevotionsJson.Default.DictionaryStringChapterTitles) ?? [];
        var all = new List<Dhikr>();
        var chapters = new List<AdhkarChapter>();
        int id = 1;
        foreach (var chapter in book)
        {
            var items = chapter.Azkar ?? [];
            chapters.Add(new AdhkarChapter(chapters.Count + 1, chapter.Title, items.Count));
            foreach (var d in items)
                all.Add(new Dhikr(id++, chapter.Title, d.Text, Math.Max(1, d.Count), d.TransliterationEn, d.TranslationEn,
                    d.TranslationTr, string.IsNullOrWhiteSpace(d.Reward) ? d.Reference : d.Reward, d.Commentary));
        }
        return (all, chapters, titles);
    }

    internal static T? Read<T>(Assembly asm, string name, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info)
    {
        using var stream = asm.GetManifestResourceStream(name) ?? throw new InvalidOperationException("missing resource " + name);
        return JsonSerializer.Deserialize(stream, info);
    }
}

public static class Tagged
{
    public static string? Pick(string? value, string language)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!value.Contains("[ar]") && !value.Contains("[en]")) return value.Trim();
        string prefix = language switch
        {
            "ar" => "[ar]",
            "ur" => value.Contains("[ur]") ? "[ur]" : "[ar]",
            "tr" => value.Contains("[tr]") ? "[tr]" : value.Contains("[en]") ? "[en]" : "[ar]",
            _ => value.Contains("[en]") ? "[en]" : "[ar]",
        };
        int start = value.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0) return value[..Math.Max(0, value.IndexOf('['))].Trim() is { Length: > 0 } s ? s : null;
        var content = value[(start + prefix.Length)..];
        int end = content.IndexOf('[');
        var result = (end >= 0 ? content[..end] : content).Trim();
        return result.Length > 0 ? result : null;
    }
}

public sealed record ChapterTitles(string En, string Tr, string Ur);

public sealed class BookChapter
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public List<BookDhikr>? Azkar { get; set; }
}

public sealed class BookDhikr
{
    public string Text { get; set; } = "";
    public int Count { get; set; } = 1;
    public string? Reward { get; set; }
    public string? Reference { get; set; }
    public string? Commentary { get; set; }
    public string? TranslationEn { get; set; }
    public string? TranslationTr { get; set; }
    public string? TransliterationEn { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<BookChapter>))]
[JsonSerializable(typeof(Dictionary<string, ChapterTitles>))]
[JsonSerializable(typeof(HadithFile))]
[JsonSerializable(typeof(List<AllahName>))]
[JsonSerializable(typeof(List<TasbihPhrase>))]
[JsonSerializable(typeof(List<ZakatCurrency>))]
internal partial class DevotionsJson : JsonSerializerContext;
