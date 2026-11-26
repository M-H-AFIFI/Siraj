using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Siraj.App.Services;
using Siraj.Core.Quran;
using Windows.Foundation;
using Windows.UI;

namespace Siraj.App.Quran;

internal sealed class QcfFace : IDisposable
{
    private readonly CanvasFontSet _set;
    private readonly Dictionary<char, (int Index, float Advance, Rect Ink)> _glyphs = new();

    public CanvasFontFace Face { get; }
    public float Ascent { get; }
    public float Descent { get; }

    public QcfFace(string path)
    {
        _set = new CanvasFontSet(new Uri(path));
        if (_set.Fonts.Count == 0) throw new InvalidDataException($"No font in {path}");
        Face = _set.Fonts[0];
        Ascent = Face.Ascent;
        Descent = Face.Descent;
    }

    private (int Index, float Advance, Rect Ink) Glyph(char c)
    {
        // A page is built off the UI thread and drawn on it.
        lock (_glyphs)
        {
            if (_glyphs.TryGetValue(c, out var g)) return g;
            int index = Face.GetGlyphIndices([c])[0];
            var metrics = Face.GetGlyphMetrics([index], false)[0];
            return _glyphs[c] = (index, metrics.AdvanceWidth, metrics.DrawBounds);
        }
    }

    public float Advance(char c) => Glyph(c).Advance;

    public float Advance(string glyphs)
    {
        float total = 0;
        foreach (var c in glyphs) total += Advance(c);
        return total;
    }

    public int Index(char c) => Glyph(c).Index;

    public (float Height, float Centre) Ink(string glyphs)
    {
        double top = double.MaxValue, bottom = double.MinValue;
        foreach (var c in glyphs)
        {
            var ink = Glyph(c).Ink;
            if (ink.Height <= 0) continue;
            top = Math.Min(top, ink.Y);
            bottom = Math.Max(bottom, ink.Y + ink.Height);
        }
        if (top == double.MaxValue) return (0, 0);
        // Win2D measures a glyph's draw bounds down from the top of the ascent, not from the baseline:
        // taken as is, they put the Basmalah half a line high and the surah band over the top of the page.
        return ((float)(bottom - top), (float)((top + bottom) / 2 - Ascent));
    }

    public void Dispose() => _set.Dispose();
}

internal sealed record PageLayout(MushafPage Page, QcfFace Face, IReadOnlyList<LayoutLine> Lines, float ColumnEm, int FirstLine, int LastLine);

internal sealed record LayoutLine(MushafLine Line, IReadOnlyList<(MushafWord Word, string Glyphs, float Em, bool IsMarker)> Words, float Em);

internal sealed record PlacedWord(MushafWord Word, string Glyphs, bool IsMarker, float Left, float Right, float Top, float Bottom, float Baseline, float[] GlyphX, float Size);

internal sealed record PlacedRun(QcfFace Face, string Glyphs, float[] GlyphX, float Baseline, float Size);

internal sealed record PlacedPage(IReadOnlyList<PlacedWord> Words, IReadOnlyList<PlacedRun> Ornaments, float Width, float Height);

internal sealed record MushafColors(Color Paper, Color Ink, Color PlayingHighlight, Color SelectedHighlight, bool IsDark)
{
    public static MushafColors For(string theme, string paper, bool appDark, bool black, Color accent)
    {
        bool dark = theme switch { "Dark" => true, "Light" => false, _ => appDark };
        if (dark)
        {
            return black
                ? new(Color.FromArgb(255, 0, 0, 0), Color.FromArgb(255, 255, 255, 255), Color.FromArgb(64, 0xD4, 0xAF, 0x37), WithAlpha(accent, 70), true)
                : new(Color.FromArgb(255, 0x1E, 0x1E, 0x22), Color.FromArgb(255, 0xE2, 0xE2, 0xE6), Color.FromArgb(64, 0xD4, 0xAF, 0x37), WithAlpha(accent, 70), true);
        }
        var (bg, ink) = paper switch
        {
            "White" => (Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 0x11, 0x11, 0x11)),
            "Sepia" => (Color.FromArgb(255, 0xF4, 0xEC, 0xD8), Color.FromArgb(255, 0x2C, 0x24, 0x1B)),
            _ => (Color.FromArgb(255, 0xFA, 0xF6, 0xEB), Color.FromArgb(255, 0x1E, 0x1B, 0x18)),
        };
        return new(bg, ink, Color.FromArgb(72, 0xE5, 0xC1, 0x58), WithAlpha(accent, 46), false);
    }

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);
}

