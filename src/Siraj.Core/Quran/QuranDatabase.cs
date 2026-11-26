using Microsoft.Data.Sqlite;

namespace Siraj.Core.Quran;

public sealed class QuranDatabase : IDisposable
{
    public const string MadaniScript = "kfgqpc_v1";
    public const int PageCount = 604;

    private readonly SqliteConnection _db;
    private readonly object _lock = new();
    private IReadOnlyList<Surah>? _surahs;
    private Ayah[]? _ayahs;
    private string[]? _foldedText;

    static QuranDatabase() => SQLitePCL.Batteries_V2.Init();

    public QuranDatabase(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("The Quran database is not installed.", path);
        _db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
        }.ToString());
        _db.Open();
    }

    private SqliteCommand Command(string sql, params (string Name, object Value)[] args)
    {
        var command = _db.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in args) command.Parameters.AddWithValue(name, value);
        return command;
    }

    public IReadOnlyList<Surah> Surahs()
    {
        lock (_lock)
        {
            if (_surahs != null) return _surahs;
            var list = new List<Surah>(114);
            using var reader = Command("SELECT number, name, englishName, englishNameTranslation, numberOfAyahs, revelationType, revelationOrder FROM quran_surahs ORDER BY number").ExecuteReader();
            while (reader.Read())
            {
                // Normalize Latin surah names to standard transliteration once on load.
                list.Add(new Surah(reader.GetInt32(0), reader.GetString(1), SurahNames.Transliteration(reader.GetString(2)), reader.GetString(3),
                    reader.GetInt32(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetInt32(6)));
            }
            return _surahs = list;
        }
    }

    public Surah Surah(int number) => Surahs()[Math.Clamp(number, 1, 114) - 1];

    public IReadOnlyList<Ayah> Ayahs()
    {
        lock (_lock)
        {
            if (_ayahs != null) return _ayahs;
            var list = new Ayah[6236];
            using var reader = Command("SELECT ayahNumber, surahNumber, numberInSurah, textUthmani, textSimple, juz, page, hizb, rub, sajda FROM quran_ayahs ORDER BY ayahNumber").ExecuteReader();
            int i = 0;
            while (reader.Read() && i < list.Length)
            {
                list[i++] = new Ayah(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt32(5), reader.GetInt32(6),
                    reader.IsDBNull(7) ? null : reader.GetInt32(7), reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    !reader.IsDBNull(9) && reader.GetInt32(9) != 0);
            }
            if (i != list.Length) throw new InvalidDataException($"The Quran database holds {i} ayahs, not 6,236.");
            return _ayahs = list;
        }
    }

    public Ayah Ayah(int surah, int numberInSurah)
    {
        var all = Ayahs();
        int index = SurahStartIndex(surah) + Math.Clamp(numberInSurah, 1, Surah(surah).AyahCount) - 1;
        return all[index];
    }

    public IEnumerable<Ayah> AyahsOf(int surah)
    {
        int start = SurahStartIndex(surah);
        return Ayahs().Skip(start).Take(Surah(surah).AyahCount);
    }

    private int SurahStartIndex(int surah)
    {
        int index = 0;
        var surahs = Surahs();
        for (int s = 1; s < surah; s++) index += surahs[s - 1].AyahCount;
        return index;
    }

    public int PageOf(int surah, int ayah) => Ayah(surah, ayah).Page;

    public IReadOnlyList<(int Juz, Ayah First, int Page)> JuzStarts() =>
        JuzBoundaries.Select((b, i) => (i + 1, Ayah(b.Surah, b.Ayah), JuzStartPages[i])).ToList();

    public static int JuzOfPage(int page)
    {
        int juz = 1;
        for (int i = 0; i < JuzStartPages.Length; i++) if (page >= JuzStartPages[i]) juz = i + 1;
        return juz;
    }

    private static readonly (int Surah, int Ayah)[] JuzBoundaries =
    [
        (1, 1), (2, 142), (2, 253), (3, 93), (4, 24), (4, 148), (5, 82), (6, 111), (7, 88), (8, 41),
        (9, 93), (11, 6), (12, 53), (15, 1), (17, 1), (18, 75), (21, 1), (23, 1), (25, 21), (27, 56),
        (29, 46), (33, 31), (36, 28), (39, 32), (41, 47), (46, 1), (51, 31), (58, 1), (67, 1), (78, 1),
    ];

    private static readonly int[] JuzStartPages =
    [
        1, 22, 42, 62, 82, 102, 122, 142, 162, 182, 202, 222, 242, 262, 282,
        302, 322, 342, 362, 382, 402, 422, 442, 462, 482, 502, 522, 542, 562, 582,
    ];

    public MushafPage Page(int number, string scriptType = MadaniScript)
    {
        lock (_lock)
        {
            var words = new Dictionary<int, List<MushafWord>>();
            using (var reader = Command(
                "SELECT lineNumber, positionInLine, glyphCode, text, isMarker, surahNumber, ayahNumber, wordPosition FROM quran_mushaf_words " +
                "WHERE pageNumber = $p AND scriptType = $s ORDER BY lineNumber, positionInLine", ("$p", number), ("$s", scriptType)).ExecuteReader())
            {
                while (reader.Read())
                {
                    int line = reader.GetInt32(0);
                    if (!words.TryGetValue(line, out var list)) words[line] = list = [];
                    list.Add(new MushafWord(reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), reader.GetInt32(4) != 0,
                        reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetInt32(6),
                        reader.IsDBNull(7) ? null : reader.GetInt32(7)));
                }
            }
            var lines = new List<MushafLine>(15);
            using (var reader = Command(
                "SELECT lineNumber, lineType, isCentered, surahNumber FROM quran_mushaf_lines WHERE pageNumber = $p AND scriptType = $s ORDER BY lineNumber",
                ("$p", number), ("$s", scriptType)).ExecuteReader())
            {
                while (reader.Read())
                {
                    int line = reader.GetInt32(0);
                    var type = reader.GetString(1) switch
                    {
                        "surah_name" => MushafLineType.SurahName,
                        "basmallah" => MushafLineType.Basmallah,
                        "ayah" => MushafLineType.Ayah,
                        _ => MushafLineType.Empty
                    };
                    lines.Add(new MushafLine(line, type, reader.GetInt32(2) != 0, reader.IsDBNull(3) ? null : reader.GetInt32(3),
                        words.TryGetValue(line, out var w) ? w : []));
                }
            }
            return new MushafPage(number, scriptType, lines);
        }
    }

    private const int TafsirMaxLookBack = 10;

    public string? Tafsir(int globalAyah, string edition = "ar.muyassar")
    {
        lock (_lock)
        {
            var text = Command("SELECT text FROM quran_tafsirs WHERE ayahNumber = $a AND edition = $e LIMIT 1", ("$a", globalAyah), ("$e", edition))
                .ExecuteScalar() as string;
            if (text == null && globalAyah is >= 1 and <= 6236)
            {
                // Multi-ayah tafsir entries are stored under the first ayah; look back up to 10 ayahs
                // within the same surah without crossing into the previous surah.
                int floor = Math.Max(globalAyah - TafsirMaxLookBack, SurahStartIndex(Ayahs()[globalAyah - 1].Surah) + 1);
                text = Command("SELECT text FROM quran_tafsirs WHERE edition = $e AND ayahNumber <= $a AND ayahNumber >= $f ORDER BY ayahNumber DESC LIMIT 1",
                    ("$a", globalAyah), ("$e", edition), ("$f", floor)).ExecuteScalar() as string;
            }
            // The rows carry markup (Al-Muyassar's open with a <div>); the reader is shown plain text.
            return text == null ? null : Text.HtmlText.Plain(text);
        }
    }

    public IReadOnlyList<(int Ayah, long From, long To)> Segments(string reciterId, int surah)
    {
        lock (_lock)
        {
            var list = new List<(int, long, long)>();
            using var reader = Command("SELECT ayahNumber, timestampFrom, timestampTo FROM quran_reciter_segments WHERE reciterId = $r AND surahNumber = $s ORDER BY ayahNumber",
                ("$r", reciterId), ("$s", surah)).ExecuteReader();
            while (reader.Read()) list.Add((reader.GetInt32(0), reader.GetInt64(1), reader.GetInt64(2)));
            return list;
        }
    }

    public IReadOnlyList<SearchHit> Search(string query, int limit = 200)
    {
        var normalized = ArabicSearch.NormalizeQuery(query);
        if (normalized.Length < ArabicSearch.MinQueryLength) return [];
        var ayahs = Ayahs();
        lock (_lock) _foldedText ??= ayahs.Select(a => ArabicSearch.Normalize(a.TextUthmani)).ToArray();
        var terms = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hits = new List<SearchHit>();
        for (int i = 0; i < ayahs.Count && hits.Count < limit; i++)
        {
            var text = _foldedText[i];
            if (!text.Contains(normalized, StringComparison.Ordinal) && !terms.All(t => text.Contains(t, StringComparison.Ordinal))) continue;
            var ranges = text.Contains(normalized, StringComparison.Ordinal)
                ? ArabicSearch.MatchRanges(ayahs[i].TextUthmani, normalized)
                : terms.SelectMany(t => ArabicSearch.MatchRanges(ayahs[i].TextUthmani, t, 3)).OrderBy(r => r.Start).ToList();
            hits.Add(new SearchHit(ayahs[i], ranges));
        }
        return hits;
    }

    public void Dispose() => _db.Dispose();
}
