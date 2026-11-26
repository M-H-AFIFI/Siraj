namespace Siraj.Core.Prayer;

public sealed record CalculationMethodInfo(string Id, IReadOnlyDictionary<string, string> Names, IReadOnlyDictionary<string, string> UsedIn)
{
    public string Name(string language) => Names.TryGetValue(Base(language), out var n) ? n : Names["en"];

    public string Where(string language) => UsedIn.TryGetValue(Base(language), out var n) ? n : UsedIn["en"];

    private static string Base(string language) => language.Length >= 2 ? language[..2].ToLowerInvariant() : language;
}

public static class CalculationMethods
{
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["MWL"] = "MuslimWorldLeague",
        ["ISNA"] = "NorthAmerica",
        ["Egypt"] = "Egyptian",
        ["Moonsighting"] = "MoonsightingCommittee",
        ["Diyanet"] = "Turkey",
    };

    public const string FallbackMethod = "MuslimWorldLeague";

    public static string CanonicalId(string id) => Aliases.TryGetValue(id, out var c) ? c : id;

    public static CalculationMethodInfo? ById(string id) => All.FirstOrDefault(m => m.Id == CanonicalId(id));

    public static readonly IReadOnlyList<CalculationMethodInfo> All =
    [
        M("MuslimWorldLeague", "Muslim World League", "رابطة العالم الإسلامي", "Dünya İslam Birliği", "رابطۃ العالم الاسلامی",
            "Europe, the Far East and much of the world", "أوروبا والشرق الأقصى ومعظم أنحاء العالم", "Avrupa, Uzak Doğu ve dünyanın çoğu", "یورپ، مشرقِ بعید اور دنیا کے بیشتر حصے"),
        M("UmmAlQura", "Umm Al-Qura, Makkah", "أم القرى، مكة المكرمة", "Ümmü'l-Kura, Mekke", "ام القریٰ، مکہ مکرمہ",
            "Saudi Arabia", "المملكة العربية السعودية", "Suudi Arabistan", "سعودی عرب"),
        M("Egyptian", "Egyptian General Authority of Survey", "الهيئة المصرية العامة للمساحة", "Mısır Genel Araştırma Kurumu", "مصری جنرل اتھارٹی برائے مساحت",
            "Egypt, Africa, Syria and Lebanon", "مصر وأفريقيا وسوريا ولبنان", "Mısır, Afrika, Suriye ve Lübnan", "مصر، افریقہ، شام اور لبنان"),
        M("Karachi", "University of Islamic Sciences, Karachi", "جامعة العلوم الإسلامية بكراتشي", "Karaçi İslami İlimler Üniversitesi", "جامعہ علوم اسلامیہ، کراچی",
            "Pakistan, India, Bangladesh and Afghanistan", "باكستان والهند وبنغلاديش وأفغانستان", "Pakistan, Hindistan, Bangladeş ve Afganistan", "پاکستان، بھارت، بنگلہ دیش اور افغانستان"),
        M("NorthAmerica", "Islamic Society of North America (ISNA)", "الجمعية الإسلامية لأمريكا الشمالية", "Kuzey Amerika İslam Cemiyeti (ISNA)", "اسلامک سوسائٹی آف نارتھ امریکہ",
            "United States and Canada", "الولايات المتحدة وكندا", "ABD ve Kanada", "امریکہ اور کینیڈا"),
        M("Turkey", "Diyanet (Turkey)", "رئاسة الشؤون الدينية التركية", "Diyanet İşleri Başkanlığı", "دیانت، ترکی",
            "Turkey", "تركيا", "Türkiye", "ترکی"),
        M("Dubai", "Dubai Islamic Affairs", "دائرة الشؤون الإسلامية بدبي", "Dubai İslami İşler Dairesi", "دبئی محکمۂ اسلامی امور",
            "United Arab Emirates", "الإمارات العربية المتحدة", "Birleşik Arap Emirlikleri", "متحدہ عرب امارات"),
        M("Qatar", "Ministry of Awqaf, Qatar", "وزارة الأوقاف القطرية", "Katar Evkaf Bakanlığı", "وزارتِ اوقاف، قطر",
            "Qatar", "قطر", "Katar", "قطر"),
        M("Kuwait", "Ministry of Awqaf, Kuwait", "وزارة الأوقاف الكويتية", "Kuveyt Evkaf Bakanlığı", "وزارتِ اوقاف، کویت",
            "Kuwait", "الكويت", "Kuveyt", "کویت"),
        M("Singapore", "MUIS, Singapore", "المجلس الإسلامي في سنغافورة", "Singapur İslam Konseyi (MUIS)", "مجلسِ اسلامی، سنگاپور",
            "Singapore, Malaysia and Indonesia", "سنغافورة وماليزيا وإندونيسيا", "Singapur, Malezya ve Endonezya", "سنگاپور، ملائیشیا اور انڈونیشیا"),
        M("MoonsightingCommittee", "Moonsighting Committee Worldwide", "لجنة رؤية الهلال العالمية", "Dünya Hilal Gözlem Heyeti", "عالمی رویتِ ہلال کمیٹی",
            "Many mosques in the UK and North America", "كثير من مساجد بريطانيا وأمريكا الشمالية", "İngiltere ve Kuzey Amerika'daki birçok cami", "برطانیہ اور شمالی امریکہ کی بہت سی مساجد"),
        M("Tehran", "Institute of Geophysics, Tehran", "معهد الجيوفيزياء بطهران", "Tahran Jeofizik Enstitüsü", "انسٹی ٹیوٹ آف جیو فزکس، تہران",
            "Iran", "إيران", "İran", "ایران"),
    ];

    private static readonly Dictionary<string, string> MethodByCountry = BuildByCountry();

    private static Dictionary<string, string> BuildByCountry()
    {
        var map = new Dictionary<string, string>();
        foreach (var c in new[] { "SA", "YE", "BH", "OM" }) map[c] = "UmmAlQura";
        map["AE"] = "Dubai";
        map["QA"] = "Qatar";
        map["KW"] = "Kuwait";
        foreach (var c in new[] { "EG", "SD", "LY", "SY", "LB", "JO", "PS", "IQ" }) map[c] = "Egyptian";
        foreach (var c in new[] { "PK", "IN", "BD", "AF" }) map[c] = "Karachi";
        map["TR"] = "Turkey";
        map["IR"] = "Tehran";
        foreach (var c in new[] { "SG", "MY", "ID", "BN" }) map[c] = "Singapore";
        foreach (var c in new[] { "US", "CA" }) map[c] = "NorthAmerica";
        return map;
    }

    private static readonly HashSet<string> HanafiCountries = ["PK", "IN", "BD", "AF"];

    public static string RecommendedFor(string? countryCode) =>
        countryCode != null && MethodByCountry.TryGetValue(countryCode.ToUpperInvariant(), out var m) ? m : FallbackMethod;

    public static string RecommendedMadhab(string? countryCode) =>
        countryCode != null && HanafiCountries.Contains(countryCode.ToUpperInvariant()) ? "Hanafi" : "Shafi";

    public static bool NeedsHighLatitudeRule(double? latitude) => latitude != null && Math.Abs(latitude.Value) >= 48.0;

    private static CalculationMethodInfo M(string id, string en, string ar, string tr, string ur, string usedEn, string usedAr, string usedTr, string usedUr) =>
        new(id,
            new Dictionary<string, string> { ["en"] = en, ["ar"] = ar, ["tr"] = tr, ["ur"] = ur },
            new Dictionary<string, string> { ["en"] = usedEn, ["ar"] = usedAr, ["tr"] = usedTr, ["ur"] = usedUr });
}
