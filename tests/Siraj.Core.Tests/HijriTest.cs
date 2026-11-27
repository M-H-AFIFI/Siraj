using System.Globalization;
using Siraj.Core.Hijri;
using Siraj.Core.Places;
using Xunit;
using HijriCalendar = Siraj.Core.Hijri.HijriCalendar;

namespace Siraj.Core.Tests;

public class HijriTest
{
    [Fact]
    public void UmmAlQuraMatchesReferenceTable()
    {
        int rows = 0;
        var mismatches = new List<string>();
        foreach (var r in Reference.Rows("umm_al_qura.csv"))
        {
            rows++;
            int y = int.Parse(r[0]), m = int.Parse(r[1]);
            var expected = DateOnly.Parse(r[2], CultureInfo.InvariantCulture);
            var got = HijriCalendar.UmmAlQura.FirstDayOf(y, m);
            if (got != expected) mismatches.Add($"{y}-{m}: expected {expected:yyyy-MM-dd} got {got:yyyy-MM-dd}");
            var back = HijriCalendar.UmmAlQura.ToHijri(expected);
            if (back.Year != y || back.Month != m || back.Day != 1) mismatches.Add($"{y}-{m}: {expected} reads as {back}");
        }
        Assert.Equal((1499 - 1319 + 1) * 12, rows);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches:\n{string.Join("\n", mismatches.Take(20))}");
    }

    [Fact]
    public void MonthStartsMatchReferenceTable()
    {
        var calendars = new Dictionary<string, HijriCalendar>();
        var mismatches = new List<string>();
        foreach (var r in Reference.Rows("hijri_months.csv"))
        {
            string key = r[0] + r[1];
            if (!calendars.TryGetValue(key, out var cal))
                calendars[key] = cal = new HijriCalendar(HijriRegion.ForCountry(r[0]), int.Parse(r[1]));
            int y = int.Parse(r[2]), m = int.Parse(r[3]);
            var first = cal.FirstDayOf(y, m).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            int length = cal.LengthOfMonth(y, m);
            if (first != r[4] || length != int.Parse(r[5]))
                mismatches.Add($"{string.Join(',', r)} -> {first},{length}");
        }
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches:\n{string.Join("\n", mismatches.Take(15))}");
    }

    [Fact]
    public void DaysMatchReferenceTable()
    {
        var calendars = new Dictionary<string, HijriCalendar>();
        var mismatches = new List<string>();
        foreach (var r in Reference.Rows("hijri_days.csv.gz"))
        {
            if (!calendars.TryGetValue(r[0], out var cal))
                calendars[r[0]] = cal = new HijriCalendar(HijriRegion.ForCountry(r[0]));
            var h = cal.ToHijri(DateOnly.Parse(r[1], CultureInfo.InvariantCulture));
            string got = $"{h.Year},{h.Month},{h.Day},{h.LengthOfMonth}";
            if (got != string.Join(',', r[2..])) mismatches.Add($"{r[0]} {r[1]}: expected {string.Join(',', r[2..])} got {got}");
        }
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches:\n{string.Join("\n", mismatches.Take(15))}");
    }

    [Fact]
    public void MoonPhaseMatchesReferenceTable()
    {
        foreach (var r in Reference.Rows("moon_phase.csv"))
        {
            var p = MoonPhase.At(DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(r[0])));
            Assert.Equal(double.Parse(r[1], CultureInfo.InvariantCulture), p.Illumination, 9);
            Assert.Equal(bool.Parse(r[2]), p.Waxing);
            Assert.Equal(double.Parse(r[3], CultureInfo.InvariantCulture), p.AgeDays, 7);
        }
    }

    [Fact]
    public void ZonesResolveCountryCodes()
    {
        Assert.Equal("EG", Zones.CountryOf("Africa/Cairo"));
        Assert.Equal("KW", Zones.CountryOf("Asia/Kuwait"));
        Assert.Null(Zones.CountryOf("UTC"));
        Assert.Equal("Africa/Cairo", Zones.Find("Africa/Cairo").Id);
    }
}
