using Siraj.Core.Quran;

namespace Siraj.Core.Audio;

public sealed record RareTrack(string CollectionId, int[] Surahs, bool WholeSurah, string Path, int DurationSec, string? ExternalUrl = null)
{
    public Uri Url => ExternalUrl != null
        ? new Uri(ExternalUrl)
        : new Uri($"https://cdn.jsdelivr.net/gh/M-H-AFIFI/Siraj-assets@main/{Path}");
}

public sealed record RareCollection(
    string Id, string NameAr, string NameEn, string NameTr, string NameUr,
    string PortraitFile, IReadOnlyList<RareTrack> Tracks, string Riwayah = "حفص عن عاصم")
{
    public string Name(string lang) => lang switch { "ar" => NameAr, "ur" => NameUr, "tr" => NameTr, _ => NameEn };
}

public sealed record AmbientSoundInfo(string Id, string FileName, string TitleKey, string IconKey)
{
    public Uri Url => new($"https://cdn.jsdelivr.net/gh/M-H-AFIFI/Siraj-assets@main/ambient/{FileName}");
}

public static class ListeningLibrary
{
    public static readonly IReadOnlyList<Reciter> ExtraReciters =
    [
        Hafs("ar.alaaaql", "علاء عقل", "Alaa Aql", "علاء عقل", "Alâ Akl"),
        Other("ar.husarywarsh", "الحصري (ورش)", "Al-Husary (Warsh)", "الحصری (ورش)", "el-Husari (Verş)", "ورش عن نافع"),
        Other("ar.husaryqalun", "الحصري (قالون)", "Al-Husary (Qalun)", "الحصری (قالون)", "el-Husari (Kalun)", "قالون عن نافع"),
        Other("ar.abdulbasitwarsh", "عبد الباسط (ورش)", "Abdul Basit (Warsh)", "عبد الباسط (ورش)", "Abdülbasit (Verş)", "ورش عن نافع"),
        Other("ar.hudhaifyqalun", "علي الحذيفي (قالون)", "Ali Al-Huthaify (Qalun)", "علی الحذیفی (قالون)", "Ali el-Huzeyfi (Kalun)", "قالون عن نافع"),
        Hafs("ar.idreesabkar", "إدريس أبكر", "Idris Abkar", "ادریس ابکر", "İdris Ebker"),
        Hafs("ar.muhaisni", "محمد المحيسني", "Muhammad al-Muhaisni", "محمد المحیسنی", "Muhammed el-Muhaysini"),
        Hafs("ar.faresabbad", "فارس عباد", "Fares Abbad", "فارس عباد", "Faris Abbad"),
        Hafs("ar.juhany", "عبد الله الجهني", "Abdullah al-Juhany", "عبد اللہ الجہنی", "Abdullah el-Cüheni"),
        Hafs("ar.salahbukhatir", "صلاح بو خاطر", "Salah Bukhatir", "صلاح بو خاطر", "Salah Buhatır"),
        Hafs("ar.thubaity", "عبد الباري الثبيتي", "Abdul Bari ath-Thubaity", "عبد الباری الثبیتی", "Abdülbari es-Sübeyti"),
        Hafs("ar.budair", "صلاح البدير", "Salah al-Budair", "صلاح البدیر", "Salah el-Budeyr"),
        Hafs("ar.khalidqahtani", "خالد القحطاني", "Khalid al-Qahtani", "خالد القحطانی", "Halid el-Kahtani"),
        Hafs("ar.mansoursalimi", "منصور السالمي", "Mansour as-Salimi", "منصور السالمی", "Mansur es-Salimi"),
        Hafs("ar.raadkurdi", "رعد محمد الكردي", "Raad Muhammad al-Kurdi", "رعد محمد الکردی", "Rad Muhammed el-Kürdi"),
        Hafs("ar.kalbani", "عادل الكلباني", "Adel al-Kalbani", "عادل الکلبانی", "Adil el-Kelbani"),
        Hafs("ar.abdulazizahmad", "عبد العزيز الأحمد", "Abdulaziz al-Ahmad", "عبد العزیز الاحمد", "Abdülaziz el-Ahmed"),
        Hafs("ar.hawashi", "أحمد الحواشي", "Ahmad al-Hawashi", "احمد الحواشی", "Ahmed el-Havaşi"),
        Hafs("ar.wadeeyamani", "وديع اليمني", "Wadee al-Yamani", "ودیع الیمنی", "Vedi el-Yemeni"),
        Hafs("ar.oosi", "عبد الرحمن العوسي", "Abdulrahman al-Ossi", "عبد الرحمن العوسی", "Abdurrahman el-Avsi"),
        Hafs("ar.yasserqurashi", "ياسر القرشي", "Yasser al-Qurashi", "یاسر القرشی", "Yasir el-Kureşi"),
        Hafs("ar.abdulmohsenqasim", "عبد المحسن القاسم", "Abdulmohsen al-Qasim", "عبد المحسن القاسم", "Abdülmuhsin el-Kasım"),
        Hafs("ar.khayyat", "عبد الله خياط", "Abdullah Khayyat", "عبد اللہ خیاط", "Abdullah Hayyat"),
        Hafs("ar.obaikan", "عبد المحسن العبيكان", "Abdulmohsen al-Obaikan", "عبد المحسن العبیکان", "Abdülmuhsin el-Ubeykan"),
        Hafs("ar.abdulazizzahrani", "عبد العزيز الزهراني", "Abdulaziz az-Zahrani", "عبد العزیز الزہرانی", "Abdülaziz ez-Zehrani"),
        Hafs("ar.alaqmi", "أكرم العلاقمي", "Akram al-Alaqmi", "اکرم العلاقمی", "Ekrem el-Alakmi"),
        Hafs("ar.abdulrahmanmajed", "عبد الرحمن الماجد", "Abdulrahman al-Majed", "عبد الرحمن الماجد", "Abdurrahman el-Mâcid"),
        Hafs("ar.ibrahimdosari", "إبراهيم الدوسري", "Ibrahim ad-Dosari", "ابراہیم الدوسری", "İbrahim ed-Devseri"),
        Hafs("ar.hatemwaer", "حاتم فريد الواعر", "Hatem Fareed al-Waer", "حاتم فرید الواعر", "Hatim Ferid el-Vair"),
        Hafs("ar.rashadshareef", "محمد رشاد الشريف", "Muhammad Rashad ash-Shareef", "محمد رشاد الشریف", "Muhammed Reşad eş-Şerif"),
        Hafs("ar.daghistani", "زكي داغستاني", "Zaki Daghistani", "زکی داغستانی", "Zeki Dağıstani"),
        Hafs("ar.habdan", "صالح الهبدان", "Saleh al-Habdan", "صالح الہبدان", "Salih el-Habdan"),
        Hafs("ar.sayegh", "توفيق الصايغ", "Tawfeeq as-Sayegh", "توفیق الصایغ", "Tevfik es-Sayığ"),
        Hafs("ar.saltani", "مفتاح السلطني", "Muftah as-Saltani", "مفتاح السلطنی", "Miftah es-Saltani"),
        Hafs("ar.yassersalama", "ياسر سلامة", "Yasser Salama", "یاسر سلامہ", "Yasir Selame"),
        Hafs("ar.nassermajed", "ناصر الماجد", "Nasser al-Majed", "ناصر الماجد", "Nasır el-Mâcid"),
        Hafs("ar.arkani", "عبد الولي الأركاني", "Abdulwali al-Arkani", "عبد الولی الارکانی", "Abdülveli el-Erkani"),
        Hafs("ar.abdulwadoodhaneef", "عبد الودود حنيف", "Abdulwadood Haneef", "عبد الودود حنیف", "Abdülvedud Hanif"),
        Hafs("ar.shoaey", "يوسف الشويعي", "Yousef ash-Shoaey", "یوسف الشویعی", "Yusuf eş-Şuveyi"),
        Hafs("ar.majedzamil", "ماجد الزامل", "Majed az-Zamil", "ماجد الزامل", "Mâcid ez-Zâmil"),
        Hafs("ar.kandari", "عبد الله الكندري", "Abdullah al-Kandari", "عبد اللہ الکندری", "Abdullah el-Kenderi"),
    ];

