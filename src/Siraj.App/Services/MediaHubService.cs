using System.Net.Http;
using Microsoft.UI.Dispatching;
using Siraj.Core.Audio;
using Siraj.Core.Quran;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Siraj.App.Services;

public enum MediaKind { None, Listen, Radio }

public enum RepeatMode { Off, All, One }

public sealed record ListenTrack(string Id, string Title, string Subtitle, Uri Url, string? Artwork, int? SurahNumber = null, string? ReciterId = null);

internal sealed class MediaHubService : IDisposable
{
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly HttpClient DownloadHttp = new() { Timeout = TimeSpan.FromMinutes(2) };

    private readonly DispatcherQueue _ui;
    private readonly MediaPlayer _player = new();
    private readonly MediaPlayer _ambientPlayer = new() { IsLoopingEnabled = true };
    private readonly DispatcherQueueTimer _tick;
    private readonly List<ListenTrack> _queue = [];
    private int _queueIndex = -1;
    private DateTimeOffset? _sleepAt;
    private bool _sleepEndOfTrack;

    public MediaKind Kind { get; private set; } = MediaKind.None;
    public ListenTrack? CurrentTrack => _queueIndex >= 0 && _queueIndex < _queue.Count ? _queue[_queueIndex] : null;
    public RadioStation? CurrentStation { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsBuffering { get; private set; }
    public string? Error { get; private set; }
    public TimeSpan Position => _player.PlaybackSession?.Position ?? TimeSpan.Zero;
    public TimeSpan Duration => _player.PlaybackSession?.NaturalDuration ?? TimeSpan.Zero;
    public bool Shuffle { get; private set; }
    public RepeatMode Repeat { get; private set; } = RepeatMode.Off;
    public double Speed { get; private set; } = 1.0;
    public bool HasSleepTimer => _sleepAt != null || _sleepEndOfTrack;
    public TimeSpan? SleepRemaining => _sleepAt is { } at ? (at > DateTimeOffset.Now ? at - DateTimeOffset.Now : TimeSpan.Zero) : null;
    public bool SleepAtEndOfTrack => _sleepEndOfTrack;

    public string? ActiveAmbientId { get; private set; }
    public bool AmbientLoading { get; private set; }
    public double AmbientVolume { get; private set; } = 0.35;

    public bool HasMedia => Kind != MediaKind.None && (CurrentTrack != null || CurrentStation != null);

    public event Action? Changed;
    public event Action? PositionTick;

    public MediaHubService(DispatcherQueue ui)
    {
        _ui = ui;
        AmbientVolume = Math.Clamp(App.Settings.Current.AmbientVolume, 0.05, 1.0);
        _ambientPlayer.Volume = AppPaths.Muted ? 0 : AmbientVolume;
        _player.Volume = AppPaths.Muted ? 0 : 1.0;
        _player.PlaybackSession.PlaybackStateChanged += (_, _) => _ui.TryEnqueue(SyncPlaybackState);
        _player.MediaEnded += (_, _) => _ui.TryEnqueue(OnEnded);
        _player.MediaFailed += (_, e) => _ui.TryEnqueue(() =>
        {
            Log.Error($"media failed: {e.Error} {e.ErrorMessage}");
            IsPlaying = false;
            IsBuffering = false;
            Error = e.ErrorMessage;
            Changed?.Invoke();
        });
        _player.CommandManager.IsEnabled = true;
        App.Adhan.PlayingChanged += p =>
        {
            if (p != null && IsPlaying)
                _ui.TryEnqueue(Pause);
        };

        _tick = _ui.CreateTimer();
        _tick.Interval = TimeSpan.FromMilliseconds(500);
        _tick.Tick += (_, _) =>
        {
            if (_sleepAt is { } at && DateTimeOffset.Now >= at)
            {
                _sleepAt = null;
                Pause();
                Changed?.Invoke();
                return;
            }
            if (IsPlaying || HasSleepTimer) PositionTick?.Invoke();
        };
        _tick.Start();
    }

    private void SyncPlaybackState()
    {
        var state = _player.PlaybackSession.PlaybackState;
        IsPlaying = state == MediaPlaybackState.Playing;
        IsBuffering = state is MediaPlaybackState.Opening or MediaPlaybackState.Buffering;
        if (IsPlaying) Error = null;
        if (ActiveAmbientId != null)
        {
            if (IsPlaying && _ambientPlayer.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
                _ambientPlayer.Play();
            else if (!IsPlaying && _ambientPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                _ambientPlayer.Pause();
        }
        Changed?.Invoke();
    }

    private void OnEnded()
    {
        if (Kind != MediaKind.Listen) return;
        if (_sleepEndOfTrack)
        {
            _sleepEndOfTrack = false;
            IsPlaying = false;
            Changed?.Invoke();
            return;
        }
        if (Repeat == RepeatMode.One)
        {
            Seek(TimeSpan.Zero);
            _player.Play();
            return;
        }
        Next(auto: true);
    }

    public void PlayReciterSurah(Reciter reciter, int surahNumber)
    {
        var loc = Loc.Current;
        var surahs = Enumerable.Range(1, 114).Select(n =>
        {
            string name = SurahTitle(n, loc.Language);
            return new ListenTrack(
                $"{reciter.Id}:{n}",
                name,
                reciter.Name(loc.Language),
                Reciters.SurahUrl(reciter, n),
                ListeningLibrary.PortraitFiles.TryGetValue(reciter.Id, out var f) ? $"Portraits/{f}" : null,
                SurahNumber: n,
                ReciterId: reciter.Id);
        }).ToList();
        App.Settings.Update(s => s with { LastListenReciterId = reciter.Id, LastListenSurah = surahNumber });
        PlayQueue(surahs, Math.Clamp(surahNumber - 1, 0, surahs.Count - 1));
    }

    public void PlayRare(RareCollection collection, int trackIndex = 0)
    {
        var loc = Loc.Current;
        var tracks = collection.Tracks.Select((tr, idx) =>
        {
            string title = string.Join(" · ", tr.Surahs.Select(s => SurahTitle(s, loc.Language)));
            return new ListenTrack($"{collection.Id}:{idx}", title, collection.Name(loc.Language), tr.Url, collection.PortraitFile, tr.Surahs.FirstOrDefault());
        }).ToList();
        PlayQueue(tracks, Math.Clamp(trackIndex, 0, tracks.Count - 1));
    }

    public void PlayQueue(IReadOnlyList<ListenTrack> tracks, int startIndex)
    {
        if (tracks.Count == 0) return;
        App.QuranPlayer.Stop();
        _queue.Clear();
        _queue.AddRange(tracks);
        _queueIndex = Math.Clamp(startIndex, 0, _queue.Count - 1);
        CurrentStation = null;
        Kind = MediaKind.Listen;
        Error = null;
        OpenCurrentTrack();
    }

    public async void PlayRadio(RadioStation station)
    {
        App.QuranPlayer.Stop();
        Kind = MediaKind.Radio;
        CurrentStation = station;
        _queue.Clear();
        _queueIndex = -1;
        Error = null;
        IsBuffering = true;
        App.Settings.Update(s => s with { LastRadioStationId = station.Id });
        Changed?.Invoke();

        var url = await ResolveStreamUrlAsync(station.StreamUrl);
        if (Kind != MediaKind.Radio || CurrentStation?.Id != station.Id) return;
        _ui.TryEnqueue(() =>
        {
            try
            {
                _player.Source = MediaSource.CreateFromUri(url);
                _player.PlaybackSession.PlaybackRate = 1.0;
                _player.Volume = AppPaths.Muted ? 0 : 1.0;
                _player.Play();
                UpdateSmtc(station.Name(Loc.Current.Language), station.Location(Loc.Current.Language));
            }
            catch (Exception e)
            {
                Log.Error("radio play", e);
                Error = e.Message;
                IsBuffering = false;
                Changed?.Invoke();
            }
        });
    }

    private static async Task<Uri> ResolveStreamUrlAsync(string raw)
    {
        var uri = new Uri(raw);
        if (!raw.Contains("radiojar.com", StringComparison.OrdinalIgnoreCase)) return uri;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if ((int)resp.StatusCode is >= 300 and < 400 && resp.Headers.Location is { } loc)
            {
                var target = loc.IsAbsoluteUri ? loc : new Uri(uri, loc);
                if (target.Scheme == Uri.UriSchemeHttp)
                {
                    var b = new UriBuilder(target) { Scheme = Uri.UriSchemeHttps, Port = -1 };
                    return b.Uri;
                }
                return target;
            }
        }
        catch { }
        return uri;
    }

    private void OpenCurrentTrack()
    {
        var track = CurrentTrack;
        if (track == null) return;
        IsBuffering = true;
        Error = null;
        try
        {
            _player.Source = MediaSource.CreateFromUri(track.Url);
            _player.PlaybackSession.PlaybackRate = Speed;
            _player.Volume = AppPaths.Muted ? 0 : 1.0;
            _player.Play();
            if (track.ReciterId != null && track.SurahNumber is { } s)
                App.Settings.Update(st => st with { LastListenReciterId = track.ReciterId, LastListenSurah = s });
            UpdateSmtc(track.Title, track.Subtitle);
        }
        catch (Exception e)
        {
            Log.Error("listen play", e);
            Error = e.Message;
            IsBuffering = false;
        }
        Changed?.Invoke();
    }

    private void UpdateSmtc(string title, string artist)
    {
        try
        {
            var smtc = _player.SystemMediaTransportControls;
            var updater = smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = title;
            updater.MusicProperties.Artist = artist;
            updater.Update();
        }
        catch { }
    }

    public void TogglePlayPause()
    {
        if (!HasMedia) return;
        if (IsPlaying) Pause();
        else
        {
            App.QuranPlayer.Stop();
            _player.Play();
        }
    }

    public void Pause()
    {
        _player.Pause();
        if (_ambientPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing) _ambientPlayer.Pause();
        IsPlaying = false;
        Changed?.Invoke();
    }

    public void Stop()
    {
        _player.Pause();
        _player.Source = null;
        _ambientPlayer.Pause();
        Kind = MediaKind.None;
        CurrentStation = null;
        _queue.Clear();
        _queueIndex = -1;
        IsPlaying = false;
        IsBuffering = false;
        Error = null;
        _sleepAt = null;
        _sleepEndOfTrack = false;
        Changed?.Invoke();
    }

    public void Next(bool auto = false)
    {
        if (Kind != MediaKind.Listen || _queue.Count == 0) return;
        if (Shuffle && _queue.Count > 1)
        {
            _queueIndex = Random.Shared.Next(_queue.Count);
            OpenCurrentTrack();
            return;
        }
        if (_queueIndex + 1 < _queue.Count)
        {
            _queueIndex++;
            OpenCurrentTrack();
        }
        else if (Repeat == RepeatMode.All || !auto)
        {
            _queueIndex = 0;
            OpenCurrentTrack();
        }
        else
        {
            IsPlaying = false;
            Changed?.Invoke();
        }
    }

    public void Previous()
    {
        if (Kind != MediaKind.Listen || _queue.Count == 0) return;
        if (Position.TotalSeconds > 4)
        {
            Seek(TimeSpan.Zero);
            return;
        }
        _queueIndex = (_queueIndex - 1 + _queue.Count) % _queue.Count;
        OpenCurrentTrack();
    }

    public void Seek(TimeSpan to)
    {
        if (Kind != MediaKind.Listen || Duration <= TimeSpan.Zero) return;
        var clamped = TimeSpan.FromMilliseconds(Math.Clamp(to.TotalMilliseconds, 0, Duration.TotalMilliseconds));
        _player.PlaybackSession.Position = clamped;
        PositionTick?.Invoke();
    }

    public void SeekBy(int seconds) => Seek(Position + TimeSpan.FromSeconds(seconds));

    public void ToggleShuffle()
    {
        Shuffle = !Shuffle;
        Changed?.Invoke();
    }

    public void CycleRepeat()
    {
        Repeat = Repeat switch { RepeatMode.Off => RepeatMode.All, RepeatMode.All => RepeatMode.One, _ => RepeatMode.Off };
        Changed?.Invoke();
    }

    public void SetSpeed(double speed)
    {
        Speed = Math.Clamp(speed, 0.75, 2.0);
        if (Kind == MediaKind.Listen) _player.PlaybackSession.PlaybackRate = Speed;
        Changed?.Invoke();
    }

    public void SetSleepTimer(int? minutes, bool endOfTrack = false)
    {
        _sleepEndOfTrack = endOfTrack;
        _sleepAt = minutes is > 0 ? DateTimeOffset.Now.AddMinutes(minutes.Value) : null;
        Changed?.Invoke();
    }

    public async void SetAmbient(string? soundId)
    {
        if (string.IsNullOrEmpty(soundId) || soundId == ActiveAmbientId)
        {
            ActiveAmbientId = null;
            AmbientLoading = false;
            _ambientPlayer.Pause();
            _ambientPlayer.Source = null;
            App.Settings.Update(s => s with { AmbientSound = null });
            Changed?.Invoke();
            return;
        }
        var info = ListeningLibrary.AmbientSounds.FirstOrDefault(a => a.Id == soundId);
        if (info == null) return;
        ActiveAmbientId = soundId;
        AmbientLoading = true;
        App.Settings.Update(s => s with { AmbientSound = soundId });
        Changed?.Invoke();

        try
        {
            var dir = Path.Combine(AppPaths.Data, "ambient");
            Directory.CreateDirectory(dir);
            var local = Path.Combine(dir, info.FileName);
            if (!File.Exists(local))
            {
                var bytes = await DownloadHttp.GetByteArrayAsync(info.Url);
                var tmp = local + ".tmp";
                await File.WriteAllBytesAsync(tmp, bytes);
                File.Move(tmp, local, overwrite: true);
            }
            if (ActiveAmbientId != soundId) return;
            _ui.TryEnqueue(() =>
            {
                AmbientLoading = false;
                _ambientPlayer.Source = MediaSource.CreateFromUri(new Uri(local));
                _ambientPlayer.Volume = AppPaths.Muted ? 0 : AmbientVolume;
                if (IsPlaying || !HasMedia) _ambientPlayer.Play();
                Changed?.Invoke();
            });
        }
        catch (Exception e)
        {
            Log.Error("ambient download", e);
            _ui.TryEnqueue(() =>
            {
                AmbientLoading = false;
                ActiveAmbientId = null;
                Changed?.Invoke();
            });
        }
    }

    public void SetAmbientVolume(double volume)
    {
        AmbientVolume = Math.Clamp(volume, 0.05, 1.0);
        _ambientPlayer.Volume = AppPaths.Muted ? 0 : AmbientVolume;
        App.Settings.Update(s => s with { AmbientVolume = AmbientVolume });
    }

    public static string SurahTitle(int number, string language)
    {
        if (App.Quran.LoadedDatabase is { } db)
        {
            var s = db.Surah(number);
            return language is "ar" or "ur" ? s.Name : s.EnglishName;
        }
        return Loc.Current.Format("dashboard_surah_fallback", Loc.Current.Number(number));
    }

    public void Dispose()
    {
        _tick.Stop();
        _player.Dispose();
        _ambientPlayer.Dispose();
    }
}
