using Siraj.Core.Devotions;
using Siraj.Core.Hijri;
using Xunit;

namespace Siraj.Core.Tests;

public class DevotionsTest
{
    [Fact]
    public void ReadsTheWholeBookWithExpectedNumbering()
    {
        Assert.Equal(384, AdhkarBook.All.Count);
        Assert.Equal(140, AdhkarBook.Chapters.Count);
        Assert.Equal(Enumerable.Range(1, 384), AdhkarBook.All.Select(d => d.Id));
        Assert.All(AdhkarBook.All, d => Assert.True(d.Count >= 1 && d.Text.Length > 0));
        // Every chapter has its title in the other three languages.
        Assert.All(AdhkarBook.Chapters, c => Assert.NotEqual(c.Title, AdhkarBook.ChapterTitle(c.Title, "en")));
    }

    [Fact]
    public void EveryGroupFindsItsAdhkar()
    {
        foreach (var group in Enum.GetValues<AdhkarGroup>())
            Assert.True(AdhkarBook.Of(group).Count > 0, group.ToString());
        Assert.True(AdhkarBook.Of(AdhkarGroup.Morning).Count >= 20);
        Assert.Contains(AdhkarBook.Of(AdhkarGroup.Morning), d => d.Count == 100);
    }

    [Fact]
    public void PicksOneLanguageFromATaggedValue()
    {
        const string value = "[ar]عربي\n[en]English\n[tr]Türkçe";
        Assert.Equal("عربي", Tagged.Pick(value, "ar"));
        Assert.Equal("English", Tagged.Pick(value, "en"));
        Assert.Equal("Türkçe", Tagged.Pick(value, "tr"));
        Assert.Equal("عربي", Tagged.Pick(value, "ur"));
        Assert.Equal("English", Tagged.Pick("[ar]عربي\n[en]English", "tr"));
        Assert.Equal("plain", Tagged.Pick("plain", "en"));
        Assert.Null(Tagged.Pick("  ", "en"));
    }

    [Fact]
    public void SuggestsTheAdhkarOfTheHour()
    {
        Assert.Equal(AdhkarGroup.Morning, AdhkarGroups.Suggested(new TimeOnly(6, 0)));
        Assert.Equal(AdhkarGroup.AfterPrayer, AdhkarGroups.Suggested(new TimeOnly(13, 0)));
        Assert.Equal(AdhkarGroup.Evening, AdhkarGroups.Suggested(new TimeOnly(18, 30)));
        Assert.Equal(AdhkarGroup.Sleep, AdhkarGroups.Suggested(new TimeOnly(23, 0)));
        Assert.Equal(AdhkarGroup.Sleep, AdhkarGroups.Suggested(new TimeOnly(2, 0)));
    }

    [Fact]
    public void HadithAndNamesLoad()
    {
        Assert.True(Hadiths.All.Count > 100);
        Assert.Equal(Hadiths.All.Count, Hadiths.All.Select(h => h.Id).Distinct().Count());
        Assert.All(Hadiths.All, h => Assert.False(string.IsNullOrWhiteSpace(h.MatnAr)));
        Assert.Equal(17, Hadiths.Categories.Count);
        Assert.Equal("Namaz", Hadiths.Category("Prayer", "الصلاة", "tr"));
        Assert.Equal(Enumerable.Range(1, 99), AllahNames.All.Select(n => n.Number));
        Assert.All(AllahNames.All, n => Assert.False(string.IsNullOrWhiteSpace(n.Meaning("ur"))));
    }

    [Fact]
    public void HadithOfTheDayRotatesByEpochDay()
    {
        Assert.Same(Hadiths.All[0], Hadiths.ForDay(new DateOnly(1970, 1, 1)));
        Assert.Same(Hadiths.All[1], Hadiths.ForDay(new DateOnly(1970, 1, 2)));
        Assert.Same(Hadiths.All[^1], Hadiths.ForDay(new DateOnly(1969, 12, 31)));
        int epochDay = new DateOnly(2026, 10, 30).DayNumber - new DateOnly(1970, 1, 1).DayNumber;
        Assert.Same(Hadiths.All[epochDay % Hadiths.All.Count], Hadiths.ForDay(new DateOnly(2026, 10, 30)));
    }