    public static IReadOnlyList<Reciter> AllReciters { get; } = [.. Reciters.All, .. ExtraReciters];

    public static Reciter FindReciter(string? id) => AllReciters.FirstOrDefault(r => r.Id == id) ?? AllReciters[0];

    public static readonly IReadOnlyList<RareCollection> RareCollections =
    [
        new("abdul-azim-zaher",
            "الشيخ عبد العظيم زاهر", "Sheikh Abdul Azim Zaher", "Şeyh Abdülazim Zahir", "شیخ عبد العظیم زاہر",
            "Portraits/reciter_abdul_azim_zaher.png",
            [
                new("abdul-azim-zaher", [16], false, "audio/recitations/abdul-azim-zaher/016-an-nahl.mp3", 1486),
                new("abdul-azim-zaher", [19], false, "audio/recitations/abdul-azim-zaher/019-maryam.mp3", 811),
                new("abdul-azim-zaher", [20], false, "audio/recitations/abdul-azim-zaher/020-taha.mp3", 596),
                new("abdul-azim-zaher", [24], false, "audio/recitations/abdul-azim-zaher/024-an-nur.mp3", 1728),
                new("abdul-azim-zaher", [27], false, "audio/recitations/abdul-azim-zaher/027-an-naml.mp3", 336),
                new("abdul-azim-zaher", [28], false, "audio/recitations/abdul-azim-zaher/028-al-qasas.mp3", 322),
                new("abdul-azim-zaher", [47], false, "audio/recitations/abdul-azim-zaher/047-muhammad.mp3", 309),
                new("abdul-azim-zaher", [48], false, "audio/recitations/abdul-azim-zaher/048-al-fath.mp3", 888),
                new("abdul-azim-zaher", [54], false, "audio/recitations/abdul-azim-zaher/054-al-qamar.mp3", 307),
                new("abdul-azim-zaher", [54, 55], false, "audio/recitations/abdul-azim-zaher/054-055-al-qamar-ar-rahman.mp3", 673),
                new("abdul-azim-zaher", [58], false, "audio/recitations/abdul-azim-zaher/058-al-mujadila.mp3", 320),
                new("abdul-azim-zaher", [75], true, "audio/recitations/abdul-azim-zaher/075-al-qiyamah.mp3", 591),
                new("abdul-azim-zaher", [86], true, "audio/recitations/abdul-azim-zaher/086-at-tariq.mp3", 200),
            ]),
    ];

