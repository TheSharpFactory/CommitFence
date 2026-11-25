using Avalonia;
using Avalonia.Controls;

namespace CF.App.Parts;

/// <summary>
/// The panel of the rail's items: the first ones stand one under the other at the top, 4 apart, and the last
/// <see cref="LowerCount"/> stand together at the bottom, apart from them. The rail is one list with one selection and one tab stop.
/// </summary>
internal sealed class RailPanel : Panel
{
    private const double _spacing = 4;

    public static readonly StyledProperty<int> LowerCountProperty = AvaloniaProperty.Register<RailPanel, int>(nameof(LowerCount), 2);

    static RailPanel()
    {
        AffectsMeasure<RailPanel>(LowerCountProperty);
    }

    /// <summary>
    /// How many of the last items stand at the bottom.
    /// </summary>
    public int LowerCount
    {
        get
        {
            return GetValue(LowerCountProperty);
        }
        set
        {
            SetValue(LowerCountProperty, value);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach(var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height + _spacing;
        }

        return new Size(width, Math.Max(0, height - _spacing));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var upper = Math.Max(0, Children.Count - LowerCount);
        var top = 0.0;
        for(var index = 0; index < upper; index++)
        {
            top = Place(Children[index], finalSize.Width, top);
        }

        var lowerHeight = 0.0;
        for(var index = upper; index < Children.Count; index++)
        {
            lowerHeight += Children[index].DesiredSize.Height + (index > upper ? _spacing : 0);
        }

        var bottom = Math.Max(top, finalSize.Height - lowerHeight);
        for(var index = upper; index < Children.Count; index++)
        {
            bottom = Place(Children[index], finalSize.Width, bottom);
        }

        return finalSize;
    }

    private static double Place(Control child, double width, double top)
    {
        var size = child.DesiredSize;
        child.Arrange(new Rect((width - size.Width) / 2, top, size.Width, size.Height));
        return top + size.Height + _spacing;
    }
}
