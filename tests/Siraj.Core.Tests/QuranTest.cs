using Siraj.Core.Quran;
using Xunit;
using Xunit.Abstractions;

namespace Siraj.Core.Tests;

public class QuranTest(ITestOutputHelper output)
{
    private static readonly string? DbPath = Environment.GetEnvironmentVariable("SIRAJ_QURAN_DB");

    private static readonly string GlyphTable = Path.Combine(AppContext.BaseDirectory, "reference", "qcf_v1_glyphs.txt");

    private bool Skip()
    {
        if (DbPath != null && File.Exists(DbPath)) return false;
        output.WriteLine("SIRAJ_QURAN_DB not set: the database checks did not run.");
        return true;
    }

    [Fact]
    public void MadaniPagesMatchGlyphTable()
    {
        if (Skip()) return;
        using var db = new QuranDatabase(DbPath!);
        var glyphs = QcfGlyphs.Load(new StreamReader(GlyphTable));
        var failed = new List<int>();
        for (int page = 1; page <= QcfGlyphs.PageCount; page++)
        {
            var mushaf = db.Page(page);
            var g = glyphs.GlyphsFor(mushaf);
            if (g == null || g.Count != mushaf.Lines.Sum(l => l.Words.Count)) failed.Add(page);
        }
        Assert.True(failed.Count == 0, $"{failed.Count} pages do not match: {string.Join(',', failed.Take(20))}");
    }

    [Fact]
    public void DatabaseContainsCompleteQuranMetadata()
    {
        if (Skip()) return;
        using var db = new QuranDatabase(DbPath!);
        Assert.Equal(114, db.Surahs().Count);
        Assert.True(db.Surah(1).IsMeccan);
        Assert.False(db.Surah(2).IsMeccan);
        Assert.Equal(86, db.Surahs().Count(s => s.IsMeccan));
        Assert.Equal(6236, db.Ayahs().Count);
        Assert.Equal(7, db.AyahsOf(1).Count());
        Assert.Equal(286, db.AyahsOf(2).Count());
        Assert.Equal(1, db.PageOf(1, 1));
        Assert.Equal(2, db.PageOf(2, 1));
        Assert.Equal(604, db.PageOf(114, 1));
        Assert.Equal(30, db.JuzStarts().Count);
        Assert.Equal(122, db.JuzStarts()[6].Page);
        Assert.Equal(15, QuranDatabase.JuzOfPage(290));
        Assert.Equal(7, db.Segments("ar.alafasy", 1).Count);
        Assert.False(string.IsNullOrWhiteSpace(db.Tafsir(1)));
        var fatiha = db.Page(1);
        Assert.Equal(MushafLineType.SurahName, fatiha.Lines[0].Type);
    }

