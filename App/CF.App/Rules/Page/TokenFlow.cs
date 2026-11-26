using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// The flow of the tokens of a clause: the items stand in lines as a wrapping panel lays them, a line break when the next item does
/// not fit. An item moves whole to the next line; an item is offered a whole line, so it breaks inside itself only when it is wider than
/// a line. The add button never takes a line of its own: it belongs to the last condition's unbreakable group, after its remove button.
/// When that line cannot hold it, the button stands in the keyword's column, under the keyword and right-aligned, level with the last
/// line, but only when it fits that column; otherwise the last condition gives up the room the button needs and shortens. The keys
/// follow what is seen: a button in the keyword's column comes before the items of the line it stands level with.
/// </summary>
internal sealed class TokenFlow : Panel
{
    public static readonly StyledProperty<double> ItemSpacingProperty = AvaloniaProperty.Register<TokenFlow, double>(nameof(ItemSpacing), 8);

    public static readonly StyledProperty<double> LineSpacingProperty = AvaloniaProperty.Register<TokenFlow, double>(nameof(LineSpacing), 8);

    public static readonly StyledProperty<double> KeywordGapProperty = AvaloniaProperty.Register<TokenFlow, double>(nameof(KeywordGap), 12);

    public static readonly StyledProperty<double> KeywordRoomProperty = AvaloniaProperty.Register<TokenFlow, double>(nameof(KeywordRoom), 90);

    static TokenFlow()
    {
        AffectsMeasure<TokenFlow>(ItemSpacingProperty, LineSpacingProperty, KeywordGapProperty, KeywordRoomProperty);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<TokenFlow>(KeyboardNavigationMode.Local);
    }

    /// <summary>
    /// The width of the keyword's column: an add button wider than this does not stand there, and the last condition gives up the room
    /// it needs on its own line instead.
    /// </summary>
    public double KeywordRoom
    {
        get
        {
            return GetValue(KeywordRoomProperty);
        }
        set
        {
            SetValue(KeywordRoomProperty, value);
        }
    }

    /// <summary>
    /// The room between the keyword's column and the flow: an add button that stands in the keyword's column ends this far left of the flow.
    /// </summary>
    public double KeywordGap
    {
        get
        {
            return GetValue(KeywordGapProperty);
        }
        set
        {
            SetValue(KeywordGapProperty, value);
        }
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

    protected override Size MeasureOverride(Size availableSize)
    {
        var flow = Flow(availableSize.Width);
        var width = flow.Lines.Count == 0 ? 0 : flow.Lines.Max(line => line.Width);
        var height = flow.Lines.Sum(line => line.Height) + (LineSpacing * Math.Max(0, flow.Lines.Count - 1));
        if(flow.Outside is { } outside && flow.Lines.Count == 1)
            height += LineSpacing + outside.DesiredSize.Height;

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var flow = Flow(finalSize.Width);
        var top = 0.0;
        var lastTop = 0.0;
        foreach(var line in flow.Lines)
        {
            var left = 0.0;
            foreach(var item in line.Items)
            {
                item.Arrange(new Rect(left, top, item.DesiredSize.Width, line.Height));
                left += item.DesiredSize.Width + ItemSpacing;
            }

            lastTop = top;
            top += line.Height + LineSpacing;
        }

        if(flow.Outside is { } outside)
            ArrangeInKeywordColumn(outside, flow.Lines, lastTop);

        FollowTheEye(flow);
        return finalSize;
    }

    /// <summary>
    /// The keys visit the items in the order they are seen: a button in the keyword's column comes before the items of the last line it
    /// stands level with, or after everything when it stands under the keyword.
    /// </summary>
    private static void FollowTheEye(FlowResult flow)
    {
        var seen = flow.Lines.SelectMany(line => line.Items).ToList();
        if(flow.Outside is { } outside)
            seen.Insert(flow.Lines.Count > 1 ? seen.Count - flow.Lines[^1].Items.Count : seen.Count, outside);

        for(var position = 0; position < seen.Count; position++)
        {
            // an item is a presenter that only holds the focusable controls, and an index reaches those only when the presenter is its own scope
            KeyboardNavigation.SetTabNavigation(seen[position], KeyboardNavigationMode.Local);
            KeyboardNavigation.SetTabIndex(seen[position], position);
        }
    }

    /// <summary>
    /// An add button that did not fit its line stands in the keyword's column, level with the bottom of the last line; when there is
    /// only one line, the keyword is on it, so the button stands under it.
    /// </summary>
    private void ArrangeInKeywordColumn(Control add, List<FlowLine> lines, double lastTop)
    {
        var size = add.DesiredSize;
        var top = lines.Count > 1 ? lastTop + lines[^1].Height - size.Height : lines[0].Height + LineSpacing;
        add.Arrange(new Rect(-KeywordGap - size.Width, top, size.Width, size.Height));
    }

    private FlowResult Flow(double width)
    {
        var items = Children.Where(child => child.IsVisible).ToList();
        var add = items.Count >= 2 && IsAdd(items[^1]) && IsCondition(items[^2]) ? items[^1] : null;
        if(add == null)
            return Lay(items, null, width, false);

        items.RemoveAt(items.Count - 1);
        add.Measure(new Size(width, double.PositiveInfinity));
        return Lay(items, add, width, add.DesiredSize.Width > KeywordRoom);
    }

    /// <summary>
    /// Lays the items in lines. The add button stands after the last item when both fit a line, or when the last item gives up the
    /// room it needs (<paramref name="reflowLast"/>); otherwise it is the result's outside item.
    /// </summary>
    private FlowResult Lay(List<Control> items, Control? add, double width, bool reflowLast)
    {
        var lines = new List<FlowLine>();
        var line = new FlowLine();
        var joined = false;
        foreach(var item in items)
        {
            var isLast = add != null && ReferenceEquals(item, items[^1]);
            var reserved = isLast ? ItemSpacing + add!.DesiredSize.Width : 0;
            item.Measure(new Size(reflowLast && isLast ? Math.Max(0, width - reserved) : width, double.PositiveInfinity));
            var size = item.DesiredSize;
            joined = isLast && (reflowLast || size.Width + reserved <= width);
            var needed = joined ? size.Width + reserved : size.Width;
            if(line.Items.Count > 0 && line.Width + ItemSpacing + needed > width)
            {
                lines.Add(line);
                line = new FlowLine();
            }

            line.Add(item, size, ItemSpacing);
            if(joined)
                line.Add(add!, add!.DesiredSize, ItemSpacing);
        }

        if(line.Items.Count > 0)
            lines.Add(line);

        return new FlowResult(lines, add != null && !joined ? add : null);
    }

    private static bool IsAdd(Control item)
    {
        return ItemOf(item) is ITokenAdd;
    }

    private static bool IsCondition(Control item)
    {
        return ItemOf(item) is ConditionViewModel or PathPieceViewModel;
    }

    private static object? ItemOf(Control item)
    {
        return item is ContentPresenter { Content: { } content } ? content : item.DataContext;
    }

    private sealed record FlowResult(List<FlowLine> Lines, Control? Outside);

    private sealed class FlowLine
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
