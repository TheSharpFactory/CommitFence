using Avalonia;
using Avalonia.Controls;

namespace CF.App.Rules.Page;

/// <summary>
/// The text of a comparison token on one line: the comparison words, then the value in the room that is left. The value shortens to fit
/// that room (see ShortValueText); the two never stack.
/// </summary>
internal sealed class WordsAndValues : Panel
{
    private const double _gap = 5;

    protected override Size MeasureOverride(Size availableSize)
    {
        if(Children.Count != 2)
            return default;

        var words = Children[0];
        var values = Children[1];
        words.Measure(Size.Infinity);
        var wordsWidth = words.DesiredSize.Width;
        values.Measure(new Size(Math.Max(0, availableSize.Width - wordsWidth - _gap), double.PositiveInfinity));
        var gap = values.DesiredSize.Width > 0 ? _gap : 0;
        return new Size(wordsWidth + gap + values.DesiredSize.Width, Math.Max(words.DesiredSize.Height, values.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if(Children.Count != 2)
            return finalSize;

        var words = Children[0];
        var values = Children[1];
        words.Arrange(new Rect(0, 0, words.DesiredSize.Width, finalSize.Height));
        values.Arrange(new Rect(words.DesiredSize.Width + _gap, 0, Math.Max(0, finalSize.Width - words.DesiredSize.Width - _gap), finalSize.Height));
        return finalSize;
    }
}
