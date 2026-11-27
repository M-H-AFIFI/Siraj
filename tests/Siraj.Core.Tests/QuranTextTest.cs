using Siraj.Core.Quran;
using Xunit;

namespace Siraj.Core.Tests;

public class QuranTextTest
{
    private const char SmallHighSeen = (char)0x06DC;

    [Fact]
    public void ForDisplayWidensWordGapsAndMapsRanges()
    {
        var (text, ranges) = DigitalKhattText.ForDisplay("ab cd ef", [(3, 5), (2, 6), (2, 3)]);
        Assert.Equal("ab   cd   ef", text);
        // Maps "cd", trims space-padded span to "cd", and drops whitespace-only span.
        Assert.Equal([(5, 7), (5, 7)], ranges);
    }

    [Fact]
    public void ForDisplayAttachesPauseSignsToPrecedingWord()
    {
        string stored = $"ab {SmallHighSeen} cd";
        var (text, ranges) = DigitalKhattText.ForDisplay(stored, [(5, 7)]);
        Assert.Equal($"ab{SmallHighSeen}   cd", text);
        Assert.Equal([(6, 8)], ranges);
        Assert.Equal(text, DigitalKhattText.ForDisplay(stored));
    }

    [Fact]
    public void HizbStartPagesAreStrictlyIncreasing()
    {
        Assert.Equal(1, Ajzaa.HizbStartPage(1));
        Assert.Equal(32, Ajzaa.HizbStartPage(4));
        Assert.Equal(591, Ajzaa.HizbStartPage(60));
        for (int hizb = 2; hizb <= 60; hizb++)
            Assert.True(Ajzaa.HizbStartPage(hizb) > Ajzaa.HizbStartPage(hizb - 1), $"hizb {hizb}");
        Assert.Equal("الجزء الثلاثون (جزء عم)", Ajzaa.ArabicOrdinal(30));
        Assert.Equal("الم", Ajzaa.OpeningWords(1));
    }

    private static readonly Surah[] Surahs =
    [
        new(2, "البقرة", "Al-Baqarah", "The Cow", 286, "madinah", 87),
        new(18, "الكهف", "Al-Kahf", "The Cave", 110, "makkah", 69),
        new(38, "ص", "Ṣād", "The Letter Sad", 88, "makkah", 38),
        new(110, "النصر", "An-Naṣr", "The Divine Support", 3, "madinah", 114),
        new(114, "الناس", "An-Nās", "Mankind", 6, "makkah", 21),
    ];

    [Theory]
    [InlineData("2:255", 2, 255)]
    [InlineData("٢:٢٥٥", 2, 255)]
    [InlineData("2 255", 2, 255)]
    [InlineData("18/10", 18, 10)]
    [InlineData("Kahf 10", 18, 10)]
    [InlineData("surah al-kahf 10", 18, 10)]
    [InlineData("سورة البقرة ٢٥٥", 2, 255)]
    [InlineData("البقره آية 255", 2, 255)]
    [InlineData("nas 6", 114, 6)]
    [InlineData("ص 42", 38, 42)]
    public void AyahReferences(string query, int surah, int ayah)
    {
        var found = QuranQuery.Ayah(query, Surahs);
        Assert.NotNull(found);
        Assert.Equal((surah, ayah), (found.Value.Surah.Number, found.Value.Ayah));
    }

    [Theory]
    [InlineData("2:287")]
    [InlineData("115:1")]
    [InlineData("p 42")]
    [InlineData("page 42")]
    [InlineData("juz 7")]
    [InlineData("para 30")]
    [InlineData("الصبر")]
    public void InvalidAyahReferences(string query) => Assert.Null(QuranQuery.Ayah(query, Surahs));

    [Theory]
    [InlineData("42", 42)]
    [InlineData("page 42", 42)]
    [InlineData("p. 42", 42)]
    [InlineData("ص ٤٢", 42)]
    [InlineData("صفحة 604", 604)]
    [InlineData("sayfa 42", 42)]
    [InlineData("صفحہ 42", 42)]
    public void PageReferences(string query, int page) => Assert.Equal(page, QuranQuery.Page(query));

    [Theory]
    [InlineData("605")]
    [InlineData("page 0")]
    [InlineData("Kahf 10")]
    public void InvalidPageReferences(string query) => Assert.Null(QuranQuery.Page(query));

    [Theory]
    [InlineData("juz 7", 7)]
    [InlineData("Juz' 30", 30)]
    [InlineData("الجزء ٧", 7)]
    [InlineData("جزء 12", 12)]
    [InlineData("7. cüz", 7)]
    [InlineData("cüz 7", 7)]
    [InlineData("پارہ 30", 30)]
    [InlineData("para 1", 1)]
    public void JuzReferences(string query, int juz) => Assert.Equal(juz, QuranQuery.Juz(query));

    [Theory]
    [InlineData("juz 31")]
    [InlineData("juz")]
    [InlineData("7")]
    public void InvalidJuzReferences(string query) => Assert.Null(QuranQuery.Juz(query));

    [Theory]
    [InlineData("Meccan", true)]
    [InlineData("مكية", true)]
    [InlineData("مکی", true)]
    [InlineData("Mekkî", true)]
    [InlineData("medinan", false)]
    [InlineData("مدنية", false)]
    [InlineData("Medenî", false)]
    public void RevelationKeywords(string query, bool meccan) => Assert.Equal(meccan, QuranQuery.Revelation(query));

    [Fact]
    public void NonRevelationQueryReturnsNull() => Assert.Null(QuranQuery.Revelation("Mecca road"));
}
