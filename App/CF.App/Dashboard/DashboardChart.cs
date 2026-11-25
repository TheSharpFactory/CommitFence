using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using CF.App.Rules.Page.Code;
using CF.Core.Review;
using CF.Core.Services;

namespace CF.App.Dashboard;

/// <summary>Draws the thirty-day chart and gives it one keyboard stop.</summary>
internal sealed class DashboardChart : Control
{
    private const double _gutter = 40;
    private const double _plotTop = 30;
    private const double _labels = 28;
    private const double _busyHeight = 240;
    private const int _maximumIndividualReviews = 40;

    public static readonly StyledProperty<IReadOnlyList<StatsDay>> DaysProperty =
        AvaloniaProperty.Register<DashboardChart, IReadOnlyList<StatsDay>>(nameof(Days), []);

    public static readonly StyledProperty<int> SelectedDayProperty =
        AvaloniaProperty.Register<DashboardChart, int>(nameof(SelectedDay));

    static DashboardChart()
    {
        AffectsMeasure<DashboardChart>(DaysProperty);
        AffectsRender<DashboardChart>(DaysProperty, SelectedDayProperty);
    }

    public DashboardChart()
    {
        Focusable = true;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    private FontFamily _labelFamily = FontFamily.Default;
    private double _labelSize;

    public event Action<int>? DayChosen;

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

    public static int MaximumOf(IReadOnlyList<StatsDay> days)
    {
        return days.Select(day => day.Total).DefaultIfEmpty(0).Max();
    }

    public static string ScaleLabel(int maximum)
    {
        return maximum > _maximumIndividualReviews ? "Each block is a day's reviews of one kind" : "Each box is one review";
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(0, _plotTop + PlotHeight(MaximumOf(Days)) + _labels);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(Days.Count == 0 || e.Key is not (Key.Left or Key.Right or Key.Home or Key.End))
            return;

        var day = SelectedDay;
        if(e.Key == Key.Left)
            day--;
        else if(e.Key == Key.Right)
            day++;
        else if(e.Key == Key.Home)
            day = 0;
        else
            day = Days.Count - 1;

        DayChosen?.Invoke(Math.Clamp(day, 0, Days.Count - 1));
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var x = e.GetPosition(this).X;
        if(Days.Count == 0 || x < _gutter || Bounds.Width <= _gutter || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Focus(NavigationMethod.Pointer);
        var day = (int)((x - _gutter) * Days.Count / (Bounds.Width - _gutter));
        DayChosen?.Invoke(Math.Min(day, Days.Count - 1));
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        if(Days.Count == 0 || Bounds.Width <= _gutter)
            return;

        context.DrawRectangle(ThemeBrushes.Find(this, "page"), null, new Rect(Bounds.Size));
        var maximum = MaximumOf(Days);
        var aggregate = maximum > _maximumIndividualReviews;
        var plotHeight = PlotHeight(maximum);
        var baseline = _plotTop + plotHeight;
        var step = maximum == 0 ? 24 : plotHeight / maximum;
        var column = (Bounds.Width - _gutter) / Days.Count;
        var boxWidth = Math.Clamp(column - 6, 10, 26);
        var selected = Days[Math.Clamp(SelectedDay, 0, Days.Count - 1)];
        var selectedHeight = Math.Max(1, selected.Total) * step + 4;
        var selectedLeft = _gutter + (Math.Clamp(SelectedDay, 0, Days.Count - 1) * column) + ((column - boxWidth) / 2);
        context.DrawRectangle(ThemeBrushes.Find(this, "hover"), null,
            new Rect(selectedLeft - 5, baseline - selectedHeight, boxWidth + 10, selectedHeight), 7, 7);
        foreach(var tick in Ticks(maximum))
            DrawAxis(context, tick, baseline - (tick * step), "line");

        DrawAxis(context, 0, baseline, "line-strong");
        Label(context, ScaleLabel(maximum), Bounds.Width - 2, 3, "ink-2", true);
        var refused = ReviewMark.InkOf(this, ReviewOutcome.Blocked);
        var incomplete = ReviewMark.InkOf(this, ReviewOutcome.Incomplete);
        var allowed = ReviewMark.InkOf(this, ReviewOutcome.Clean);
        for(var day = 0; day < Days.Count; day++)
        {
            var x = _gutter + (day * column) + ((column - boxWidth) / 2);
            var data = Days[day];
            var y = baseline;
            y = DrawStack(context, x, y, boxWidth, step, data.Refused, ReviewOutcome.Blocked, refused, aggregate);
            y = DrawStack(context, x, y, boxWidth, step, data.Incomplete, ReviewOutcome.Incomplete, incomplete, aggregate);
            DrawStack(context, x, y, boxWidth, step, data.Allowed, ReviewOutcome.Clean, allowed, aggregate);
            if(day == Days.Count - 1)
                Label(context, "Today", x + boxWidth, baseline + 7, "ink", true, FontWeight.SemiBold);
            else if(day < Days.Count - 3 && data.Date.DayOfWeek == DayOfWeek.Monday)
                Label(context, data.Date.ToString("d MMM", CultureInfo.InvariantCulture), x, baseline + 7, "ink-3", false);
        }
    }

    private static double PlotHeight(int maximum)
    {
        return maximum <= 10 ? Math.Max(1, maximum) * 24 : _busyHeight;
    }

    private static IEnumerable<int> Ticks(int maximum)
    {
        var interval = maximum switch { <= 10 => 5, <= 30 => 10, <= 75 => 25, _ => (int)(Math.Ceiling(maximum / 150.0) * 50) };
        for(var tick = interval; tick < maximum; tick += interval)
            yield return tick;

        if(maximum > 0)
            yield return maximum;
    }

    private static double DrawStack(DrawingContext context, double x, double bottom, double width, double step, int count,
        ReviewOutcome outcome, ReviewMarkInk ink, bool aggregate)
    {
        if(aggregate && count > 0)
        {
            var band = count * step;
            var height = Math.Max(1, band - 4);
            ReviewMark.Draw(context, new Rect(x, bottom - height, width, height), outcome, ink, false);
            return bottom - band;
        }

        var boxHeight = step - 4;
        for(var i = 0; i < count; i++)
        {
            ReviewMark.Draw(context, new Rect(x, bottom - boxHeight, width, boxHeight), outcome, ink, boxHeight >= 12);
            bottom -= step;
        }

        return bottom;
    }

    private void DrawAxis(DrawingContext context, int value, double y, string line)
    {
        context.DrawRectangle(ThemeBrushes.Find(this, line), null, new Rect(_gutter, y, Bounds.Width - _gutter, 1));
        Label(context, value.ToString(CultureInfo.InvariantCulture), _gutter - 12, y - 8, "ink-3", true);
    }

    private void Label(DrawingContext context, string label, double x, double y, string token, bool right, FontWeight weight = FontWeight.Normal)
    {
        if(_labelSize <= 0)
            return;

        var type = new Typeface(_labelFamily, weight: weight);
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, type, _labelSize, ThemeBrushes.Find(this, token));
        context.DrawText(text, new Point(right ? x - text.Width : x, y));
    }
}
