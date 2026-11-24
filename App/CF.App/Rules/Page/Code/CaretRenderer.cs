using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace CF.App.Rules.Page.Code;

/// <summary>
/// Draws the caret in <c>ink</c>, 2 wide. The editor's own caret is a
/// hair of half a pixel and has no setting for it, so that one is drawn transparent and this one is drawn in its place, 16 high
/// in the middle of the row blinking while the code has the keyboard.
/// </summary>
internal sealed class CaretRenderer : IBackgroundRenderer
{
    private const double _width = 2;
    private const double _height = 16;
    private static readonly TimeSpan _blink = TimeSpan.FromMilliseconds(530);

    private readonly TextArea _area;
    private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = _blink };
    private bool _shown = true;

    public CaretRenderer(TextArea area)
    {
        _area = area;
        _timer.Tick += (_, _) => Flip();
        _area.GotFocus += (_, _) => Restart();
        _area.LostFocus += (_, _) => Stop();
        _area.Caret.PositionChanged += (_, _) => Restart();
    }

    public KnownLayer Layer
    {
        get
        {
            return KnownLayer.Caret;
        }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if(!_area.IsFocused || !_shown || !textView.VisualLinesValid)
            return;

        var rectangle = _area.Caret.CalculateCaretRectangle();
        drawingContext.DrawRectangle(ThemeBrushes.Find(textView, "ink"), null, new Rect(rectangle.X, rectangle.Y + ((rectangle.Height - _height) / 2), _width, _height));
    }

    private void Flip()
    {
        _shown = !_shown;
        _area.TextView.InvalidateLayer(KnownLayer.Caret);
    }

    private void Restart()
    {
        _shown = true;
        _timer.Stop();
        _timer.Start();
        _area.TextView.InvalidateLayer(KnownLayer.Caret);
    }

    private void Stop()
    {
        _timer.Stop();
        _shown = true;
        _area.TextView.InvalidateLayer(KnownLayer.Caret);
    }
}
