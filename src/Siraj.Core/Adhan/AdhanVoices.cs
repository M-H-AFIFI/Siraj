namespace Siraj.Core.Adhan;

public sealed record OnlineRecording(string RemoteFile, string LocalFile, long Bytes)
{
    private const string Jsdelivr = "https://cdn.jsdelivr.net/gh/Kiwifu/adhan-mp3@master/";
    private const string GithubRaw = "https://raw.githubusercontent.com/Kiwifu/adhan-mp3/master/";

    public Uri Url => new(Jsdelivr + Uri.EscapeDataString(RemoteFile));
    public Uri Fallback => new(GithubRaw + Uri.EscapeDataString(RemoteFile));
}

public sealed record AdhanVoice(
    string Id, string Bundled, bool HolyMosque,
    string En, string Ar, string Tr, string Ur,
    OnlineRecording? Better = null, OnlineRecording? Fajr = null)
{
    public string Name(string language) => language switch
    {
        "ar" => Ar,
        "tr" => Tr,
        "ur" => Ur,
        _ => En
    };
}

public static class AdhanVoices
{
    public const string DefaultId = "Makkah";

    public static readonly IReadOnlyList<AdhanVoice> All =
    [
        new("Makkah", "Adhan-Makkah.mp3", true, "Adhan Makkah (Holy Mosque)", "أذان مكة المكرمة (الحرم المكي)", "Mekke-i Mükerreme Ezanı", "مکہ مکرمہ اذان (حرم مکی)",
            Better: new("Ali_Ibn_Ahmad_Mala_HQ.mp3", "Adhan-Makkah-HQ.mp3", 3_171_055),
            Fajr: new("Adhan_Fajr_Al_Haram_Al_Maki_(أذان_الفجر_الحرم_المكي).mp3", "Adhan-Makkah-Fajr.mp3", 450_002)),
        new("Madinah", "Adhan-Madinah.mp3", true, "Adhan Madinah (Prophet's Mosque)", "أذان المدينة المنورة (المسجد النبوي)", "Medine-i Münevvere Ezanı", "مدینہ منورہ اذان (مسجد نبوی)",
            Better: new("Adhan_Al_Haram_Al_Madani_-_Al_Madinah_1_(أذان_الحرم_المدني_-_المدينة_المنورة).mp3", "Adhan-Madinah-HQ.mp3", 379_784),
            Fajr: new("Adhan_Fajr_Al_Haram_Al_Madani_(أذان_الفجر_الحرم_المدني).mp3", "Adhan-Madinah-Fajr.mp3", 561_776)),
        new("Al-Aqsa", "Adhan-Alaqsa.mp3", true, "Adhan Al-Aqsa (Jerusalem)", "أذان المسجد الأقصى المبارك", "Mescid-i Aksa Ezanı", "مسجد اقصی اذان (بیت المقدس)",
            Better: new("Adhan_Al_Aqsa_-_Jerusalem_(أذان_المسجد_الأقصى_-_القدس).mp3", "Adhan-Alaqsa-HQ.mp3", 307_298)),
        new("Abdul Basit", "Abdul-Basit.mp3", false, "Sheikh Abdul Basit Abdus-Samad", "الشيخ عبد الباسط عبد الصمد", "Şeyh Abdussamed", "شیخ عبد الباسط عبد الصمد",
            Fajr: new("Abdulbasit_Abdusamad_7_-_Fajr_Egypt_(عبد_الباسط_عبد_الصمد_-_فجر_مصر).mp3", "Abdul-Basit-Fajr.mp3", 3_095_800)),
        new("Minshawi", "Mohamed-Siddiq-Minshawi.mp3", false, "Sheikh Mohamed Siddiq Al-Minshawi", "الشيخ محمد صديق المنشاوي", "Muhammed Sıddık el-Minşavi", "شیخ محمد صدیق المنشاوی",
            Better: new("Mohamed_Siddiq_El-Minshawi_-_Egypt_1_(محمد_صديق_المنشاوي_-_مصر).mp3", "Mohamed-Siddiq-Minshawi-HQ.mp3", 503_264)),
        new("Mishary Alafasy", "Mishary-Alafasy.mp3", false, "Sheikh Mishary Rashid Alafasy", "الشيخ مشاري راشد العفاسي", "Mişari Raşid el-Afasi", "شیخ مشاری راشد العفاسی",
            Fajr: new("Mishary_Rashid_Alafasy_3_-_Fajr_Kuwait_(مشاري_راشد_العفاسي_-_فجر_الكويت).mp3", "Mishary-Alafasy-Fajr.mp3", 1_503_337)),
        new("Ahmad al-Nafees", "Ahmad-Al-Nafees.mp3", false, "Sheikh Ahmad Al-Nafees", "الشيخ أحمد النفيس", "Ahmed en-Nefis", "شیخ احمد النفیس"),
        new("Mansour Al-Zahrani", "Mansour-Al-Zahrani-Adhan.mp3", false, "Sheikh Mansour Al-Zahrani", "الشيخ منصور الزهراني", "Mansur ez-Zehrani", "شیخ منصور الزہرانی",
            Fajr: new("Mansur_Al_Zahrane_(منصور_الزهراني).mp3", "Mansour-Al-Zahrani-Fajr.mp3", 2_959_028)),
        new("Naghshbandi", "Sayed-Naqshbandi.mp3", false, "Sheikh Sayed Al-Naghshbandi", "الشيخ سيد النقشبندي", "Seyyid Nakşibendi", "شیخ سید نقشبندی",
            Better: new("Sheikh_Al_Naqshbandi_(الشيخ_النقشبندى).mp3", "Sayed-Naqshbandi-HQ.mp3", 2_219_550)),
        new("Nasser Al-Qatami", "Nasser-Al-Qatami.mp3", false, "Sheikh Nasser Al-Qatami", "الشيخ ناصر القطامي", "Nâsır el-Katamî", "شیخ ناصر القطامی"),
    ];

    public static AdhanVoice Find(string? id) =>
        All.FirstOrDefault(v => string.Equals(v.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? All[0];

    public static OnlineRecording FajrFor(AdhanVoice voice) => voice.Fajr ?? All[0].Fajr!;

    public static string FileFor(AdhanVoice voice, bool fajr, bool fajrWanted, string bundledDir, string downloadDir)
    {
        if (fajr && fajrWanted)
        {
            var path = Path.Combine(downloadDir, FajrFor(voice).LocalFile);
            if (File.Exists(path)) return path;
        }
        if (voice.Better != null)
        {
            var path = Path.Combine(downloadDir, voice.Better.LocalFile);
            if (File.Exists(path)) return path;
        }
        return Path.Combine(bundledDir, voice.Bundled);
    }

    public static IEnumerable<OnlineRecording> WantedDownloads(AdhanVoice voice, bool fajrWanted)
    {
        if (voice.Better != null) yield return voice.Better;
        if (fajrWanted) yield return FajrFor(voice);
    }
}
