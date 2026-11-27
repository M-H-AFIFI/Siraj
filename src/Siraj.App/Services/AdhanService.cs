using Microsoft.UI.Dispatching;
using Siraj.Core.Adhan;
using Siraj.Core.Prayer;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Siraj.App.Services;

internal sealed class AdhanService : IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Timer _timer;
    private readonly HashSet<string> _fired = [];
    private IReadOnlyList<PlannedAlert> _plan = [];
    private MediaPlayer? _player;

    public event Action<PrayerType?>? PlayingChanged;

    public PrayerType? Playing { get; private set; }

    public AdhanService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        _timer = new Timer(_ => _dispatcher.TryEnqueue(Check), null, Timeout.Infinite, Timeout.Infinite);
        App.Settings.Changed += (_, _) => _dispatcher.TryEnqueue(Replan);
        Replan();
    }

    public IReadOnlyList<PlannedAlert> Plan => _plan;

    public void Replan()
    {
        var settings = App.Settings.Current;
        _plan = settings.Location == null ? [] : AlertPlanner.Plan(settings, settings.Location, DateTimeOffset.Now);
        // Prune expired alert keys from _fired.
        _fired.RemoveWhere(k => !_plan.Any(a => a.Key == k));
        Arm();
    }

    private void Arm()
    {
        var now = DateTimeOffset.Now;
        var next = _plan.FirstOrDefault(a => !_fired.Contains(a.Key));
        var wait = TimeSpan.FromMinutes(1);
        if (next != null && next.At - now < wait) wait = next.At - now;
        if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
        _timer.Change(wait + TimeSpan.FromMilliseconds(30), Timeout.InfiniteTimeSpan);
    }

    private void Check()
    {
        try
        {
            var now = DateTimeOffset.Now;
            // Refresh the plan before checking if it is empty or near its horizon.
            if (_plan.Count == 0 || _plan[^1].At < now.AddHours(20)) Replan();
            foreach (var alert in _plan)
            {
                if (alert.At > now) break;
                if (_fired.Contains(alert.Key)) continue;
                _fired.Add(alert.Key);
                if (now - alert.At > AlertPlanner.Grace)
                {
                    Log.Info($"skipped {alert.Key}: {(now - alert.At).TotalMinutes:F1} min late");
                    continue;
                }
                Fire(alert);
            }
        }
        catch (Exception e)
        {
            Log.Error("adhan check failed", e);
        }
        finally
        {
            Arm();
        }
    }

    private void Fire(PlannedAlert alert)
    {
        var settings = App.Settings.Current;
        var loc = Loc.Current;
        Log.Info($"alert {alert.Key} ({settings.AlertType})");
        if (alert.Kind == AlertKind.Reminder)
        {
            int minutes = (int)Math.Round((alert.PrayerTime - alert.At).TotalMinutes);
            Notifier.ShowReminder(alert.Prayer, minutes, alert.PrayerTime);
            return;
        }
        if (alert.Prayer == PrayerType.Sunrise)
        {
            Notifier.ShowPrayer(alert.Prayer, alert.PrayerTime, sound: settings.AlertType != "silent", adhan: false);
            return;
        }
        bool adhan = settings.AlertType == "azan";
        Notifier.ShowPrayer(alert.Prayer, alert.PrayerTime, sound: settings.AlertType == "notification", adhan: adhan);
        if (adhan)
        {
            var voice = AdhanVoices.Find(settings.AdhanVoice);
            var file = AdhanVoices.FileFor(voice, alert.Prayer == PrayerType.Fajr, settings.FajrAdhan, AppPaths.Asset("Adhan"), VoiceDownloads.Folder);
            Play(file, voice, alert.Prayer, loc.PrayerName(new PrayerTime(alert.Prayer, alert.PrayerTime)));
            if (settings.AdhanWindow) Views.AdhanAlertWindow.ShowFor(alert.Prayer, alert.PrayerTime);
        }
    }

    public void Test(PrayerType prayer)
    {
        var now = DateTimeOffset.Now;
        Fire(new PlannedAlert(prayer, AlertKind.Prayer, now, now));
    }

    public void Preview(AdhanVoice voice) =>
        Play(AdhanVoices.FileFor(voice, false, false, AppPaths.Asset("Adhan"), VoiceDownloads.Folder), voice, null, voice.Name(Loc.Current.Language));

    public void Play(string path, AdhanVoice voice, PrayerType? prayer, string title)
    {
        Stop();
        if (!File.Exists(path))
        {
            Log.Error($"adhan file missing: {path}");
            path = AppPaths.Asset("Adhan", voice.Bundled);
            if (!File.Exists(path)) return;
        }
        var player = new MediaPlayer
        {
            Volume = AppPaths.Muted ? 0 : Math.Clamp(App.Settings.Current.AdhanVolume, 0, 100) / 100.0,
            AudioCategory = MediaPlayerAudioCategory.Alerts,
        };
        // Populate SMTC metadata so Windows media controls and media keys can stop the adhan.
        var item = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(path)));
        var display = item.GetDisplayProperties();
        display.Type = MediaPlaybackType.Music;
        display.MusicProperties.Title = title;
        display.MusicProperties.Artist = voice.Name(Loc.Current.Language);
        item.ApplyDisplayProperties(display);
        player.Source = item;
        player.CommandManager.IsEnabled = true;
        player.CommandManager.PauseReceived += (_, _) => _dispatcher.TryEnqueue(Stop);
        player.MediaEnded += (_, _) => _dispatcher.TryEnqueue(() => Finished(player));
        player.MediaFailed += (_, e) => _dispatcher.TryEnqueue(() =>
        {
            Log.Error($"adhan playback failed: {e.Error} {e.ErrorMessage}");
            Finished(player);
        });
        _player = player;
        Playing = prayer;
        player.Play();
        PlayingChanged?.Invoke(prayer);
    }

    public bool IsPlaying => _player != null;

    private void Finished(MediaPlayer player)
    {
        if (!ReferenceEquals(player, _player)) return;
        Stop();
    }

    public void Stop()
    {
        var player = _player;
        if (player == null) return;
        _player = null;
        Playing = null;
        try
        {
            player.Pause();
            player.Dispose();
        }
        catch (Exception e)
        {
            Log.Error("stopping the adhan", e);
        }
        PlayingChanged?.Invoke(null);
    }

    public void Dispose()
    {
        _timer.Dispose();
        Stop();
    }
}
