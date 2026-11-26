using Microsoft.UI.Dispatching;
using Siraj.App.Services;
using Siraj.Core.Quran;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Siraj.App.Quran;

internal sealed class QuranPlayer
{
    private readonly DispatcherQueue _dispatcher;
    private MediaPlayer? _player;
    private MediaPlaybackList? _list;
    private IReadOnlyList<(int Ayah, long From, long To)> _segments = [];
    private int _surah;
    private int _firstAyah;

    public (int Surah, int Ayah)? Current { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsActive => _player != null;
    public string? Error { get; private set; }

    public event Action? Changed;

    public QuranPlayer(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        // The adhan comes first.
        App.Adhan.PlayingChanged += prayer =>
        {
            if (prayer != null && IsPlaying) Pause();
        };
    }

    private static Reciter Reciter => Reciters.Find(App.Settings.Current.ReciterId);

    public void Play(int surah, int ayah)
    {
        Stop();
        var db = App.Quran.Database;
        var reciter = Reciter;
        _surah = surah;
        _firstAyah = ayah;
        Error = null;
        var player = new MediaPlayer { AudioCategory = MediaPlayerAudioCategory.Media, AutoPlay = true, Volume = AppPaths.Muted ? 0 : 1 };
        var surahInfo = db.Surah(surah);
        string Title(int a) => $"{surahInfo.Name} {Loc.Current.Number(a)}";

        if (reciter.Mode == ReciterMode.AyahByAyah && reciter.EveryAyahFolder != null)
        {
            var list = new MediaPlaybackList { MaxPrefetchTime = TimeSpan.FromSeconds(30) };
            for (int a = ayah; a <= surahInfo.AyahCount; a++)
            {
                var item = new MediaPlaybackItem(MediaSource.CreateFromUri(Reciters.AyahUrl(reciter, surah, a)));
                var display = item.GetDisplayProperties();
                display.Type = MediaPlaybackType.Music;
                display.MusicProperties.Title = Title(a);
                display.MusicProperties.Artist = reciter.Name(Loc.Current.Language);
                item.ApplyDisplayProperties(display);
                list.Items.Add(item);
            }
            list.CurrentItemChanged += (l, _) =>
            {
                int index = (int)l.CurrentItemIndex;
                if (index >= 0 && index < l.Items.Count) _dispatcher.TryEnqueue(() => SetCurrent(surah, _firstAyah + index));
            };
            _list = list;
            player.Source = list;
        }
        else
        {
            // A surah recording: the ayah is found from the timings, where the database has them.
            _segments = db.Segments(reciter.Id, surah);
            var item = new MediaPlaybackItem(MediaSource.CreateFromUri(Reciters.SurahUrl(reciter, surah)));
            var display = item.GetDisplayProperties();
            display.Type = MediaPlaybackType.Music;
            display.MusicProperties.Title = surahInfo.Name;
            display.MusicProperties.Artist = reciter.Name(Loc.Current.Language);
            item.ApplyDisplayProperties(display);
            player.Source = item;
            var start = _segments.FirstOrDefault(s => s.Ayah == ayah);
            if (start.To > 0) player.PlaybackSession.Position = TimeSpan.FromMilliseconds(start.From);
            player.PlaybackSession.PositionChanged += (session, _) =>
            {
                long ms = (long)session.Position.TotalMilliseconds;
                var at = _segments.LastOrDefault(s => s.From <= ms);
                if (at.To > 0) _dispatcher.TryEnqueue(() => SetCurrent(surah, at.Ayah));
            };
            SetCurrent(surah, ayah);
        }

        player.MediaEnded += (_, _) => _dispatcher.TryEnqueue(Ended);
        player.MediaFailed += (_, e) => _dispatcher.TryEnqueue(() =>
        {
            Log.Error($"recitation failed: {e.Error} {e.ErrorMessage}");
            Error = Loc.Current["win_recitation_failed"];
            Stop();
        });
        player.CommandManager.IsEnabled = true;
        player.CommandManager.NextReceived += (_, a) => { a.Handled = true; _dispatcher.TryEnqueue(Next); };
        player.CommandManager.PreviousReceived += (_, a) => { a.Handled = true; _dispatcher.TryEnqueue(Previous); };
        _player = player;
        IsPlaying = true;
        if (_list == null) player.Play();
        Changed?.Invoke();
    }

    private void SetCurrent(int surah, int ayah)
    {
        if (Current == (surah, ayah)) return;
        Current = (surah, ayah);
        Changed?.Invoke();
    }

    private void Ended()
    {
        if (_player == null) return;
        // The list ends after the last ayah; a surah recording ends with the surah.
        if (_list != null && _list.CurrentItemIndex + 1 < _list.Items.Count) return;
        int surah = _surah;
        Stop();
        if (App.Settings.Current.QuranContinuous && surah < 114) Play(surah + 1, 1);
    }

    public void Pause()
    {
        _player?.Pause();
        IsPlaying = false;
        Changed?.Invoke();
    }

    public void Resume()
    {
        if (_player == null) return;
        _player.Play();
        IsPlaying = true;
        Changed?.Invoke();
    }

    public void Next()
    {
        if (Current is not { } at) return;
        var count = App.Quran.Database.Surah(at.Surah).AyahCount;
        if (at.Ayah < count) Seek(at.Surah, at.Ayah + 1);
        else if (at.Surah < 114) Play(at.Surah + 1, 1);
    }

    public void Previous()
    {
        if (Current is not { } at) return;
        if (at.Ayah > 1) Seek(at.Surah, at.Ayah - 1);
        else if (at.Surah > 1) Play(at.Surah - 1, App.Quran.Database.Surah(at.Surah - 1).AyahCount);
    }

    private void Seek(int surah, int ayah)
    {
        if (_player == null || surah != _surah) { Play(surah, ayah); return; }
        if (_list != null)
        {
            int index = ayah - _firstAyah;
            if (index < 0 || index >= _list.Items.Count) { Play(surah, ayah); return; }
            _list.MoveTo((uint)index);
            SetCurrent(surah, ayah);
        }
        else
        {
            var at = _segments.FirstOrDefault(s => s.Ayah == ayah);
            if (at.To > 0) _player.PlaybackSession.Position = TimeSpan.FromMilliseconds(at.From);
        }
    }

    public void Stop()
    {
        var player = _player;
        _player = null;
        _list = null;
        _segments = [];
        bool was = Current != null || IsPlaying;
        Current = null;
        IsPlaying = false;
        if (player != null)
        {
            try
            {
                player.Pause();
                player.Dispose();
            }
            catch (Exception e)
            {
                Log.Error("stopping the recitation", e);
            }
        }
        if (was) Changed?.Invoke();
    }
}
