using System.Text.Json;
using System.Text.Json.Serialization;
using Siraj.Core.Places;
using Siraj.Core.Prayer;

namespace Siraj.Core.Settings;

public sealed record AppSettings
{
    public Place? Location { get; set; }
    public bool LocationIsManual { get; set; }
    public string CalculationMethod { get; set; } = Prayer.CalculationMethods.FallbackMethod;
    public string Madhab { get; set; } = "Shafi";
    public string HighLatitudeRule { get; set; } = "MiddleOfTheNight";
    public Dictionary<string, int> MinuteOffsets { get; set; } = [];

    public string Language { get; set; } = "en";
    public string ThemeMode { get; set; } = "System";
    public string Palette { get; set; } = "Emerald";
    public bool BlackMode { get; set; }
    public bool GlassUi { get; set; } = true;
    public string GlassFinish { get; set; } = "Clear";
    public bool LiveBackground { get; set; } = true;
    public string LiveScene { get; set; } = "DesertDunes";
    public string LiveIntensity { get; set; } = "Balanced";
    public bool LiveSkySync { get; set; }
    public string NumeralSystem { get; set; } = "Auto";
    public string HourFormat { get; set; } = "System";
    public bool ReduceMotion { get; set; }
    public bool ShowContinueReadingOnHome { get; set; } = true;

    public string HijriMethod { get; set; } = Hijri.HijriCalendars.MethodAuto;
    public int HijriAdjustment { get; set; }

    public Dictionary<string, bool> PrayerAlerts { get; set; } = new()
    {
        ["fajr"] = true, ["sunrise"] = false, ["dhuhr"] = true, ["asr"] = true, ["maghrib"] = true, ["isha"] = true,
    };
    public string AlertType { get; set; } = "azan";
    public string AdhanVoice { get; set; } = Adhan.AdhanVoices.DefaultId;
    public bool FajrAdhan { get; set; } = true;
    public int PrePrayerReminderMinutes { get; set; }
    public bool AdhanWindow { get; set; } = true;
    public int AdhanVolume { get; set; } = 80;

    public Quran.QuranPosition? LastRead { get; set; }
    public List<Quran.Bookmark> Bookmarks { get; set; } = [];
    public string ReciterId { get; set; } = Quran.Reciters.DefaultId;
    public string QuranTheme { get; set; } = "System";
    public string QuranPaper { get; set; } = "Creamy";
    public bool QuranSpread { get; set; } = true;
    public bool QuranContinuous { get; set; } = true;

    public bool AdhkarTranslation { get; set; } = true;
    public bool AdhkarTransliteration { get; set; } = true;
    public bool AdhkarVirtues { get; set; } = true;

    public List<string> PinnedRadioIds { get; set; } = [];
    public string? LastRadioStationId { get; set; }
    public string? LastListenReciterId { get; set; }
    public int? LastListenSurah { get; set; }
    public string? AmbientSound { get; set; }
    public double AmbientVolume { get; set; } = 0.35;

    public bool StartWithWindows { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool SetupDone { get; set; }
    public bool TrayHintShown { get; set; }
    public bool AutoCheckUpdates { get; set; } = true;
    public string? SkippedUpdateVersion { get; set; }
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
    public List<WidgetConfig> Widgets { get; set; } = [];
    public TaskbarWidgetConfig TaskbarWidget { get; set; } = new();

    [JsonIgnore]
    public CalculationSettings Calculation => new(CalculationMethod, Madhab, HighLatitudeRule, MinuteOffsets);

    public bool AlertOn(PrayerType type) =>
        PrayerAlerts.TryGetValue(type.ToString().ToLowerInvariant(), out var on) ? on : type != PrayerType.Sunrise;

    public AppSettings WithAlert(PrayerType type, bool on)
    {
        var alerts = new Dictionary<string, bool>(PrayerAlerts) { [type.ToString().ToLowerInvariant()] = on };
        return this with { PrayerAlerts = alerts };
    }

    public AppSettings WithOffset(PrayerType type, int minutes)
    {
        var offsets = new Dictionary<string, int>(MinuteOffsets) { [type.ToString().ToUpperInvariant()] = minutes };
        return this with { MinuteOffsets = offsets };
    }

    public int OffsetOf(PrayerType type) => MinuteOffsets.TryGetValue(type.ToString().ToUpperInvariant(), out var m) ? m : 0;

    public bool IsRtl => Language is "ar" or "ur";
}

public sealed record WidgetConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Face { get; set; } = "next";
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public double Scale { get; set; } = 1.0;
    public string Background { get; set; } = "glass";
    public string Palette { get; set; } = "App";
    public int Opacity { get; set; } = 85;
    public bool Locked { get; set; }
    public string? Monitor { get; set; }
}

public sealed record TaskbarWidgetConfig
{
    public bool Enabled { get; set; } = true;
    public string Layout { get; set; } = "full";
    public string Style { get; set; } = "auto";
    public string Palette { get; set; } = "App";
    public string TextColor { get; set; } = "auto";
    public string UrgentStyle { get; set; } = "gold";
    public string Position { get; set; } = "auto";
    public double CustomPositionPercent { get; set; } = 80.0;
    public int OffsetX { get; set; }
    public bool Locked { get; set; }
    public int CornerRadius { get; set; } = 6;
    public double Scale { get; set; } = 1.0;
    public int Opacity { get; set; } = 90;
    public bool ShowBorder { get; set; } = true;
    public bool ShowPrayerTime { get; set; } = true;
    public bool ShowPrefix { get; set; } = true;
    public bool ShowSeconds { get; set; }
    public bool PulseOnUrgent { get; set; } = true;
    public int UrgentMinutes { get; set; } = 15;
    public bool ShowFlyoutHijri { get; set; } = true;
    public bool ShowFlyoutVerse { get; set; } = true;
    public bool HideInFullscreen { get; set; } = true;
}

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJson : JsonSerializerContext;

public sealed class SettingsStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public AppSettings Current { get; private set; }

    public event Action<AppSettings, AppSettings>? Changed;

    public SettingsStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.json");
        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var json = File.ReadAllText(_path);
            var read = JsonSerializer.Deserialize(json, SettingsJson.Default.AppSettings) ?? new AppSettings();
            // Guard against null collections in hand-edited settings files.
            return read with
            {
                Bookmarks = read.Bookmarks ?? [], Widgets = read.Widgets ?? [],
                PinnedRadioIds = read.PinnedRadioIds ?? [],
                MinuteOffsets = read.MinuteOffsets ?? [], PrayerAlerts = read.PrayerAlerts ?? new AppSettings().PrayerAlerts,
            };
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
            return new AppSettings();
        }
    }

    public void Update(Func<AppSettings, AppSettings> change)
    {
        AppSettings old, updated;
        lock (_lock)
        {
            old = Current;
            updated = change(old);
            if (updated == old) return;
            Current = updated;
            Save(updated);
        }
        Changed?.Invoke(old, updated);
    }

    private void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, SettingsJson.Default.AppSettings);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, _path, overwrite: true);
    }
}
