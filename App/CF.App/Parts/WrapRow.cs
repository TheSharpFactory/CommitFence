using Avalonia;
using Avalonia.Controls;

namespace CF.App.Parts;

/// <summary>
/// A wrapping row, the row of a screen's head and of a section's head: its items centred on the line, 12 apart (or as set) with
/// the set room between lines. An item marked
/// <see cref="AtEndProperty"/> goes to the right end of its line, and takes a line of its own when it does not fit on the last one.
/// </summary>
internal sealed class WrapRow : Panel
{
    public static readonly StyledProperty<double> ItemSpacingProperty = AvaloniaProperty.Register<WrapRow, double>(nameof(ItemSpacing), 12);

    public static readonly StyledProperty<double> LineSpacingProperty = AvaloniaProperty.Register<WrapRow, double>(nameof(LineSpacing), 8);

    public static readonly AttachedProperty<bool> AtEndProperty = AvaloniaProperty.RegisterAttached<WrapRow, Control, bool>("AtEnd");

    static WrapRow()
    {
        AffectsMeasure<WrapRow>(ItemSpacingProperty, LineSpacingProperty);
    }

    /// <summary>
    /// The room between two items of a line.
    /// </summary>
    public double ItemSpacing
    {
        get
        {
            return GetValue(ItemSpacingProperty);
        }
        set
        {
            SetValue(ItemSpacingProperty, value);
        }
    }

    /// <summary>
    /// The room between two lines.
    /// </summary>
    public double LineSpacing
    {
        get
        {
            return GetValue(LineSpacingProperty);
        }
        set
        {
            SetValue(LineSpacingProperty, value);
        }
    }

    public static bool GetAtEnd(Control control)
    {
        return control.GetValue(AtEndProperty);
    }

    public static void SetAtEnd(Control control, bool value)
    {
        control.SetValue(AtEndProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var lines = Flow(availableSize.Width);
        var width = 0.0;
        var height = 0.0;
        foreach(var line in lines)
        {
            width = Math.Max(width, line.Width);
            height += line.Height;
        }

        height += LineSpacing * Math.Max(0, lines.Count - 1);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var top = 0.0;
        foreach(var line in Flow(finalSize.Width))
        {
            var left = 0.0;
            foreach(var item in line.Items)
            {
                var size = item.DesiredSize;
                var x = GetAtEnd(item) ? Math.Max(left, finalSize.Width - size.Width) : left;
                item.Arrange(new Rect(x, top + ((line.Height - size.Height) / 2), size.Width, size.Height));
                left = x + size.Width + ItemSpacing;
            }

            top += line.Height + LineSpacing;
        }

        return finalSize;
    }

    /// <summary>
    /// Measures every visible item against the width and lays them in lines.
    /// </summary>
    private List<HeadLine> Flow(double width)
    {
        var lines = new List<HeadLine>();
        var line = new HeadLine();
        foreach(var child in Children)
        {
            if(!child.IsVisible)
                continue;

            child.Measure(new Size(width, double.PositiveInfinity));
            var size = child.DesiredSize;
            if(line.Items.Count > 0 && line.Width + ItemSpacing + size.Width > width)
            {
                lines.Add(line);
                line = new HeadLine();
            }

            line.Add(child, size, ItemSpacing);
        }

        if(line.Items.Count > 0)
            lines.Add(line);

        return lines;
    }

    private sealed class HeadLine
    {
        public List<Control> Items { get; } = new List<Control>();

        public double Width { get; private set; }

        public double Height { get; private set; }

        public void Add(Control item, Size size, double spacing)
        {
            Width += (Items.Count > 0 ? spacing : 0) + size.Width;
            Height = Math.Max(Height, size.Height);
            Items.Add(item);
        }
    }
}