    public static readonly IReadOnlyList<AmbientSoundInfo> AmbientSounds =
    [
        new("RAIN", "rain.m4a", "ambient_rain", "ambient_window"),
        new("WAVES", "waves.m4a", "ambient_waves", "ambient_fountain"),
        new("STREAM", "stream.m4a", "ambient_stream", "ambient_fountain"),
        new("BIRDS", "birds.m4a", "ambient_birds", "ambient_forest"),
        new("NIGHT", "night.m4a", "ambient_night", "crescent_star"),
        new("WIND", "wind.m4a", "ambient_wind", "ambient_forest"),
        new("THUNDER", "thunder.m4a", "ambient_thunder", "ambient_thunder"),
        new("FIRE", "fire.m4a", "ambient_fire", "ambient_fire"),
        new("FOREST", "forest.m4a", "ambient_forest", "ambient_forest"),
        new("FOUNTAIN", "fountain.m4a", "ambient_fountain", "ambient_fountain"),
        new("TENT", "tent.m4a", "ambient_tent", "ambient_tent"),
        new("WINDOW", "window.m4a", "ambient_window", "ambient_window"),
    ];

    public static readonly IReadOnlyDictionary<string, string> PortraitFiles = new Dictionary<string, string>
    {
        ["ar.alafasy"] = "reciter_alafasy.jpg",
        ["ar.abdulbasitmurattal"] = "reciter_abdulbasit.jpg",
        ["ar.abdulbasitmujawwad"] = "reciter_abdulbasit.jpg",
        ["ar.abdulbasitwarsh"] = "reciter_abdulbasit.jpg",
        ["ar.husary"] = "reciter_husary.jpg",
        ["ar.husarymujawwad"] = "reciter_husary.jpg",
        ["ar.husarywarsh"] = "reciter_husary.jpg",
        ["ar.husaryqalun"] = "reciter_husary.jpg",
        ["ar.minshawi"] = "reciter_minshawi.jpg",
        ["ar.minshawikids"] = "reciter_minshawi.jpg",
        ["ar.abdurrahmaansudais"] = "reciter_sudais.jpg",
        ["ar.shuraym"] = "reciter_shuraym.jpg",
        ["ar.mahermuaiqly"] = "reciter_maher.jpg",
        ["ar.saadghamidi"] = "reciter_ghamidi.jpg",
        ["ar.yasseraldossari"] = "reciter_dossari.jpg",
        ["ar.abubakralshatri"] = "reciter_shatri.jpg",
        ["ar.ahmedalnufeis"] = "reciter_nufeis.jpg",
        ["ar.bandarbaleela"] = "reciter_baleela.jpg",
        ["ar.hanirifai"] = "reciter_rifai.jpg",
        ["ar.khalidaljalil"] = "reciter_jalil.jpg",
        ["ar.abdullahbasfar"] = "reciter_basfar.jpg",
        ["ar.abdullahmatroud"] = "reciter_matroud.jpg",
        ["ar.alihajjajalsouasi"] = "reciter_souasi.jpg",
        ["ar.azizalili"] = "reciter_alili.jpg",
        ["ar.mahmoodalibana"] = "reciter_bana.jpg",
        ["ar.tablawi"] = "reciter_tablawi.jpg",
        ["ar.mostafaismaeel"] = "reciter_ismaeel.jpg",
        ["ar.muhammadjibreel"] = "reciter_jibreel.jpg",
        ["ar.qatami"] = "reciter_qatami.jpg",
        ["ar.sahlyasin"] = "reciter_yasin.jpg",
        ["ar.alijaber"] = "reciter_alijaber.jpg",
        ["ar.hudhaify"] = "reciter_hudhaify.jpg",
        ["ar.hudhaifyqalun"] = "reciter_hudhaify.jpg",
        ["ar.ahmedajamy"] = "reciter_ajamy.jpg",
        ["ar.abdurrashidsufi"] = "reciter_sufi.jpg",
        ["ar.muhammadayyoub"] = "reciter_ayyoub.jpg",
        ["ar.muhammadalluhaidan"] = "reciter_luhaidan.jpg",
        ["ar.abdallahkamel"] = "reciter_kamel.jpg",
        ["ar.mansourzahrani"] = "reciter_mansour_zahrani.jpg",
        ["ar.naqshbandi"] = "reciter_naqshbandi.jpg",
        ["ar.alaaaql"] = "reciter_alaa_aql.jpg",
        ["ar.idreesabkar"] = "reciter_idrees_abkar.jpg",
        ["ar.muhaisni"] = "reciter_muhaisni.jpg",
        ["ar.faresabbad"] = "reciter_fares_abbad.jpg",
        ["ar.juhany"] = "reciter_juhany.jpg",
        ["ar.salahbukhatir"] = "reciter_salah_bukhatir.jpg",
        ["ar.thubaity"] = "reciter_thubaity.jpg",
        ["ar.budair"] = "reciter_budair.jpg",
        ["ar.khalidqahtani"] = "reciter_khalid_qahtani.jpg",
        ["ar.raadkurdi"] = "reciter_raad_kurdi.jpg",
        ["ar.kalbani"] = "reciter_kalbani.jpg",
        ["ar.abdulazizahmad"] = "reciter_abdulaziz_ahmad.jpg",
        ["ar.hawashi"] = "reciter_hawashi.jpg",
        ["ar.wadeeyamani"] = "reciter_wadee_yamani.jpg",
        ["ar.oosi"] = "reciter_oosi.jpg",
        ["ar.yasserqurashi"] = "reciter_yasser_qurashi.jpg",
        ["ar.abdulmohsenqasim"] = "reciter_abdulmohsen_qasim.jpg",
        ["ar.khayyat"] = "reciter_khayyat.jpg",
        ["ar.abdulazizzahrani"] = "reciter_abdulaziz_zahrani.jpg",
        ["ar.alaqmi"] = "reciter_alaqmi.jpg",
        ["ar.ibrahimdosari"] = "reciter_ibrahim_dosari.jpg",
        ["ar.hatemwaer"] = "reciter_hatem_waer.jpg",
        ["ar.rashadshareef"] = "reciter_rashad_shareef.jpg",
        ["ar.daghistani"] = "reciter_daghistani.jpg",
        ["ar.sayegh"] = "reciter_sayegh.jpg",
        ["ar.yassersalama"] = "reciter_yasser_salama.jpg",
        ["ar.arkani"] = "reciter_arkani.jpg",
        ["ar.abdulwadoodhaneef"] = "reciter_abdulwadood_haneef.jpg",
        ["ar.shoaey"] = "reciter_shoaey.jpg",
        ["ar.kandari"] = "reciter_kandari.jpg",
        ["ar.obaikan"] = "reciter_obaikan.jpg",
        ["ar.habdan"] = "reciter_habdan.jpg",
        ["ar.mansoursalimi"] = "reciter_mansour_salimi.jpg",
        ["ar.majedzamil"] = "reciter_majed_zamil.jpg",
        ["ar.saltani"] = "reciter_saltani.jpg",
        ["ar.nassermajed"] = "reciter_nasser_majed.jpg",
        ["ar.abdulrahmanmajed"] = "reciter_abdulrahman_majed.jpg",
    };

