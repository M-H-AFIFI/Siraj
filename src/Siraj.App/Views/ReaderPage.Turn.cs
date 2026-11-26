using System.Diagnostics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Siraj.App.Controls;
using Siraj.App.Quran;
using Siraj.Core.Quran;
using Windows.Foundation;

namespace Siraj.App.Views;

public sealed partial class ReaderPage
{
    private const double TurnMs = 720, QuickTurnMs = 360;

    private PageCurl? _curl;
    private readonly List<CanvasRenderTarget> _pictures = [];
    private int _turnId;
    private bool _turnForward;
    private int _turnFrom, _turnTo;
    private bool _turnReady;
    // In the curl's terms: 0 is the spread before a forward turn, 1 the spread after it.
    private double _turnProgress;
    private double? _settleTo;
    private double _animFrom, _animMs;
    private long _animStart;
    private bool _animEaseInOut;
    private bool _rendering;
    private int? _pendingPage;
    private int _hideIn;

    private uint? _dragId;
    private Point _dragStart;
    private bool _dragTurn;
    private double _dragLastX, _dragVelocity;
    private long _dragLastTick;

    private double _wheel;
    private long _wheelAt;

    private static double Ms(long from) => Stopwatch.GetElapsedTime(from).TotalMilliseconds;

    private int SpreadOf(int page) => TwoPages ? (page % 2 == 1 ? page : page - 1) : page;

    private int ForwardOf(int page) => Math.Clamp(TwoPages ? SpreadOf(page) + 2 : page + 1, 1, QuranDatabase.PageCount);

    private int BackOf(int page) => Math.Clamp(TwoPages ? SpreadOf(page) - 2 : page - 1, 1, QuranDatabase.PageCount);

    private int Heading => _pendingPage ?? (_curl != null ? _turnTo : _page);

    private void Forward() => TurnTo(ForwardOf(Heading));

    private void Back() => TurnTo(BackOf(Heading));

    private void TurnTo(int page)
    {
        page = Math.Clamp(page, 1, QuranDatabase.PageCount);
        if (_curl != null)
        {
            // The hand has the leaf; a key or the wheel waits until it is let go.
            if (_dragId != null) return;
            if (SpreadOf(page) == SpreadOf(_turnTo)) { _pendingPage = null; return; }
            _pendingPage = page;
            Hurry();
            return;
        }
        if (SpreadOf(page) == SpreadOf(_page) || !Kit.Motion || !IsLoaded || Pages.ActualWidth < 10)
        {
            ShowPage(page);
            return;
        }
        _ = BeginTurn(page, page > _page, dragged: false, quick: false);
    }

    private async Task BeginTurn(int page, bool forward, bool dragged, bool quick)
    {
        int id = ++_turnId;
        bool two = TwoPages;
        float dpi = (float)((XamlRoot?.RasterizationScale ?? 1) * 96);
        var oldLeft = two ? Keep(LeftPage.Snapshot(dpi)) : null;
        var oldRight = Keep(RightPage.Snapshot(dpi));
        var leaf = SheetOnLayer(two ? LeftPage : RightPage);
        bool dark = _mushafColors.IsDark;
        _turnForward = forward;
        _turnFrom = _page;
        _turnTo = page;
        _turnReady = false;
        _turnProgress = forward ? 0 : 1;
        _settleTo = dragged ? null : forward ? 1 : 0;
        // Until the pages it turns to are ready, the layer holds the spread as it was.
        _curl = forward
            ? new PageCurl { Leaf = leaf, Single = !two, Front = two ? oldLeft : oldRight, Landing = two ? oldRight : null, IsDarkPaper = dark, Progress = 0 }
            : new PageCurl { Leaf = leaf, Single = !two, Under = two ? oldLeft : oldRight, Back = two ? oldRight : null, IsDarkPaper = dark, Progress = 1 };
        TurnLayer.Visibility = Visibility.Visible;
        TurnLayer.Invalidate();
        StartRendering();
        // The real pages change only once the picture of them covers them, or the new spread flashes first.
        await LayerShown();
        if (id != _turnId || _curl == null) return;

        ShowPage(page);
        if (two) await Task.WhenAll(LeftPage.ShowAsync(LeftPage.PageNumber), RightPage.ShowAsync(RightPage.PageNumber));
        else await RightPage.ShowAsync(RightPage.PageNumber);
        if (id != _turnId || _curl == null) return;

        var newLeft = two ? Keep(LeftPage.Snapshot(dpi)) : null;
        var newRight = Keep(RightPage.Snapshot(dpi));
        if (two)
        {
            _curl = forward
                ? new PageCurl { Leaf = leaf, Front = oldLeft, Back = newRight, Under = newLeft, Landing = oldRight, IsDarkPaper = dark }
                : new PageCurl { Leaf = leaf, Front = newLeft, Back = oldRight, Under = oldLeft, Landing = newRight, IsDarkPaper = dark };
        }
        else
        {
            var front = forward ? oldRight : newRight;
            var verso = front != null ? Keep(PageCurl.Verso(front, _mushafColors.Paper, dpi)) : null;
            _curl = new PageCurl { Leaf = leaf, Single = true, Front = front, Back = verso, Under = forward ? newRight : oldRight, IsDarkPaper = dark };
        }
        _curl.Progress = _turnProgress;
        _turnReady = true;
        if (_settleTo is { } to) Animate(to, easeInOut: _dragId == null && !dragged, quick ? QuickTurnMs : TurnMs);
        TurnLayer.Invalidate();
    }

