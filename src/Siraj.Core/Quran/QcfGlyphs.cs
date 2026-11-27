using System.IO.Hashing;
using System.Text;

namespace Siraj.Core.Quran;

public sealed class QcfGlyphs
{
    public const int PageCount = 604;
    public const int SlotCount = 194;

    private static readonly int[] SlotCodepoints =
        Enumerable.Range(0xFB51, 0xFBB1 - 0xFB51 + 1).Concat(Enumerable.Range(0xFBD3, 0xFC33 - 0xFBD3 + 1)).ToArray();

    private static readonly int[] SurahBannerCodepoints =
    [
        0xFC45, 0xFC46, 0xFC47, 0xFC4A, 0xFC4B, 0xFC4E, 0xFC4F, 0xFC51, 0xFC52, 0xFC53, 0xFC55, 0xFC56, 0xFC58, 0xFC5A, 0xFC5B, 0xFC5C,
        0xFC5D, 0xFC5E, 0xFC61, 0xFC62, 0xFC64, 0xFB51, 0xFB52, 0xFB54, 0xFB55, 0xFB57, 0xFB58, 0xFB5A, 0xFB5B, 0xFB5D, 0xFB5E, 0xFB60,
        0xFB61, 0xFB63, 0xFB64, 0xFB66, 0xFB67, 0xFB69, 0xFB6A, 0xFB6C, 0xFB6D, 0xFB6F, 0xFB70, 0xFB72, 0xFB73, 0xFB75, 0xFB76, 0xFB78,
        0xFB79, 0xFB7B, 0xFB7C, 0xFB7E, 0xFB7F, 0xFB81, 0xFB82, 0xFB84, 0xFB85, 0xFB87, 0xFB88, 0xFB8A, 0xFB8B, 0xFB8D, 0xFB8E, 0xFB90,
        0xFB91, 0xFB93, 0xFB94, 0xFB96, 0xFB97, 0xFB99, 0xFB9A, 0xFB9C, 0xFB9D, 0xFB9F, 0xFBA0, 0xFBA2, 0xFBA3, 0xFBA5, 0xFBA6, 0xFBA8,
        0xFBA9, 0xFBAB, 0xFBAC, 0xFBAE, 0xFBAF, 0xFBB1, 0xFBB2, 0xFBB4, 0xFBB5, 0xFBB7, 0xFBB8, 0xFBBA, 0xFBBB, 0xFBBD, 0xFBBE, 0xFBC0,
        0xFBC1, 0xFBD3, 0xFBD4, 0xFBD6, 0xFBD7, 0xFBD9, 0xFBDA, 0xFBDC, 0xFBDD, 0xFBDF, 0xFBE0, 0xFBE2, 0xFBE3, 0xFBE5, 0xFBE6, 0xFBE8,
        0xFBE9, 0xFBEB,
    ];

    public const string BasmalahGlyphs = "!\"#";

    private readonly (uint Fingerprint, byte[] SlotCounts)[] _pages;

    private QcfGlyphs((uint, byte[])[] pages) => _pages = pages;

    public static QcfGlyphs Load(TextReader reader)
    {
        var pages = new List<(uint, byte[])>(PageCount);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                pages.Add((0, []));
                continue;
            }
            int space = line.IndexOf(' ');
            var digits = line[(space + 1)..];
            pages.Add((Convert.ToUInt32(line[..space], 16), digits.Select(d => (byte)(d - '0')).ToArray()));
        }
        if (pages.Count != PageCount) throw new InvalidDataException($"Expected {PageCount} pages in glyph table, found {pages.Count}.");
        return new QcfGlyphs(pages.ToArray());
    }

    public static string SlotGlyph(int slot) => slot is >= 0 and < SlotCount ? char.ConvertFromUtf32(SlotCodepoints[slot]) : "";

    public static string SurahBanner(int surah) => surah is >= 1 and <= 114 ? char.ConvertFromUtf32(SurahBannerCodepoints[surah - 1]) : "";

    public IReadOnlyList<string>? GlyphsFor(MushafPage page)
    {
        if (page.Number is < 1 or > PageCount) return null;
        var (fingerprint, counts) = _pages[page.Number - 1];
        var words = page.Lines.Where(l => l.Type != MushafLineType.Empty).OrderBy(l => l.LineNumber).SelectMany(l => l.Words).ToList();
        if (counts.Length == 0 || words.Count != counts.Length) return null;

        var crc = new Crc32();
        for (int i = 0; i < words.Count; i++)
        {
            if (i > 0) crc.Append([10]);
            crc.Append(Encoding.UTF8.GetBytes(words[i].Text));
        }
        if (crc.GetCurrentHashAsUInt32() != fingerprint) return null;

        var glyphs = new List<string>(words.Count);
        int slot = 0;
        foreach (var count in counts)
        {
            var sb = new StringBuilder(count * 2);
            for (int k = 0; k < count; k++) sb.Append(SlotGlyph(slot + k));
            slot += count;
            glyphs.Add(sb.ToString());
        }
        return glyphs;
    }

    public static bool IsAyahMarker(string text) =>
        !string.IsNullOrWhiteSpace(text) && text.All(c => c is >= '٠' and <= '٩' || c == '۝' || char.IsWhiteSpace(c));
}