    public static string? PortraitForAdhan(string voiceId) => voiceId switch
    {
        "Makkah" => "img_makkah.jpg",
        "Madinah" => "img_madinah.jpg",
        "Alaqsa" => "img_alaqsa.jpg",
        "Alafasy" => "reciter_alafasy.jpg",
        "AbdulBasit" => "reciter_abdulbasit.jpg",
        "Minshawi" => "reciter_minshawi.jpg",
        "Naghshbandi" => "reciter_naqshbandi.jpg",
        "AlNafees" => "reciter_nufeis.jpg",
        "Qatami" => "reciter_qatami.jpg",
        "MansourAlZahrani" => "reciter_mansour_zahrani.jpg",
        _ => null,
    };

    private static Reciter Hafs(string id, string ar, string en, string ur, string tr) =>
        new(id, ar, en, ur, tr, "Murattal", "مرتل", "مرتل", "Murattal",
            ReciterMode.SurahSegments, null, Reciters.SurahServers.GetValueOrDefault(id), "حفص عن عاصم");

    private static Reciter Other(string id, string ar, string en, string ur, string tr, string riwayah) =>
        new(id, ar, en, ur, tr, "Murattal", "مرتل", "مرتل", "Murattal",
            ReciterMode.SurahSegments, null, Reciters.SurahServers.GetValueOrDefault(id), riwayah);
}
