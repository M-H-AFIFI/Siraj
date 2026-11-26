using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using Siraj.App.Services;
using Siraj.Core.Theme;

namespace Siraj.App.Theme;

internal static class SceneThumbCache
{
    private const int ThumbWidth = 240;
    private const int ThumbHeight = 148;
    private static readonly Dictionary<string, WriteableBitmap> Cache = new(StringComparer.Ordinal);
    private static string _paletteKey = "";

    public static WriteableBitmap? Get(string sceneId)
    {
        var s = App.Settings.Current;
        var scheme = App.Theme.Scheme;
        var now = DateTime.Now;
        int mins = now.Hour * 60 + now.Minute;
        // Bucket sky-sync minutes in 15-minute steps so thumbnails stay cached across minor ticks.
        int bucket = s.LiveSkySync ? (mins / 15) * 15 : 0;
        string pKey = $"{s.Palette}|{scheme.IsDark}|{s.BlackMode}|{s.LiveIntensity}|{s.LiveSkySync}|{bucket}";
        if (pKey != _paletteKey)
        {
            Cache.Clear();
            _paletteKey = pKey;
        }
        if (Cache.TryGetValue(sceneId, out var existing)) return existing;

        try
        {
            var device = CanvasDevice.GetSharedDevice();
            var intensity = LiveScenes.FindIntensity(s.LiveIntensity);
            var colors = LiveScenes.ColorsFor(scheme, s.BlackMode, s.LiveSkySync, mins);

            using var rt = new CanvasRenderTarget(device, ThumbWidth, ThumbHeight, 96f);
            using (var ds = rt.CreateDrawingSession())
            {
                LiveSceneRenderer.DrawBackdrop(ds, device, ThumbWidth, ThumbHeight, sceneId, colors, intensity,
                    App.Theme.Glass, liveEnabled: true, glassEnabled: false, s.LiveSkySync, mins, t: 5.4f, drawVeil: false);
            }

            byte[] bytes = rt.GetPixelBytes();
            var bmp = new WriteableBitmap(ThumbWidth, ThumbHeight);
            using (var stream = bmp.PixelBuffer.AsStream())
            {
                stream.Write(bytes, 0, bytes.Length);
            }
            bmp.Invalidate();
            Cache[sceneId] = bmp;
            return bmp;
        }
        catch (Exception e)
        {
            Log.Error($"scene thumb {sceneId}", e);
            return null;
        }
    }
}
