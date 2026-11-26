namespace Siraj.Core.Quran;

public sealed record Surah(
    int Number, string Name, string EnglishName, string EnglishNameTranslation,
    int AyahCount, string RevelationType, int? RevelationOrder)
{
    public bool IsMeccan => RevelationType.StartsWith("mak", StringComparison.OrdinalIgnoreCase) ||
                            RevelationType.StartsWith("mec", StringComparison.OrdinalIgnoreCase);
}

public sealed record Ayah(
    int Number, int Surah, int NumberInSurah, string TextUthmani, string? TextSimple,
    int Juz, int Page, int? Hizb, int? Rub, bool Sajda);

public enum MushafLineType { SurahName, Basmallah, Ayah, Empty }

public sealed record MushafWord(
    int Position, int GlyphCode, string Text, bool IsMarker, int? Surah, int? Ayah, int? WordPosition);

public sealed record MushafLine(int LineNumber, MushafLineType Type, bool IsCentered, int? Surah, IReadOnlyList<MushafWord> Words);

public sealed record MushafPage(int Number, string ScriptType, IReadOnlyList<MushafLine> Lines)
{
    public IEnumerable<(int Surah, int Ayah)> Ayahs() =>
        Lines.SelectMany(l => l.Words).Where(w => w.Surah != null && w.Ayah != null)
            .Select(w => (w.Surah!.Value, w.Ayah!.Value)).Distinct();
}

public sealed record QuranPosition(int Page, int Surah, int Ayah);

public sealed record Bookmark(int Surah, int Ayah, int Page, DateTimeOffset Added);

public sealed record SearchHit(Ayah Ayah, IReadOnlyList<(int Start, int End)> Ranges);
