using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace Siraj.App.Controls;

public sealed class RollingText : UserControl
{
    private const int RollMs = 175;

    private readonly StackPanel _line = new() { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight };
    private readonly List<Slot> _slots = [];
    private string _text = "";

    public RollingText()
    {
        Content = _line;
        IsTabStop = false;
    }

    public string Text
    {
        get => _text;
        set
        {
            string shown = new((value ?? "").Where(c => c is not ('\u200E' or '\u200F' or '\u061C')).ToArray());
            if (shown == _text) return;
            bool roll = Kit.Motion && IsLoaded && shown.Length == _text.Length && SameShape(shown, _text);
            _text = shown;
            AutomationProperties.SetName(this, value ?? "");
            if (!roll)
            {
                Rebuild();
                return;
            }
            for (int i = 0; i < _slots.Count; i++) _slots[i].Show(shown[i].ToString());
        }
    }

    private static bool SameShape(string a, string b)
    {
        for (int i = 0; i < a.Length; i++)
            if (char.IsDigit(a[i]) != char.IsDigit(b[i])) return false;
        return true;
    }

    private void Rebuild()
    {
        _line.Children.Clear();
        _slots.Clear();
        foreach (char c in _text)
        {
            var slot = new Slot(c.ToString());
            _slots.Add(slot);
            _line.Children.Add(slot.Host);
        }
    }

    private sealed class Slot
    {
        public readonly Grid Host = new();
        private readonly TextBlock[] _faces = [new(), new()];
        private int _current;

        public Slot(string text)
        {
            foreach (var face in _faces)
            {
                face.IsTextScaleFactorEnabled = false;
                face.LineStackingStrategy = LineStackingStrategy.MaxHeight;
                face.Padding = new Thickness(0, 2, 0, 2);
                // Figures of one width, so a 1 rolling in for an 8 does not open a gap in the line.
                Typography.SetNumeralAlignment(face, FontNumeralAlignment.Tabular);
                Host.Children.Add(face);
            }
            _faces[0].Text = text;
            // Faces outside the window are cut off, as on a counter's wheel.
            Host.SizeChanged += (_, e) => Host.Clip = new RectangleGeometry
            {
                Rect = new Windows.Foundation.Rect(-1, 0, e.NewSize.Width + 2, e.NewSize.Height),
            };
        }

        public void Show(string text)
        {
            var leaving = _faces[_current];
            if (leaving.Text == text) return;
            _current = 1 - _current;
            var coming = _faces[_current];
            coming.Text = text;
            float travel = (float)Math.Max(12, Host.ActualHeight * 0.6);
            var compositor = ElementCompositionPreview.GetElementVisual(Host).Compositor;
            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            Move(leaving, Vector3.Zero, new Vector3(0, travel, 0), 1, 0);
            Move(coming, new Vector3(0, -travel, 0), Vector3.Zero, 0, 1);
            batch.End();
            // The face gone by is emptied, or the window keeps its width.
            batch.Completed += (_, _) => { if (_faces[_current] != leaving) leaving.Text = ""; };
        }

        private static void Move(UIElement face, Vector3 from, Vector3 to, float fadeFrom, float fadeTo)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(face, true);
            var visual = ElementCompositionPreview.GetElementVisual(face);
            var compositor = visual.Compositor;
            var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.3f, 0), new Vector2(0.1f, 1));
            var move = compositor.CreateVector3KeyFrameAnimation();
            move.InsertKeyFrame(0, from);
            move.InsertKeyFrame(1, to, ease);
            move.Duration = TimeSpan.FromMilliseconds(RollMs);
            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0, fadeFrom);
            fade.InsertKeyFrame(1, fadeTo, ease);
            fade.Duration = TimeSpan.FromMilliseconds(RollMs);
            visual.StartAnimation("Translation", move);
            visual.StartAnimation("Opacity", fade);
        }
    }
}