    private CanvasRenderTarget? Keep(CanvasRenderTarget? picture)
    {
        if (picture != null) _pictures.Add(picture);
        return picture;
    }

    private Rect SheetOnLayer(MushafPageView view)
    {
        var r = view.TransformToVisual(Pages).TransformBounds(view.Sheet);
        return new Rect(r.X - Pages.Padding.Left, r.Y - Pages.Padding.Top, r.Width, r.Height);
    }

    private TaskCompletionSource? _layerShown;

    private Task LayerShown()
    {
        _layerShown = new TaskCompletionSource();
        return _layerShown.Task;
    }

    private void Animate(double to, bool easeInOut, double fullMs)
    {
        _animFrom = _turnProgress;
        _settleTo = to;
        _animStart = Stopwatch.GetTimestamp();
        _animMs = Math.Max(140, fullMs * Math.Abs(to - _animFrom));
        _animEaseInOut = easeInOut;
        StartRendering();
    }

    private void Hurry()
    {
        if (_turnReady && _settleTo is { } to && _animMs > 200) Animate(to, easeInOut: false, QuickTurnMs * 0.7);
    }

    private void StartRendering()
    {
        if (_rendering) return;
        _rendering = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopRendering()
    {
        if (!_rendering) return;
        _rendering = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, object e)
    {
        if (_curl == null) { StopRendering(); return; }
        if (_hideIn > 0)
        {
            if (--_hideIn == 0) EndTurn();
            return;
        }
        if (!_turnReady || _settleTo is not { } to || _dragId != null) return;
        double f = Math.Clamp(Ms(_animStart) / _animMs, 0, 1);
        double outward = 1 - Math.Pow(1 - f, 3);
        // A turn by itself is lifted briskly and laid down softly; let go by the hand, it only slows.
        double eased = _animEaseInOut ? 0.35 * outward + 0.65 * (f < 0.5 ? 4 * f * f * f : 1 - Math.Pow(-2 * f + 2, 3) / 2) : outward;
        _turnProgress = _animFrom + (to - _animFrom) * eased;
        _curl.Progress = _turnProgress;
        TurnLayer.Invalidate();
        if (f >= 1) Settled(to);
    }

    private void Settled(double to)
    {
        _settleTo = null;
        bool done = _turnForward ? to >= 1 : to <= 0;
        if (done)
        {
            EndTurn();
            if (_pendingPage is { } next)
            {
                _pendingPage = null;
                if (SpreadOf(next) != SpreadOf(_page) && Kit.Motion) _ = BeginTurn(next, next > _page, dragged: false, quick: true);
                else ShowPage(next);
            }
            return;
        }
        // Laid back: the real pages return to where they were, and the picture stays a few frames while they draw.
        _pendingPage = null;
        ShowPage(_turnFrom);
        _hideIn = 3;
    }

    private void EndTurn()
    {
        _turnId++;
        _curl = null;
        _turnReady = false;
        _settleTo = null;
        _hideIn = 0;
        _layerShown?.TrySetResult();
        _layerShown = null;
        TurnLayer.Visibility = Visibility.Collapsed;
        foreach (var picture in _pictures) picture.Dispose();
        _pictures.Clear();
        StopRendering();
    }

    private void FinishTurnNow()
    {
        if (_curl == null) return;
        bool cancelled = _settleTo is { } to && (_turnForward ? to <= 0 : to >= 1);
        EndTurn();
        if (cancelled) ShowPage(_turnFrom);
        if (_pendingPage is { } next)
        {
            _pendingPage = null;
            ShowPage(next);
        }
        _dragId = null;
    }

    private void OnTurnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        _curl?.Draw(args.DrawingSession);
        // Let the pages change on the next frame, when this one is on the screen.
        if (_layerShown is { } shown)
        {
            _layerShown = null;
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => shown.TrySetResult());
        }
    }

    private void OnPagesPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Pages);
        if (point.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed) return;
        if (_curl != null) return;
        _dragId = e.Pointer.PointerId;
        _dragStart = point.Position;
        _dragTurn = false;
        _dragLastX = point.Position.X;
        _dragLastTick = Stopwatch.GetTimestamp();
        _dragVelocity = 0;
    }

    private void OnPagesMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragId != e.Pointer.PointerId) return;
        var at = e.GetCurrentPoint(Pages).Position;
        double dx = at.X - _dragStart.X, dy = at.Y - _dragStart.Y;
        if (!_dragTurn)
        {
            if (Math.Abs(dx) < 14 || Math.Abs(dx) < 1.4 * Math.Abs(dy)) return;
            // Pulled to the right, the left-hand leaf comes over: the next page of an Arabic book.
            bool forward = dx > 0;
            int target = forward ? ForwardOf(_page) : BackOf(_page);
            if (SpreadOf(target) == SpreadOf(_page)) { _dragId = null; return; }
            if (!Kit.Motion)
            {
                _dragId = null;
                ShowPage(target);
                return;
            }
            _dragTurn = true;
            Pages.CapturePointer(e.Pointer);
            _ = BeginTurn(target, forward, dragged: true, quick: false);
        }
        double ms = Math.Max(1, Ms(_dragLastTick));
        _dragVelocity = 0.6 * _dragVelocity + 0.4 * ((at.X - _dragLastX) / ms);
        _dragLastX = at.X;
        _dragLastTick = Stopwatch.GetTimestamp();

        double leaf = (TwoPages ? LeftPage : RightPage).Sheet.Width;
        double span = Math.Max(100, TwoPages ? 2 * leaf : 1.3 * leaf);
        double share = Math.Clamp((_turnForward ? dx : -dx) / span, 0, 1);
        _turnProgress = _turnForward ? share : 1 - share;
        if (_turnReady && _curl != null)
        {
            _curl.Progress = _turnProgress;
            TurnLayer.Invalidate();
        }
        e.Handled = true;
    }

    private void OnPagesReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragId != e.Pointer.PointerId) return;
        _dragId = null;
        if (!_dragTurn) return;
        _dragTurn = false;
        Pages.ReleasePointerCapture(e.Pointer);
        // A quick throw finishes the turn whatever the distance; a slow hand needs to have taken it a third of the way.
        double towards = _turnForward ? _dragVelocity : -_dragVelocity;
        if (Ms(_dragLastTick) > 120) towards = 0;
        double done = _turnForward ? _turnProgress : 1 - _turnProgress;
        bool complete = towards > 0.35 || (done > 0.33 && towards > -0.15);
        double end = complete == _turnForward ? 1 : 0;
        _settleTo = end;
        if (_turnReady) Animate(end, easeInOut: false, TurnMs);
        e.Handled = true;
    }

    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        // A notch of a mouse wheel turns a leaf; a touchpad's small steps add up to one.
        int delta = e.GetCurrentPoint(Pages).Properties.MouseWheelDelta;
        if (Ms(_wheelAt) > 400) _wheel = 0;
        _wheelAt = Stopwatch.GetTimestamp();
        _wheel += delta;
        if (_wheel <= -100) { _wheel = 0; Forward(); }
        else if (_wheel >= 100) { _wheel = 0; Back(); }
        e.Handled = true;
    }
}