    [Fact]
    public void JuzStartPagesMatchFirstHizb()
    {
        if (Skip()) return;
        using var db = new QuranDatabase(DbPath!);
        var starts = db.JuzStarts();
        var wrong = starts.Where(s => Ajzaa.HizbStartPage(s.Juz * 2 - 1) != s.First.Page)
            .Select(s => $"juz {s.Juz}: hizb page {Ajzaa.HizbStartPage(s.Juz * 2 - 1)}, ayah page {s.First.Page}").ToList();
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [Fact]
    public void AyahQueryRejectsPageAndJuzKeywords()
    {
        if (Skip()) return;
        using var db = new QuranDatabase(DbPath!);
        var surahs = db.Surahs();
        foreach (var query in new[] { "page 42", "p 42", "juz 7", "para 30", "cüz 7", "sayfa 42", "الجزء 7", "جزء 7", "صفحة 42" })
            Assert.Null(QuranQuery.Ayah(query, surahs));
        foreach (var (query, surah, ayah) in new[] { ("Kahf 10", 18, 10), ("nas 1", 114, 1), ("يس 1", 36, 1), ("Ar-Rahman 13", 55, 13) })
        {
            var found = QuranQuery.Ayah(query, surahs);
            Assert.True(found != null, query);
            Assert.Equal((surah, ayah), (found.Value.Surah.Number, found.Value.Ayah));
        }
        Assert.Equal(86, surahs.Count(x => x.IsMeccan == QuranQuery.Revelation("Meccan")));
    }

    [Fact]
    public void SearchMatchesArabicIgnoringDiacritics()
    {
        if (Skip()) return;
        using var db = new QuranDatabase(DbPath!);
        var hits = db.Search("الرحمن الرحيم");
        Assert.Contains(hits, h => h.Ayah.Surah == 1 && h.Ayah.NumberInSurah == 1);
        Assert.Contains(hits, h => h.Ayah.Surah == 1 && h.Ayah.NumberInSurah == 3);
        var first = hits.First(h => h.Ayah.Surah == 1 && h.Ayah.NumberInSurah == 3);
        Assert.NotEmpty(first.Ranges);
        Assert.True(db.Search("امنوا").Count >= 100);
        Assert.Empty(db.Search("ا"));
    }

    [Fact]
    public void ArabicFoldingNormalizesDiacriticsAndHamzas()
    {
        Assert.Equal("الرحمن", ArabicSearch.Normalize("ٱلرَّحْمَـٰنِ"));
        Assert.Equal("امنوا", ArabicSearch.Normalize("ءَامَنُوا۟"));
        Assert.Equal("الحمد لله", ArabicSearch.NormalizeQuery("  الحمد   لله "));
        var ranges = ArabicSearch.MatchRanges("بِسْمِ ٱللَّهِ ٱلرَّحْمَـٰنِ", "الرحمن");
        Assert.Single(ranges);
    }

    [Fact]
    public void ReciterUrlsMatchExpectedFormats()
    {
        var alafasy = Reciters.Find("ar.alafasy");
        Assert.Equal("https://everyayah.com/data/Alafasy_128kbps/002255.mp3", Reciters.AyahUrl(alafasy, 2, 255).ToString());
        var jalil = Reciters.Find("ar.khalidaljalil");
        Assert.Equal(ReciterMode.SurahSegments, jalil.Mode);
        Assert.Equal("https://download.quranicaudio.com/qdc/khalid_jalil/murattal/mp3/18.mp3", Reciters.SurahUrl(jalil, 18).ToString());
        Assert.Equal(34, Reciters.All.Count);
        Assert.Equal(75, Reciters.SurahServers.Count);
        Assert.All(Reciters.All, r =>
        {
            var url = Reciters.SurahUrl(r, 1).ToString();
            Assert.DoesNotContain("//", url["https://".Length..]);
        });
    }

    [Fact]
    public void TafsirReturnsPlainText()
    {
        Assert.Equal("a & b\n\nc", Siraj.Core.Text.HtmlText.Plain("<div><p>a &amp; b</p>\n\n\n<p>c &rlm;</p></div>"));
        if (Skip()) return;
        using var db = new QuranDatabase(DbPath!);
        var missing = Enumerable.Range(1, 6236).Where(n => db.Tafsir(n) == null).ToList();
        // 4 ayahs have no Muyassar entry within the 10-ayah lookback window.
        Assert.Equal([5405, 5812, 5813, 5814], missing);
        var first = db.Tafsir(1)!;
        Assert.DoesNotContain("<", first);
        Assert.Contains("\n", first);
    }

    [Fact]
    public void SurahNamesFormatAndMatchQueries()
    {
        Assert.Equal("Al-Fātiḥah", SurahNames.Transliteration("Al-Fātiĥah"));
        Assert.Equal("Āli ʿImrān", SurahNames.Transliteration("Āli `Imrān"));
        Assert.Equal("Al-Anʿām", SurahNames.Transliteration("Al-'An`ām"));
        Assert.Equal("Ash-Shūrā", SurahNames.Transliteration("Ash-Shūraá"));
        Assert.Equal("Al-Fātiḥah", SurahNames.Transliteration("Al-Fātiḥah"));

        var rahman = new Surah(55, "الرحمن", "Ar-Raḥmān", "The Beneficent", 78, "madinah", 97);
        Assert.True(SurahNames.Matches(rahman, "rahman"));
        Assert.True(SurahNames.Matches(rahman, "Surah Ar Rahman"));
        Assert.True(SurahNames.Matches(rahman, "٥٥"));
        Assert.False(SurahNames.Matches(rahman, "5"));
        var imran = new Surah(3, "آل عمران", "Āli ʿImrān", "Family of Imran", 200, "madinah", 89);
        Assert.True(SurahNames.Matches(imran, "imran"));
        Assert.True(SurahNames.Matches(imran, "عمران"));
    }

    [Fact]
    public void KhatmaPlannerSplitsFullMushaf()
    {
        var start = new DateOnly(2026, 11, 1);
        var state = KhatmaPlanner.Create(start, targetDays: 30, startPage: 1);
        var summary = KhatmaPlanner.Summarize(state, start);

        Assert.Equal(30, summary.Portions.Count);
        Assert.Equal(1, summary.Portions[0].FromPage);
        Assert.Equal(QuranDatabase.PageCount, summary.Portions[^1].ToPage);
        Assert.Equal(QuranDatabase.PageCount, summary.TotalPages);
        Assert.Equal(0, summary.PagesRead);
        Assert.False(summary.IsFinished);
        for (int i = 1; i < summary.Portions.Count; i++)
            Assert.Equal(summary.Portions[i - 1].ToPage + 1, summary.Portions[i].FromPage);

        state.CompletedDays.AddRange([1, 2]);
        var day2 = KhatmaPlanner.Summarize(state, start.AddDays(1));
        Assert.Equal(2, day2.CompletedDays);
        Assert.Equal(2, day2.Streak);
        Assert.Equal(3, day2.TodayPortion.DayNumber);
        Assert.True(day2.PagesRead > 35);
    }

    [Fact]
    public void KhatmaPlannerStartsFromCustomPage()
    {
        var start = new DateOnly(2026, 11, 1);
        var state = KhatmaPlanner.Create(start, targetDays: 10, startPage: 305);
        var summary = KhatmaPlanner.Summarize(state, start);

        Assert.Equal(305, summary.Portions[0].FromPage);
        Assert.Equal(604, summary.Portions[^1].ToPage);
        Assert.Equal(300, summary.TotalPages);
        Assert.Equal(30, KhatmaPlanner.PagesPerDay(10, 305));
    }

    [Fact]
    public void DailyAyahRotatesAcrossLanguages()
    {
        var a = DailyAyah.ForDay(new DateOnly(2026, 11, 23));
        var b = DailyAyah.ForDay(new DateOnly(2026, 11, 24));
        Assert.NotEqual(a, b);
        Assert.All(DailyAyah.Pool, v =>
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Arabic));
            Assert.InRange(v.Surah, 1, 114);
            Assert.InRange(v.Page, 1, QuranDatabase.PageCount);
            Assert.False(string.IsNullOrWhiteSpace(v.Translation("en")));
            Assert.False(string.IsNullOrWhiteSpace(v.Translation("tr")));
            Assert.False(string.IsNullOrWhiteSpace(v.Translation("ur")));
        });
    }
}
