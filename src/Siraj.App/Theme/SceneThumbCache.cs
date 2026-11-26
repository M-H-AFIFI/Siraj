using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.UI.Xaml.Media.Imaging;
using Siraj.App.Services;
using Siraj.Core.Theme;
using Windows.UI;

namespace Siraj.App.Theme;

internal static class SceneThumbCache
{
    private const int ThumbWidth = 260;
    private const int ThumbHeight = 164;
    private static readonly Dictionary<string, WriteableBitmap> Cache = new(StringComparer.Ordinal);
    private static string _paletteKey = "";

    public static WriteableBitmap? Get(string sceneId)
    {
        var s = App.Settings.Current;
        var scheme = App.Theme.Scheme;
        var now = DateTime.Now;
        int mins = now.Hour * 60 + now.Minute;
        int bucket = s.LiveSkySync ? (mins / 15) * 15 : 0;
        string pKey = $"{s.Palette}|{scheme.IsDark}|{s.LiveSkySync}|{bucket}";
        if (pKey != _paletteKey)
        {
            Cache.Clear();
            _paletteKey = pKey;
        }
        if (Cache.TryGetValue(sceneId, out var existing)) return existing;

        try
        {
            var device = CanvasDevice.GetSharedDevice();
            var vivid = LiveScenes.FindIntensity("Vivid");
            // Keep small picker tiles rich and readable even when True Black is enabled on the main window.
            var thumbScheme = s.BlackMode && scheme.IsDark ? Palettes.Scheme(s.Palette, dark: true, black: false) : scheme;
            var colors = LiveScenes.ColorsFor(thumbScheme, blackMode: false, s.LiveSkySync, mins);

            using var rt = new CanvasRenderTarget(device, ThumbWidth, ThumbHeight, 96f);
            using (var ds = rt.CreateDrawingSession())
            {
                LiveSceneRenderer.DrawBackdrop(ds, device, ThumbWidth, ThumbHeight, sceneId, colors, vivid,
                    App.Theme.Glass, liveEnabled: true, glassEnabled: false, s.LiveSkySync, mins,
                    t: LiveScenes.RestSeconds, drawVeil: false);

                // Bottom scrim baked into the bitmap so tile labels stay legible without an extra XAML gradient layer.
                using var scrim = new CanvasLinearGradientBrush(device, [
                    new CanvasGradientStop { Position = 0f, Color = Color.FromArgb(0, 8, 12, 18) },
                    new CanvasGradientStop { Position = 1f, Color = Color.FromArgb(190, 8, 12, 18) },
                ]) { StartPoint = new Vector2(0, ThumbHeight * 0.48f), EndPoint = new Vector2(0, ThumbHeight) };
                ds.FillRectangle(0, ThumbHeight * 0.48f, ThumbWidth, ThumbHeight * 0.52f, scrim);
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
