using System.Net.Http;
using Siraj.Core.Quran;

namespace Siraj.App.Services;

internal sealed class QuranService
{
    public enum State { Missing, Downloading, Ready, Failed }

    private readonly QuranAssets _assets = new(Path.Combine(AppPaths.Data, "quran"));
    private QuranDatabase? _db;
    private QcfGlyphs? _glyphs;
    private CancellationTokenSource? _download;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public State Status { get; private set; }
    public DownloadProgress Progress { get; private set; }
    public string? Error { get; private set; }

    public event Action? Changed;

    public QuranService()
    {
        Status = _assets.IsInstalled ? State.Ready : State.Missing;
        // Initialize SQLitePCL off the WinUI UI thread: its WinRT temp-dir probe raises a
        // stowed 0x80073D54 in unpackaged desktop apps, which fails fast if run inside XAML Loaded.
        if (Status == State.Ready) _ = Task.Run(() =>
        {
            try
            {
                _ = Database.Surahs();
                App.Dispatcher.TryEnqueue(() => Changed?.Invoke());
            }
            catch { }
        });
    }

    public QuranDatabase? LoadedDatabase => _db;

    public QuranAssets Assets => _assets;

    public static long DownloadBytes => QuranAssets.TotalBytes;

    public QuranDatabase Database
    {
        get
        {
            if (Status != State.Ready) throw new InvalidOperationException("The Quran is not installed yet.");
            return _db ??= new QuranDatabase(_assets.DatabasePath);
        }
    }

    public QcfGlyphs Glyphs
    {
        get
        {
            if (_glyphs != null) return _glyphs;
            using var reader = new StreamReader(AppPaths.Asset("Quran", "qcf_v1_glyphs.txt"));
            return _glyphs = QcfGlyphs.Load(reader);
        }
    }

    public void StartDownload()
    {
        if (Status is State.Downloading or State.Ready) return;
        _download = new CancellationTokenSource();
        var token = _download.Token;
        Status = State.Downloading;
        Error = null;
        Changed?.Invoke();
        var progress = new Progress<DownloadProgress>(p =>
        {
            Progress = p;
            Changed?.Invoke();
        });
        _ = Task.Run(async () =>
        {
            try
            {
                await _assets.InstallAsync(Http, progress, token);
                Log.Info("Quran assets installed");
                App.Dispatcher.TryEnqueue(() =>
                {
                    Status = State.Ready;
                    Changed?.Invoke();
                });
            }
            catch (OperationCanceledException)
            {
                App.Dispatcher.TryEnqueue(() =>
                {
                    Status = State.Missing;
                    Changed?.Invoke();
                });
            }
            catch (Exception e)
            {
                Log.Error("Quran download", e);
                App.Dispatcher.TryEnqueue(() =>
                {
                    Status = State.Failed;
                    Error = e.Message;
                    Changed?.Invoke();
                });
            }
        }, token);
    }

    public void CancelDownload() => _download?.Cancel();

    public void SavePosition(int page, int surah, int ayah) =>
        App.Settings.Update(s => s with { LastRead = new QuranPosition(page, surah, ayah) });

    public bool IsBookmarked(int surah, int ayah) => App.Settings.Current.Bookmarks.Any(b => b.Surah == surah && b.Ayah == ayah);

    public void ToggleBookmark(int surah, int ayah, int page) => App.Settings.Update(s =>
    {
        var list = s.Bookmarks.Where(b => !(b.Surah == surah && b.Ayah == ayah)).ToList();
        if (list.Count == s.Bookmarks.Count) list.Insert(0, new Bookmark(surah, ayah, page, DateTimeOffset.Now));
        return s with { Bookmarks = list };
    });
}