    [Fact]
    public void ZakatMatchesExpectedThresholdsAndRates()
    {
        Assert.Equal(36, Zakat.Currencies.Count);
        Assert.Equal("SAR", Zakat.Currencies[0].Code);
        var none = Zakat.Calculate(new ZakatInputs { Cash = 1000, GoldPrice = 0, SilverPrice = 0 });
        Assert.False(none.Eligible);
        Assert.Equal(0, none.Due);
        var below = Zakat.Calculate(new ZakatInputs { Cash = 2000, GoldPrice = 280, SilverPrice = 3.75 });
        Assert.Equal(595 * 3.75, below.Nisab, 6);
        Assert.False(below.Eligible);
        var due = Zakat.Calculate(new ZakatInputs { Cash = 10000, Debts = 2000, GoldGrams = 10, GoldPrice = 280, SilverPrice = 0 });
        Assert.Equal(85 * 280, due.Nisab, 6);
        Assert.Equal(10000 + 2800 - 2000, due.Net, 6);
        Assert.False(due.Eligible); // 10,800 is under the gold nisab of 23,800
        var gold = Zakat.Calculate(new ZakatInputs { Cash = 30000, GoldPrice = 280, SilverPrice = 0 });
        Assert.True(gold.Eligible);
        Assert.Equal(750, gold.Due, 6);
    }

    [Fact]
    public void TasbihStartsANewRoundAfterTheGoal()
    {
        Assert.Equal(8, TasbihPhrases.BuiltIn.Count);
        Assert.Equal([33, 33, 34], TasbihPhrases.BuiltIn.Take(3).Select(p => p.DefaultGoal));
        Assert.Equal((33, true), TasbihPhrases.Increment(32, 33));
        Assert.Equal((1, false), TasbihPhrases.Increment(33, 33));
        Assert.Equal((5, false), TasbihPhrases.Increment(4, 0));
    }

    [Fact]
    public void UpcomingFastsAreInOrderAndInTheFuture()
    {
        var today = new DateOnly(2026, 10, 30); // a Friday
        var list = Fasting.Upcoming(today, HijriCalendar.UmmAlQura);
        Assert.Equal(list.OrderBy(f => f.DaysAway), list);
        Assert.All(list, f => Assert.True(f.DaysAway >= 0 && f.Date == today.AddDays(f.DaysAway)));
        Assert.Equal(new DateOnly(2026, 11, 2), list.Single(f => f.Occasion == FastOccasion.Monday).Date);
        Assert.Equal(new DateOnly(2026, 11, 5), list.Single(f => f.Occasion == FastOccasion.Thursday).Date);
        var ramadan = list.Single(f => f.Occasion == FastOccasion.Ramadan);
        Assert.Equal((9, 1), (HijriCalendar.UmmAlQura.ToHijri(ramadan.Date).Month, HijriCalendar.UmmAlQura.ToHijri(ramadan.Date).Day));
        var white = list.Single(f => f.Occasion == FastOccasion.WhiteDays);
        Assert.InRange(white.HijriDay, 13, 15);
    }

    [Fact]
    public void StreakCountsBackFromTodayOrYesterday()
    {
        var today = new DateOnly(2026, 10, 30);
        var log = new Dictionary<string, string>
        {
            [Fasting.Key(today.AddDays(-1))] = "voluntary", [Fasting.Key(today.AddDays(-2))] = "voluntary",
            [Fasting.Key(today.AddDays(-4))] = "voluntary",
        };
        Assert.Equal(2, Fasting.Streak(log, today));
        log[Fasting.Key(today)] = "voluntary";
        Assert.Equal(3, Fasting.Streak(log, today));
        Assert.Equal(0, Fasting.Streak(new Dictionary<string, string>(), today));
    }

    [Fact]
    public void JournalKeepsTodaysAdhkarAndForgetsYesterdays()
    {
        var dir = Path.Combine(Path.GetTempPath(), "siraj-journal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var day = new DateOnly(2026, 10, 30);
            using (var store = new JournalStore(dir))
            {
                store.SetAdhkarCount("Morning", day, 5, 3);
                store.Update(j => j.Tasbih.Count = 12);
            }
            using (var again = new JournalStore(dir))
            {
                Assert.Equal(3, again.AdhkarCounts("Morning", day)[5]);
                Assert.Empty(again.AdhkarCounts("Morning", day.AddDays(1)));
                Assert.Equal(12, again.Current.Tasbih.Count);
            }
            File.WriteAllText(Path.Combine(dir, "journal.json"), "{ not json");
            using var broken = new JournalStore(dir);
            Assert.Empty(broken.Current.Adhkar);
            Assert.True(File.Exists(Path.Combine(dir, "journal.bad.json")));
        }
        finally { Directory.Delete(dir, true); }
    }
}