internal static class MushafRenderer
{
    private const float SideMargin = 0.045f;
    private const float MaxSlotFill = 0.80f;
    private const float BasmalahColumnFraction = 0.52f;
    private const float DecoratedSlotFill = 0.94f;
    private const float BannerColumnFraction = 0.98f;
    private const int NominalLines = 15;

    public const int KeptPages = 16;

    private static readonly LinkedList<(int Page, QcfFace Face)> Faces = new();
    private static QcfFace? _basmalah, _banner;
    private static readonly object Gate = new();

    private static QcfFace PageFace(QuranAssets assets, int page)
    {
        lock (Gate)
        {
            var node = Faces.First;
            while (node != null)
            {
                if (node.Value.Page == page)
                {
                    Faces.Remove(node);
                    Faces.AddFirst(node);
                    return node.Value.Face;
                }
                node = node.Next;
            }
            var face = new QcfFace(assets.PageFont(page));
            Faces.AddFirst((page, face));
            // A reader keeps a handful of pages near; more fonts held is waste.
            while (Faces.Count > KeptPages)
            {
                Faces.Last!.Value.Face.Dispose();
                Faces.RemoveLast();
            }
            return face;
        }
    }

    public static QcfFace Basmalah(QuranAssets assets) { lock (Gate) return _basmalah ??= new QcfFace(assets.BasmalahFont); }

    public static QcfFace Banner() { lock (Gate) return _banner ??= new QcfFace(AppPaths.Asset("Quran", "quran_surah_header.ttf")); }

    public static PageLayout? Build(MushafPage page, QcfGlyphs table, QuranAssets assets)
    {
        var glyphs = table.GlyphsFor(page);
        if (glyphs == null) return null;
        var face = PageFace(assets, page.Number);
        int i = 0;
        var lines = new List<LayoutLine>();
        foreach (var line in page.Lines.Where(l => l.Type != MushafLineType.Empty).OrderBy(l => l.LineNumber))
        {
            var words = line.Words.Select(w =>
            {
                var g = glyphs[i++];
                return (w, g, face.Advance(g), QcfGlyphs.IsAyahMarker(w.Text));
            }).ToList();
            lines.Add(new LayoutLine(line, words, words.Sum(w => w.Item3)));
        }
        var full = lines.Where(l => l.Words.Count > 0 && !l.Line.IsCentered).Select(l => l.Em).DefaultIfEmpty(0).Max();
        float column = full > 0 ? full : lines.Where(l => l.Words.Count > 0).Select(l => l.Em).DefaultIfEmpty(0).Max();
        if (column <= 0) return null;
        return new PageLayout(page, face, lines, column, lines.Min(l => l.Line.LineNumber), lines.Max(l => l.Line.LineNumber));
    }

