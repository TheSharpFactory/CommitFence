using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using CF.App.Rules.Page.Code;
using CF.Core.Services;

namespace CF.App.Dashboard;

/// <summary>Draws the thirty-day chart and gives it one keyboard stop.</summary>
internal sealed class DashboardChart : Control
{
    private const double _gutter = ChartFrame.Gutter;
    private static readonly FontFeatureCollection _tabularNumbers = FontFeatureCollection.Parse("+tnum");

    public static readonly StyledProperty<IReadOnlyList<StatsDay>> DaysProperty =
        AvaloniaProperty.Register<DashboardChart, IReadOnlyList<StatsDay>>(nameof(Days), []);

    public static readonly StyledProperty<int> SelectedDayProperty =
        AvaloniaProperty.Register<DashboardChart, int>(nameof(SelectedDay));

    public static readonly StyledProperty<bool> TwoColumnsProperty =
        AvaloniaProperty.Register<DashboardChart, bool>(nameof(TwoColumns));

    static DashboardChart()
    {
        AffectsMeasure<DashboardChart>(DaysProperty, TwoColumnsProperty);
        AffectsRender<DashboardChart>(DaysProperty, SelectedDayProperty, TwoColumnsProperty);
    }

    public DashboardChart()
    {
        Focusable = true;
        FocusAdorner = null;
        LostFocus += (_, _) => SetKeyboardFocus(false);
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    private FontFamily _labelFamily = FontFamily.Default;
    private double _labelSize;
    private int _captionLines = 1;

    public event Action<int>? DayChosen;

    public bool KeyboardFocus { get; private set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if(!this.TryFindResource("type-meta", out var resource) || resource is not ControlTheme theme)
            return;

        foreach(var setter in theme.Setters.OfType<Setter>())
        {
            if(setter.Property == TextBlock.FontFamilyProperty && setter.Value is FontFamily family)
                _labelFamily = family;
            else if(setter.Property == TextBlock.FontSizeProperty && setter.Value is double size)
                _labelSize = size;
        }
    }

    public IReadOnlyList<StatsDay> Days
    {
        get { return GetValue(DaysProperty); }
        set { SetValue(DaysProperty, value); }
    }

    public int SelectedDay
    {
        get { return GetValue(SelectedDayProperty); }
        set { SetValue(SelectedDayProperty, value); }
    }

    /// <summary>Whether the screen has two columns, which gives the scale more height.</summary>
    public bool TwoColumns
    {
        get { return GetValue(TwoColumnsProperty); }
        set { SetValue(TwoColumnsProperty, value); }
    }

    private double ScaleHeight { get { return DashboardChartLayout.ScaleHeight(TwoColumns); } }

    protected override Size MeasureOverride(Size availableSize)
    {
        _captionLines = CaptionLines(availableSize.Width);
        return new Size(0, PlotTop(DashboardChartLayout.ScaleTop(Days)) + ScaleHeight + DashboardChartLayout.LabelsHeight);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        SetKeyboardFocus(e.NavigationMethod != NavigationMethod.Pointer);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(Days.Count == 0 || e.Key is not (Key.Left or Key.Right or Key.Home or Key.End))
            return;

        SetKeyboardFocus(true);
        DayChosen?.Invoke(Math.Clamp(DayForKey(e.Key), 0, Days.Count - 1));
        e.Handled = true;
    }

    /// <summary>The day a key moves to: Left and Right one day, Home the first, End the last.</summary>
    private int DayForKey(Key key)
    {
        if(key == Key.Left)
            return SelectedDay - 1;

        if(key == Key.Right)
            return SelectedDay + 1;

        return key == Key.Home ? 0 : Days.Count - 1;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var x = e.GetPosition(this).X;
        if(Days.Count == 0 || x < _gutter || Bounds.Width <= _gutter || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Focus(NavigationMethod.Pointer);
        SetKeyboardFocus(false);
        var day = (int)((x - _gutter) * Days.Count / (Bounds.Width - _gutter));
        DayChosen?.Invoke(Math.Min(day, Days.Count - 1));
        e.Handled = true;
    }

    private void SetKeyboardFocus(bool keyboard)
    {
        KeyboardFocus = keyboard;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if(Days.Count == 0 || Bounds.Width <= _gutter)
            return;

        context.DrawRectangle(ThemeBrushes.Find(this, "page"), null, new Rect(Bounds.Size));
        var top = DashboardChartLayout.ScaleTop(Days);
        var baseline = PlotTop(top) + ScaleHeight;
        var step = ScaleHeight / top;
        var frame = new ChartFrame(Bounds.Width, Days.Count);
        var selected = Math.Clamp(SelectedDay, 0, Days.Count - 1);
        foreach(var tick in DashboardChartLayout.AxisLines(top))
            DrawAxis(context, tick, baseline - (tick * step), "line");

        DrawCaption(context, top);
        DrawSelection(context, frame.BoxLeft(selected), frame.Box, baseline, DashboardChartLayout.ColumnHeight(Days[selected], top, ScaleHeight));
        for(var day = 0; day < Days.Count; day++)
            DrawDay(context, Days[day], frame.BoxLeft(day), baseline, frame.Box, top);

        foreach(var total in DashboardChartLayout.TotalLabels(Days, top, frame, StrongWidth))
        {
            var topLine = baseline - ScaleHeight;
            Label(context, total, topLine - 10);
            if(total.Anchor is { } anchor)
            {
                var labelCentre = total.Left + (StrongWidth(total.Text) / 2);
                var pen = new Pen(ThemeBrushes.Find(this, "ink-3"), 1);
                context.DrawLine(pen, new Point(labelCentre, topLine - 6), new Point(anchor, topLine + 4));
            }
        }

        foreach(var date in DashboardChartLayout.DateLabels(Days, selected, frame, StrongWidth))
            Label(context, date, baseline + 22);

        DrawAxis(context, 0, baseline, "line-strong");
    }

    private double PlotTop(long top)
    {
        return DashboardChartLayout.SpaceAbove(Days.Any(day => day.Total > top), _captionLines);
    }

    private int CaptionLines(double width)
    {
        if(_labelSize <= 0 || double.IsInfinity(width) || width <= 0 || Days.Count == 0)
            return 1;

        using var text = CaptionLayout(DashboardChartLayout.ScaleTop(Days), width);
        return Math.Max(1, text.TextLines.Count);
    }

    private TextLayout CaptionLayout(long top, double width)
    {
        var type = new Typeface(_labelFamily);
        var caption = DashboardChartLayout.ScaleLabel(top) + DashboardChartLayout.CaptionTail;
        return new TextLayout(caption, type, _labelSize, ThemeBrushes.Find(this, "ink-2"), TextAlignment.Right,
            TextWrapping.Wrap, maxWidth: width, lineHeight: DashboardChartLayout.CaptionLine, fontFeatures: _tabularNumbers);
    }

    /// <summary>The chart's own line, right-aligned at the chart's right edge; it wraps when the chart is narrower than it.</summary>
    private void DrawCaption(DrawingContext context, long top)
    {
        if(_labelSize <= 0)
            return;

        using var text = CaptionLayout(top, Bounds.Width);
        text.Draw(context, new Point(0, DashboardChartLayout.CaptionLine - text.TextLines[0].Baseline));
    }

    private double StrongWidth(string label)
    {
        if(_labelSize <= 0)
            return 0;

        using var text = new TextLayout(label, new Typeface(_labelFamily, weight: FontWeight.SemiBold), _labelSize, null, fontFeatures: _tabularNumbers);
        return text.Width;
    }

    private void DrawSelection(DrawingContext context, double x, double width, double baseline, double columnHeight)
    {
        var thickness = KeyboardFocus ? 2.0 : 1.0;
        var inset = thickness / 2;
        var height = Math.Max(4, columnHeight + 3);
        var pen = new Pen(ThemeBrushes.Find(this, KeyboardFocus ? "focus" : "ink-3"), thickness);
        context.DrawRectangle(ThemeBrushes.Find(this, "hover"), pen,
            new Rect(x - 3 + inset, baseline - height + inset, width + 6 - thickness, height - thickness), 7 - inset, 7 - inset);
    }

    private void DrawDay(DrawingContext context, StatsDay day, double x, double baseline, double width, long top)
    {
        var bottom = baseline;
        if(DashboardChartLayout.UsesBlocks(top))
        {
            foreach(var block in DashboardChartLayout.BlocksOf(day, top, ScaleHeight))
            {
                ReviewMark.Draw(context, new Rect(x, bottom - block.Height, width, block.Height), block.Kind, ReviewMark.InkOf(this, block.Kind));
                bottom -= block.Height + 4;
            }

            return;
        }

        var step = ScaleHeight / top;
        foreach(var group in DashboardChartLayout.BoxesOf(day, top))
        {
            var ink = ReviewMark.InkOf(this, group.Kind);
            for(var i = 0; i < group.Count; i++)
            {
                ReviewMark.Draw(context, new Rect(x, bottom - (step - 4), width, step - 4), group.Kind, ink);
                bottom -= step;
            }
        }
    }

    private void DrawAxis(DrawingContext context, long value, double y, string line)
    {
        context.DrawRectangle(ThemeBrushes.Find(this, line), null, new Rect(_gutter, y, Bounds.Width - _gutter, 1));
        Label(context, value.ToString(CultureInfo.InvariantCulture), _gutter - 12, y + 5, "ink-3", TextAlignment.Right);
    }

    private void Label(DrawingContext context, ChartLabel label, double baseline)
    {
        Label(context, label.Text, label.Left, baseline, label.Strong ? "ink" : "ink-3", TextAlignment.Left, label.Strong ? FontWeight.SemiBold : FontWeight.Normal);
    }

    private void Label(DrawingContext context, string label, double x, double baseline, string token, TextAlignment alignment, FontWeight weight = FontWeight.Normal)
    {
        if(_labelSize <= 0)
            return;

        var type = new Typeface(_labelFamily, weight: weight);
        using var text = new TextLayout(label, type, _labelSize, ThemeBrushes.Find(this, token), fontFeatures: _tabularNumbers);
        var left = alignment == TextAlignment.Right ? x - text.Width : x;
        text.Draw(context, new Point(left, baseline - text.TextLines[0].Baseline));
    }
}
