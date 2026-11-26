namespace Siraj.Core.Quran;

public sealed record DailyVerse(
    int Surah, int Ayah, int Page,
    string SurahAr, string SurahEn,
    string Arabic, string En, string Tr, string Ur)
{
    public string Translation(string language) => language switch
    {
        "ar" => "",
        "tr" => Tr,
        "ur" => Ur,
        _ => En,
    };

    public string SurahName(string language) => language is "ar" or "ur" ? SurahAr : SurahEn;
}

public static class DailyAyah
{
    public static readonly IReadOnlyList<DailyVerse> Pool =
    [
        new(13, 28, 252, "الرعد", "Ar-Ra'd",
            "أَلَا بِذِكْرِ اللَّهِ تَطْمَئِنُّ الْقُلُوبُ",
            "Unquestionably, by the remembrance of Allah hearts are assured.",
            "Bilesiniz ki, kalpler ancak Allah'ı anmakla huzur bulur.",
            "جان لو کہ اللہ ہی کے ذکر سے دلوں کو اطمینان نصیب ہوتا ہے۔"),
        new(94, 5, 596, "الشرح", "Ash-Sharh",
            "فَإِنَّ مَعَ الْعُسْرِ يُسْرًا ۝ إِنَّ مَعَ الْعُسْرِ يُسْرًا",
            "For indeed, with hardship will be ease. Indeed, with hardship will be ease.",
            "Şüphesiz güçlükle beraber bir kolaylık vardır. Gerçekten güçlükle beraber bir kolaylık vardır.",
            "پس بے شک مشکل کے ساتھ آسانی ہے۔ بے شک مشکل کے ساتھ آسانی ہے۔"),
        new(2, 186, 28, "البقرة", "Al-Baqarah",
            "وَإِذَا سَأَلَكَ عِبَادِي عَنِّي فَإِنِّي قَرِيبٌ أُجِيبُ دَعْوَةَ الدَّاعِ إِذَا دَعَانِ",
            "And when My servants ask you concerning Me, indeed I am near. I respond to the invocation of the supplicant when he calls upon Me.",
            "Kullarım sana beni sorduğunda, ben çok yakınım. Bana dua ettiği vakit dua edenin dileğine karşılık veririm.",
            "اور جب میرے بندے آپ سے میرے متعلق پوچھیں تو میں قریب ہوں، دعا کرنے والے کی دعا قبول کرتا ہوں جب وہ مجھے پکارے۔"),
        new(2, 286, 49, "البقرة", "Al-Baqarah",
            "لَا يُكَلِّفُ اللَّهُ نَفْسًا إِلَّا وُسْعَهَا",
            "Allah does not charge a soul except with that within its capacity.",
            "Allah hiç kimseye gücünün yeteceğinden fazlasını yüklemez.",
            "اللہ کسی جان پر اس کی طاقت سے زیادہ بوجھ نہیں ڈالتا۔"),
        new(39, 53, 464, "الزمر", "Az-Zumar",
            "قُلْ يَا عِبَادِيَ الَّذِينَ أَسْرَفُوا عَلَىٰ أَنفُسِهِمْ لَا تَقْنَطُوا مِن رَّحْمَةِ اللَّهِ إِنَّ اللَّهَ يَغْفِرُ الذُّنُوبَ جَمِيعًا",
            "Say, O My servants who have transgressed against themselves, do not despair of the mercy of Allah. Indeed, Allah forgives all sins.",
            "De ki: Ey kendilerinin aleyhine aşırı giden kullarım! Allah'ın rahmetinden ümidinizi kesmeyin. Şüphesiz Allah bütün günahları bağışlar.",
            "کہہ دیجیے کہ اے میرے بندو جنہوں نے اپنی جانوں پر زیادتی کی ہے، اللہ کی رحمت سے ناامید نہ ہو، بے شک اللہ سارے گناہ بخش دیتا ہے۔"),
        new(65, 3, 558, "الطلاق", "At-Talaq",
            "وَمَن يَتَوَكَّلْ عَلَى اللَّهِ فَهُوَ حَسْبُهُ",
            "And whoever relies upon Allah, then He is sufficient for him.",
            "Kim Allah'a tevekkül ederse, O kendisine yeter.",
            "اور جو کوئی اللہ پر بھروسہ کرے تو وہ اس کے لیے کافی ہے۔"),
        new(3, 139, 67, "آل عمران", "Aal-E-Imran",
            "وَلَا تَهِنُوا وَلَا تَحْزَنُوا وَأَنتُمُ الْأَعْلَوْنَ إِن كُنتُم مُّؤْمِنِينَ",
            "So do not weaken and do not grieve, and you will be superior if you are true believers.",
            "Gevşemeyin, hüzünlenmeyin. Eğer gerçekten iman etmiş kimseler iseniz üstün olan sizlersiniz.",
            "اور نہ کمزوری دکھاؤ اور نہ غم کرو، اور تم ہی غالب رہو گے اگر تم مومن ہو۔"),
        new(29, 69, 404, "العنكبوت", "Al-'Ankabut",
            "وَالَّذِينَ جَاهَدُوا فِينَا لَنَهْدِيَنَّهُمْ سُبُلَنَا وَإِنَّ اللَّهَ لَمَعَ الْمُحْسِنِينَ",
            "And those who strive for Us, We will surely guide them to Our ways. And indeed, Allah is with the doers of good.",
            "Bizim uğrumuzda cihad edenlere gelince, elbette onları bize ulaşan yollara iletiriz. Şüphesiz Allah iyilik edenlerle beraberdir.",
            "اور جو لوگ ہماری راہ میں کوشش کرتے ہیں ہم ضرور انہیں اپنے راستے دکھائیں گے، اور بے شک اللہ نیکوکاروں کے ساتھ ہے۔"),
        new(2, 152, 23, "البقرة", "Al-Baqarah",
            "فَاذْكُرُونِي أَذْكُرْكُمْ وَاشْكُرُوا لِي وَلَا تَكْفُرُونِ",
            "So remember Me; I will remember you. And be grateful to Me and do not deny Me.",
            "Öyleyse siz beni anın ki ben de sizi anayım. Bana şükredin, sakın nankörlük etmeyin.",
            "پس تم مجھے یاد کرو میں تمہیں یاد کروں گا، اور میرا شکر ادا کرو اور ناشکری نہ کرو۔"),
        new(14, 7, 256, "إبراهيم", "Ibrahim",
            "لَئِن شَكَرْتُمْ لَأَزِيدَنَّكُمْ",
            "If you are grateful, I will surely increase you in favor.",
            "Eğer şükrederseniz, elbette size nimetimi artırırım.",
            "اگر تم شکر کرو گے تو میں تمہیں اور زیادہ عطا کروں گا۔"),
        new(2, 153, 23, "البقرة", "Al-Baqarah",
            "يَا أَيُّهَا الَّذِينَ آمَنُوا اسْتَعِينُوا بِالصَّبْرِ وَالصَّلَاةِ إِنَّ اللَّهَ مَعَ الصَّابِرِينَ",
            "O you who have believed, seek help through patience and prayer. Indeed, Allah is with the patient.",
            "Ey iman edenler! Sabır ve namaz ile Allah'tan yardım isteyin. Çünkü Allah sabredenlerle beraberdir.",
            "اے ایمان والو! صبر اور نماز کے ذریعے مدد طلب کرو، بے شک اللہ صبر کرنے والوں کے ساتھ ہے۔"),
        new(93, 3, 596, "الضحى", "Ad-Duhaa",
            "مَا وَدَّعَكَ رَبُّكَ وَمَا قَلَىٰ ۝ وَلَلْآخِرَةُ خَيْرٌ لَّكَ مِنَ الْأُولَىٰ",
            "Your Lord has not taken leave of you, nor has He detested you. And the Hereafter is better for you than the first life.",
            "Rabbin seni terk etmedi ve sana darılmadı. Şüphesiz senin için son olan, ilk olandan daha hayırlıdır.",
            "آپ کے رب نے آپ کو نہیں چھوڑا اور نہ وہ ناراض ہوا ہے۔ اور یقیناً آخرت آپ کے لیے دنیا سے بہتر ہے۔"),
        new(40, 60, 474, "غافر", "Ghafir",
            "وَقَالَ رَبُّكُمُ ادْعُونِي أَسْتَجِبْ لَكُمْ",
            "And your Lord says, Call upon Me; I will respond to you.",
            "Rabbiniz şöyle buyurdu: Bana dua edin, duanıza cevap vereyim.",
            "اور تمہارے رب نے فرمایا: مجھے پکارو، میں تمہاری دعا قبول کروں گا۔"),
        new(16, 97, 278, "النحل", "An-Nahl",
            "مَنْ عَمِلَ صَالِحًا مِّن ذَكَرٍ أَوْ أُنثَىٰ وَهُوَ مُؤْمِنٌ فَلَنُحْيِيَنَّهُ حَيَاةً طَيِّبَةً",
            "Whoever does righteousness, whether male or female, while a believer, We will surely cause them to live a good life.",
            "Erkek veya kadın, kim mümin olarak salih amel işlerse, ona mutlaka güzel bir hayat yaşatırız.",
            "جو کوئی نیک عمل کرے، مرد ہو یا عورت، بشرطیکہ وہ مومن ہو، تو ہم اسے ضرور پاکیزہ زندگی عطا کریں گے۔"),
    ];

    public static DailyVerse ForDay(DateOnly day)
    {
        int index = ((day.DayNumber % Pool.Count) + Pool.Count) % Pool.Count;
        return Pool[index];
    }
}
