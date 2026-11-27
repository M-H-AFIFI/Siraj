using System.Text.Json;
using System.Text.Json.Serialization;

namespace Siraj.Core.Devotions;

public sealed class Journal
{
    public Dictionary<string, AdhkarProgress> Adhkar { get; set; } = [];
    public TasbihState Tasbih { get; set; } = new();
    public Dictionary<string, string> Fasts { get; set; } = [];
    public ZakatInputs Zakat { get; set; } = new();
    public Quran.KhatmaState? Khatma { get; set; }
}

public sealed class AdhkarProgress
{
    public string Day { get; set; } = "";
    public Dictionary<int, int> Counts { get; set; } = [];
}

public sealed class TasbihState
{
    public int Phrase { get; set; }
    public int Count { get; set; }
    public Dictionary<int, int> Goals { get; set; } = [];
    public List<TasbihPhrase> Custom { get; set; } = [];
    public string Day { get; set; } = "";
    public int Today { get; set; }
    public long Total { get; set; }
    public bool Sound { get; set; } = true;
}

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Journal))]
internal partial class JournalJson : JsonSerializerContext;

public sealed class JournalStore : IDisposable
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly Timer _timer;
    private bool _dirty;

    public Journal Current { get; }

    public event Action? Changed;

    public JournalStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "journal.json");
        Current = Load();
        _timer = new Timer(_ => Flush());
    }

    private Journal Load()
    {
        try
        {
            if (!File.Exists(_path)) return new Journal();
            var read = JsonSerializer.Deserialize(File.ReadAllText(_path), JournalJson.Default.Journal) ?? new Journal();
            read.Adhkar ??= [];
            read.Tasbih ??= new TasbihState();
            read.Tasbih.Goals ??= [];
            read.Tasbih.Custom ??= [];
            read.Fasts ??= [];
            read.Zakat ??= new ZakatInputs();
            return read;
        }
        catch (Exception)
        {
            try
            {
                File.Copy(_path, Path.ChangeExtension(_path, ".bad.json"), overwrite: true);
            }
            catch
            {
            }
            return new Journal();
        }
    }

    public void Update(Action<Journal> change)
    {
        lock (_lock)
        {
            change(Current);
            _dirty = true;
        }
        _timer.Change(800, Timeout.Infinite);
        Changed?.Invoke();
    }

    public void Flush()
    {
        string json;
        lock (_lock)
        {
            if (!_dirty) return;
            json = JsonSerializer.Serialize(Current, JournalJson.Default.Journal);
            _dirty = false;
        }
        try
        {
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _path, overwrite: true);
        }
        catch (IOException)
        {
            lock (_lock) _dirty = true;
        }
    }

    public IReadOnlyDictionary<int, int> AdhkarCounts(string key, DateOnly day)
    {
        lock (_lock)
            return Current.Adhkar.TryGetValue(key, out var p) && p.Day == Fasting.Key(day) ? new Dictionary<int, int>(p.Counts) : [];
    }

    public void SetAdhkarCount(string key, DateOnly day, int id, int count) => Update(j =>
    {
        var dayKey = Fasting.Key(day);
        if (!j.Adhkar.TryGetValue(key, out var p) || p.Day != dayKey) j.Adhkar[key] = p = new AdhkarProgress { Day = dayKey };
        if (count <= 0)
            p.Counts.Remove(id);
        else
            p.Counts[id] = count;
    });

    public void ResetAdhkar(string key) => Update(j => j.Adhkar.Remove(key));

    public void Dispose()
    {
        _timer.Dispose();
        Flush();
    }
}
