namespace Siraj.Core.Quran;

public static partial class Reciters
{
    public static readonly IReadOnlyList<Reciter> All =
    [
        new("ar.alafasy", "مشاري راشد العفاسي", "Mishary Rashid Alafasy", "مشاری راشد العفاسی", "Mişari Raşid el-Afasi", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Alafasy_128kbps", "https://audio-cdn.tarteel.ai/quran/surah/alafasy/murattal/mp3/", "حفص عن عاصم"),
        new("ar.abdulbasitmurattal", "عبد الباسط عبد الصمد", "Abdul Basit Abdul Samad", "عبد الباسط عبد الصمد", "Abdülbasit Abdüssamed", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Abdul_Basit_Murattal_192kbps", "https://audio-cdn.tarteel.ai/quran/surah/abdulBasit/murattal/mp3/", "حفص عن عاصم"),
        new("ar.abdulbasitmujawwad", "عبد الباسط (مجود)", "Abdul Basit Mujawwad", "عبد الباسط (تجوید)", "Abdülbasit Abdüssamed (Mücevved)", "Mujawwad", "مجود", "تجوید", "Mücevved", ReciterMode.AyahByAyah, "Abdul_Basit_Mujawwad_128kbps", "https://server7.mp3quran.net/basit/Almusshaf-Al-Mojawwad/", "حفص عن عاصم"),
        new("ar.husary", "محمود خليل الحصري", "Mahmoud Khalil Al-Husary", "محمود خلیل الحصری", "Mahmud Halil el-Husari", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Husary_128kbps", "https://download.quranicaudio.com/quran/mahmood_khaleel_al-husaree/", "حفص عن عاصم"),
        new("ar.husarymujawwad", "الحصري (مجود)", "Mahmoud Al-Husary Mujawwad", "الحصری (تجوید)", "Mahmud Halil el-Husari (Mücevved)", "Mujawwad", "مجود", "تجوید", "Mücevved", ReciterMode.AyahByAyah, "Husary_Mujawwad_64kbps", "https://audio-cdn.tarteel.ai/quran/surah/husary/mujawwad/mp3/", "حفص عن عاصم"),
        new("ar.minshawi", "محمد صديق المنشاوي", "Muhammad Siddiq Al-Minshawi", "محمد صدیق المنشاوی", "Muhammed Sıddık el-Minşavi", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Minshawy_Murattal_128kbps", "https://download.quranicaudio.com/quran/muhammad_siddeeq_al-minshaawee/", "حفص عن عاصم"),
        new("ar.minshawikids", "المنشاوي مع الأطفال", "Minshawi with Kids", "المنشاوی (مع اطفال)", "Muhammed Sıddık el-Minşavi (Çocuklarla)", "Teacher / With Kids", "المصحف المعلم", "تعلیمی مصحف", "Öğretici / Çocuklarla", ReciterMode.AyahByAyah, "Minshawy_Teacher_128kbps", "https://download.quranicaudio.com/qdc/siddiq_minshawi/kids_repeat/", "معلم / حفص"),
        new("ar.abdurrahmaansudais", "عبد الرحمن السديس", "Abdur-Rahman as-Sudais", "عبد الرحمن السدیس", "Abdurrahman es-Sudeys", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Abdurrahmaan_As-Sudais_192kbps", "https://download.quranicaudio.com/quran/abdurrahmaan_as-sudays/", "حفص عن عاصم"),
        new("ar.shuraym", "سعود الشريم", "Saud Al-Shuraim", "سعود الشریم", "Suud eş-Şuraym", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Saood_ash-Shuraym_128kbps", "https://download.quranicaudio.com/quran/sa3ood_al-shuraym/", "حفص عن عاصم"),
        new("ar.mahermuaiqly", "ماهر المعيقلي", "Maher Al-Mu'aiqly", "ماہر المعیقلی", "Mahir el-Muaykili", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Maher_AlMuaiqly_64kbps", "https://download.quranicaudio.com/quran/maher_almu3aiqly/year1440/", "حفص عن عاصم"),
        new("ar.saadghamidi", "سعد الغامدي", "Saad al Ghamdi", "سعد الغامدی", "Saad el-Gamidi", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Ghamadi_40kbps", "https://download.quranicaudio.com/quran/sa3d_al-ghaamidi/complete/", "حفص عن عاصم"),
        new("ar.yasseraldossari", "ياسر الدوسري", "Yasser ad-Dussary", "یاسر الدوسری", "Yaser ed-Devseri", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Yasser_Ad-Dussary_128kbps", "https://audio-cdn.tarteel.ai/quran/surah/yasserAlDosari/murattal/mp3/", "حفص عن عاصم"),
        new("ar.abubakralshatri", "أبو بكر الشاطري", "Abu Bakr al-Shatri", "ابو بکر الشاطری", "Ebu Bekir eş-Şatri", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Abu_Bakr_Ash-Shaatree_128kbps", "https://download.quranicaudio.com/quran/abu_bakr_ash-shaatree/", "حفص عن عاصم"),
        new("ar.ahmedalnufeis", "أحمد النفيس", "Ahmed Alnufais", "احمد النفیس", "Ahmed en-Nufeyis", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.SurahSegments, null, "https://audio-cdn.tarteel.ai/quran/surah/alnufais/murattal/mp3/", "حفص عن عاصم"),
        new("ar.bandarbaleela", "بندر بليلة", "Bandar Baleela", "بندر بلیلۃ", "Bender Belile", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Bandar_Balila_64kbps", "https://download.quranicaudio.com/quran/bandar_baleela/complete/", "حفص عن عاصم"),
        new("ar.hanirifai", "هاني الرفاعي", "Hani ar-Rifai", "ہانی الرفاعی", "Hani er-Rifai", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Hani_Rifai_192kbps", "https://server8.mp3quran.net/hani/", "حفص عن عاصم"),
        new("ar.khalidaljalil", "خالد الجليل", "Khalid Al-Jalil", "خالد الجلیل", "Halid el-Celil", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.SurahSegments, null, "https://download.quranicaudio.com/qdc/khalid_jalil/murattal/mp3/", "حفص عن عاصم"),
        new("ar.abdullahbasfar", "عبد الله بصفر", "Abdullah Basfar", "عبد اللہ بصفر", "Abdullah Basfar", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Abdullah_Basfar_192kbps", "https://download.quranicaudio.com/quran/abdullaah_basfar/", "حفص عن عاصم"),
        new("ar.abdullahmatroud", "عبد الله مطرود", "Abdullah Matroud", "عبد اللہ مطرود", "Abdullah Matrud", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Abdullah_Matroud_128kbps", "https://download.quranicaudio.com/quran/abdullah_matroud/", "حفص عن عاصم"),
        new("ar.alihajjajalsouasi", "علي حجاج السويسي", "Ali Hajjaj Alsouasi", "علی حجاج السویسی", "Ali Haccac es-Suveysi", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Ali_Hajjaj_AlSuesy_128kbps", "https://download.quranicaudio.com/quran/ali_hajjaj_alsouasi/", "حفص عن عاصم"),
        new("ar.azizalili", "عزيز عليلي", "Aziz Alili", "عزیز علیلی", "Aziz Alili", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "aziz_alili_128kbps", "https://download.quranicaudio.com/quran/aziz_alili/", "حفص عن عاصم"),
        new("ar.mahmoodalibana", "محمود علي البنا", "Mahmood Ali Al-Bana", "محمود علی البنا", "Mahmud Ali el-Benna", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "mahmoud_ali_al_banna_32kbps", "https://download.quranicaudio.com/quran/mahmood_ali_albana/", "حفص عن عاصم"),
        new("ar.tablawi", "محمد الطبلاوي", "Mohamed al-Tablawi", "محمد الطبلاوی", "Muhammed Mahmud et-Tablavi", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Mohammad_al_Tablaway_128kbps", "https://server12.mp3quran.net/tblawi/", "حفص عن عاصم"),
        new("ar.mostafaismaeel", "مصطفى إسماعيل", "Mostafa Ismaeel", "مصطفیٰ اسماعیل", "Mustafa İsmail", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Mustafa_Ismail_48kbps", "https://download.quranicaudio.com/quran/mostafa_ismaeel/", "حفص عن عاصم"),
        new("ar.muhammadjibreel", "محمد جبريل", "Muhammad Jibreel", "محمد جبریل", "Muhammed Cibril", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Muhammad_Jibreel_128kbps", "https://download.quranicaudio.com/quran/muhammad_jibreel/complete/", "حفص عن عاصم"),
        new("ar.qatami", "ناصر القطامي", "Nasser Al Qatami", "ناصر القطامی", "Nasır el-Kıtami", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Nasser_Alqatami_128kbps", "https://download.quranicaudio.com/quran/nasser_bin_ali_alqatami/", "حفص عن عاصم"),
        new("ar.sahlyasin", "سهل ياسين", "Sahl Yasin", "سہل یاسین", "Sehl Yasin", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Sahl_Yassin_128kbps", "https://download.quranicaudio.com/quran/sahl_yaaseen/", "حفص عن عاصم"),
        new("ar.alijaber", "علي جابر", "Ali Jaber", "علی جابر", "Ali Cabir", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Ali_Jaber_64kbps", "https://server11.mp3quran.net/a_jbr/", "حفص عن عاصم"),
        new("ar.hudhaify", "علي الحذيفي", "Ali Al-Huthaify", "علی الحذیفی", "Ali Abdurrahman el-Huzeyfi", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Hudhaify_128kbps", "https://download.quranicaudio.com/quran/huthayfi/", "حفص عن عاصم"),
        new("ar.ahmedajamy", "أحمد العجمي", "Ahmed Al-Ajmi", "احمد العجمی", "Ahmed el-Acemi", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "ahmed_ibn_ali_al_ajamy_128kbps", "https://download.quranicaudio.com/quran/ahmed_ibn_3ali_al-3ajamy/", "حفص عن عاصم"),
        new("ar.abdurrashidsufi", "عبد الرشيد صوفي", "Abdul Rashid Ali Sufi", "عبد الرشید صوفی", "Abdürreşid Sufi", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.SurahSegments, null, "https://server16.mp3quran.net/soufi/Rewayat-Hafs-A-n-Assem/", "حفص / السوسي"),
        new("ar.muhammadayyoub", "محمد أيوب", "Muhammad Ayyub", "محمد ایوب", "Muhammed Eyyub", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.AyahByAyah, "Muhammad_Ayyoub_128kbps", "https://download.quranicaudio.com/quran/muhammad_ayyoob/", "حفص عن عاصم"),
        new("ar.muhammadalluhaidan", "محمد اللحيدان", "Muhammad Al-Luhaidan", "محمد اللحیدان", "Muhammed el-Luhaydan", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.SurahSegments, null, "https://server8.mp3quran.net/lhdan/", "حفص عن عاصم"),
        new("ar.abdallahkamel", "عبد الله كامل", "Abdallah Kamel", "عبد اللہ کامل", "Abdullah Kamil", "Murattal", "مرتل", "مرتل", "Murattal", ReciterMode.SurahSegments, null, "https://server16.mp3quran.net/kamel/Rewayat-Hafs-A-n-Assem/", "حفص عن عاصم"),
    ];

    public static readonly IReadOnlyDictionary<string, string> SurahServers = BuildSurahServers();

    private static Dictionary<string, string> BuildSurahServers()
    {
        var servers = new Dictionary<string, string>
        {
            ["ar.alaaaql"] = "https://media.way2quran.com/alaa-aql/hafs-an-asim/",
            ["ar.husarywarsh"] = "https://cdn.mp3quran.net/audio/mahmoud-husary/r3/",
            ["ar.husaryqalun"] = "https://cdn.mp3quran.net/audio/mahmoud-husary/r5/",
            ["ar.abdulbasitwarsh"] = "https://cdn.mp3quran.net/audio/abdulbasit-abdulsamad/r2/",
            ["ar.hudhaifyqalun"] = "https://cdn.mp3quran.net/audio/ali-hudhaifi/r2/",
            ["ar.idreesabkar"] = "https://cdn.mp3quran.net/audio/idrees-abkar/r1/",
            ["ar.muhaisni"] = "https://cdn.mp3quran.net/audio/muhammad-muhaisni/r1/",
            ["ar.faresabbad"] = "https://cdn.mp3quran.net/audio/fares-abbad/r1/",
            ["ar.juhany"] = "https://cdn.mp3quran.net/audio/abdullah-juhani/r1/",
            ["ar.salahbukhatir"] = "https://cdn.mp3quran.net/audio/salah-bukhatir/r1/",
            ["ar.thubaity"] = "https://cdn.mp3quran.net/audio/abdulbari-thubaiti/r1/",
            ["ar.budair"] = "https://cdn.mp3quran.net/audio/salah-budair/r1/",
            ["ar.khalidqahtani"] = "https://cdn.mp3quran.net/audio/khalid-qahtani/r1/",
            ["ar.mansoursalimi"] = "https://cdn.mp3quran.net/audio/mansour-salimi/r1/",
            ["ar.raadkurdi"] = "https://cdn.mp3quran.net/audio/raad-kurdi/r1/",
            ["ar.kalbani"] = "https://cdn.mp3quran.net/audio/adel-kalbani/r1/",
            ["ar.abdulazizahmad"] = "https://cdn.mp3quran.net/audio/abdulaziz-ahmad/r1/",
            ["ar.hawashi"] = "https://cdn.mp3quran.net/audio/ahmad-hawashi/r1/",
            ["ar.wadeeyamani"] = "https://cdn.mp3quran.net/audio/wadee-yamani/r1/",
            ["ar.oosi"] = "https://cdn.mp3quran.net/audio/abdulrahman-oosi/r1/",
            ["ar.yasserqurashi"] = "https://cdn.mp3quran.net/audio/yasser-qurashi/r1/",
            ["ar.abdulmohsenqasim"] = "https://cdn.mp3quran.net/audio/abdulmohsen-qasim/r1/",
            ["ar.khayyat"] = "https://cdn.mp3quran.net/audio/abdullah-khayyat/r1/",
            ["ar.obaikan"] = "https://cdn.mp3quran.net/audio/abdulmohsen-obaikan/r1/",
            ["ar.abdulazizzahrani"] = "https://cdn.mp3quran.net/audio/abdulaziz-zahrani/r1/",
            ["ar.alaqmi"] = "https://cdn.mp3quran.net/audio/akram-alaqmi/r1/",
            ["ar.abdulrahmanmajed"] = "https://cdn.mp3quran.net/audio/abdulrahman-majed/r1/",
            ["ar.ibrahimdosari"] = "https://cdn.mp3quran.net/audio/ibrahim-dosari/r2/",
            ["ar.hatemwaer"] = "https://cdn.mp3quran.net/audio/hatem-waer/r1/",
            ["ar.rashadshareef"] = "https://cdn.mp3quran.net/audio/muhammad-shareef/r1/",
            ["ar.daghistani"] = "https://cdn.mp3quran.net/audio/zaki-daghistani/r1/",
            ["ar.habdan"] = "https://cdn.mp3quran.net/audio/saleh-habdan/r1/",
            ["ar.sayegh"] = "https://cdn.mp3quran.net/audio/tawfeeq-sayegh/r1/",
            ["ar.saltani"] = "https://cdn.mp3quran.net/audio/muftah-saltani/r3/",
            ["ar.yassersalama"] = "https://cdn.mp3quran.net/audio/yasser-salama/r1/",
            ["ar.nassermajed"] = "https://cdn.mp3quran.net/audio/nasser-majed/r1/",
            ["ar.arkani"] = "https://cdn.mp3quran.net/audio/abdulwali-arkani/r1/",
            ["ar.abdulwadoodhaneef"] = "https://cdn.mp3quran.net/audio/abdulwadood-haneef/r1/",
            ["ar.shoaey"] = "https://cdn.mp3quran.net/audio/yousef-shoaey/r1/",
            ["ar.majedzamil"] = "https://cdn.mp3quran.net/audio/majed-zamil/r1/",
            ["ar.kandari"] = "https://cdn.mp3quran.net/audio/abdullah-kandari/r1/",
        };
        foreach (var r in All)
        {
            if (r.SurahServer != null) servers[r.Id] = r.SurahServer;
        }
        return servers;
    }
}
