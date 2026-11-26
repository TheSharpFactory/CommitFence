using Avalonia;
using Avalonia.Controls;

namespace CF.App.Rules.Page;

/// <summary>
/// The two parts of a condition: its property token, then its comparison with the remove button and "and" after it. A condition is
/// always one row: when the whole of it does not fit the row it is offered, the comparison gets the room that the property leaves, and
/// its value is shortened to fit (see ShortValueText). The flow of the clause moves a condition that does not fit the end of a row to the
/// next row whole.
/// </summary>
internal sealed class ConditionFlow : Panel
{
    private const double _gap = 2;

    protected override Size MeasureOverride(Size availableSize)
    {
        if(Children.Count != 2)
            return default;

        var property = Children[0];
        var tail = Children[1];
        property.Measure(Size.Infinity);
        var propertyWidth = property.DesiredSize.Width;
        var gap = property.IsVisible ? _gap : 0;
        tail.Measure(new Size(Math.Max(0, availableSize.Width - propertyWidth - gap), double.PositiveInfinity));
        var tailWidth = tail.DesiredSize.Width;
        return new Size(propertyWidth + (tailWidth > 0 ? gap : 0) + tailWidth, Math.Max(property.DesiredSize.Height, tail.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if(Children.Count != 2)
            return finalSize;

        var property = Children[0];
        var tail = Children[1];
        property.Arrange(new Rect(0, 0, property.DesiredSize.Width, finalSize.Height));
        var left = property.DesiredSize.Width + (tail.DesiredSize.Width > 0 ? _gap : 0);
        tail.Arrange(new Rect(left, 0, tail.DesiredSize.Width, finalSize.Height));
        return finalSize;
    }
}