    public static PlacedPage Place(PageLayout layout, QuranAssets assets, float width, float height)
    {
        float side = width * SideMargin;
        float column = width - 2 * side;
        int slots = Math.Max(NominalLines, layout.LastLine);
        float slotHeight = height / slots;
        float size = Math.Min(column / layout.ColumnEm, slotHeight * MaxSlotFill);
        float block = layout.ColumnEm * size;
        float columnRight = (width + block) / 2;
        int used = layout.LastLine - layout.FirstLine + 1;
        float topOffset = (height - used * slotHeight) / 2;
        var face = layout.Face;
        var words = new List<PlacedWord>();
        var ornaments = new List<PlacedRun>();

        foreach (var line in layout.Lines)
        {
            float slotTop = topOffset + (line.Line.LineNumber - layout.FirstLine) * slotHeight;
            float slotCentre = slotTop + slotHeight / 2;
            if (line.Line.Type == MushafLineType.SurahName && line.Line.Surah is { } surah)
            {
                var banner = Banner();
                var glyph = QcfGlyphs.SurahBanner(surah);
                float em = banner.Advance(glyph);
                if (em <= 0) continue;
                var ink = banner.Ink(glyph);
                float s = ink.Height > 0 ? Math.Min(block * BannerColumnFraction / em, slotHeight * DecoratedSlotFill / ink.Height) : block * BannerColumnFraction / em;
                ornaments.Add(new PlacedRun(banner, glyph, [(width - em * s) / 2], slotCentre - ink.Centre * s, s));
            }
            else if (line.Line.Type == MushafLineType.Basmallah && line.Words.Count == 0)
            {
                var bsml = Basmalah(assets);
                var glyphsText = QcfGlyphs.BasmalahGlyphs;
                float em = bsml.Advance(glyphsText);
                if (em <= 0) continue;
                var ink = bsml.Ink(glyphsText);
                float s = ink.Height > 0 ? Math.Min(block * BasmalahColumnFraction / em, slotHeight * DecoratedSlotFill / ink.Height) : block * BasmalahColumnFraction / em;
                float x = (width + em * s) / 2;
                var xs = new float[glyphsText.Length];
                for (int k = 0; k < glyphsText.Length; k++)
                {
                    x -= bsml.Advance(glyphsText[k]) * s;
                    xs[k] = x;
                }
                ornaments.Add(new PlacedRun(bsml, glyphsText, xs, slotCentre - ink.Centre * s, s));
            }
            else if (line.Words.Count > 0)
            {
                float stretch = line.Line.IsCentered || line.Em <= 0 ? 1 : layout.ColumnEm / line.Em;
                float lineWidth = line.Em * stretch * size;
                float x = line.Line.IsCentered ? (width + lineWidth) / 2 : columnRight;
                float baseline = slotCentre + (face.Ascent - face.Descent) / 2 * size;
                foreach (var (word, glyphs, _, marker) in line.Words)
                {
                    float right = x;
                    var xs = new float[glyphs.Length];
                    for (int k = 0; k < glyphs.Length; k++)
                    {
                        x -= face.Advance(glyphs[k]) * stretch * size;
                        xs[k] = x;
                    }
                    words.Add(new PlacedWord(word, glyphs, marker, x, right, slotTop, slotTop + slotHeight, baseline, xs, size));
                }
            }
        }
        return new PlacedPage(words, ornaments, width, height);
    }

    public static void Draw(CanvasDrawingSession ds, PlacedPage page, PageLayout layout, MushafColors colors,
        (int Surah, int Ayah)? playing, (int Surah, int Ayah)? selected)
    {
        void Highlight((int Surah, int Ayah)? ayah, Color colour)
        {
            if (ayah == null) return;
            foreach (var w in page.Words)
            {
                if (w.Word.Surah != ayah.Value.Surah || w.Word.Ayah != ayah.Value.Ayah) continue;
                float inset = (w.Bottom - w.Top) * 0.10f;
                ds.FillRoundedRectangle(new Rect(w.Left - 1, w.Top + inset, w.Right - w.Left + 2, w.Bottom - w.Top - 2 * inset), 4, 4, colour);
            }
        }
        Highlight(playing, colors.PlayingHighlight);
        if (selected != playing) Highlight(selected, colors.SelectedHighlight);

        using var ink = new CanvasSolidColorBrush(ds, colors.Ink);
        foreach (var w in page.Words)
        {
            for (int k = 0; k < w.Glyphs.Length; k++)
            {
                var glyph = new CanvasGlyph { Index = layout.Face.Index(w.Glyphs[k]), Advance = 0 };
                ds.DrawGlyphRun(new Vector2(w.GlyphX[k], w.Baseline), layout.Face.Face, w.Size, [glyph], false, 0, ink);
            }
        }
        foreach (var run in page.Ornaments)
        {
            for (int k = 0; k < run.Glyphs.Length; k++)
            {
                var glyph = new CanvasGlyph { Index = run.Face.Index(run.Glyphs[k]), Advance = 0 };
                ds.DrawGlyphRun(new Vector2(run.GlyphX[Math.Min(k, run.GlyphX.Length - 1)], run.Baseline), run.Face.Face, run.Size, [glyph], false, 0, ink);
            }
        }
    }

    public static (int Surah, int Ayah)? HitTest(PlacedPage page, float x, float y)
    {
        PlacedWord? best = null;
        float bestDistance = float.MaxValue;
        foreach (var w in page.Words)
        {
            if (y < w.Top || y > w.Bottom || w.Word.Surah == null || w.Word.Ayah == null) continue;
            float d = x < w.Left ? w.Left - x : x > w.Right ? x - w.Right : 0;
            if (d < bestDistance) { bestDistance = d; best = w; }
        }
        return best != null && bestDistance < 40 ? (best.Word.Surah!.Value, best.Word.Ayah!.Value) : null;
    }
}
