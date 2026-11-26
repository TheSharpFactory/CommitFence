using CF.Core.Review;
using CF.Core.Services;

namespace CF.App.Dashboard;

/// <summary>A block of one kind in a day's column, as high as it is drawn.</summary>
internal readonly record struct ChartBlock(ReviewOutcome Kind, double Height);

/// <summary>A text under or over the plot: its left edge, whether it is the strong kind, and the day it belongs to.</summary>
internal readonly record struct ChartLabel(string Text, double Left, bool Strong, int Day, double? Anchor = null);

/// <summary>The columns of the chart: how wide the chart is and how many days it holds.</summary>
internal readonly record struct ChartFrame(double Width, int Days)
{
    public const double Gutter = 40;

    public double Column { get { return (Width - Gutter) / Days; } }
    public double Box { get { return Math.Clamp(Column - 6, 10, 26); } }

    /// <summary>The left edge of the boxes of a day.</summary>
    public double BoxLeft(int day)
    {
        return Gutter + ((Column - Box) / 2) + (day * Column);
    }
}

/// <summary>What the thirty-day chart draws and where, apart from drawing it.</summary>
internal static class DashboardChartLayout
{
    public const double LabelsHeight = 28;
    public const double CaptionLine = 18;
    public const string CaptionTail = ". Click a day, or use Left and Right, for its numbers.";

    private const double _gap = 4;
    private const double _closest = 8;
    private const double _leastBlock = 4;
    private const long _mostBoxes = 20;
    private const long _mostFullSteps = 15;
    private const string _boxesCaption = "Each box is one review";
    private const string _blocksCaption = "Each block is a day's reviews of one kind";

    /// <summary>How high the scale is: more when the screen has two columns.</summary>
    public static double ScaleHeight(bool twoColumns)
    {
        return twoColumns ? 280 : 240;
    }

    /// <summary>The top line: the fourth busiest day rounded up to 5 (to 10 above 15), at least 10.</summary>
    public static long ScaleTop(IReadOnlyList<StatsDay> days)
    {
        var fourth = days.Select(day => day.Total).OrderDescending().Skip(3).FirstOrDefault();
        var interval = fourth <= _mostFullSteps ? 5L : 10L;
        return Math.Max(10, ((fourth + interval - 1) / interval) * interval);
    }

    /// <summary>The gridlines above the baseline: every step up to a top of 20, then only the middle and the top.</summary>
    public static IReadOnlyList<long> AxisLines(long top)
    {
        if(top > _mostBoxes)
            return [top / 2, top];

        var interval = top <= _mostFullSteps ? 5 : 10;
        return Enumerable.Range(1, (int)(top / interval)).Select(number => number * (long)interval).ToList();
    }

    /// <summary>What a mark is, by the kind of column the scale gives.</summary>
    public static string ScaleLabel(long top)
    {
        return top > _mostBoxes ? _blocksCaption : _boxesCaption;
    }

    /// <summary>Whether the scale draws a day as blocks of its kinds, not as one box for each review.</summary>
    public static bool UsesBlocks(long top)
    {
        return top > _mostBoxes;
    }

    /// <summary>
    /// The boxes of a day, one for each review, as counts by kind in the order refused, incomplete, allowed. A day with more
    /// reviews than the top line holds keeps the first reviews that fit under it.
    /// </summary>
    public static IReadOnlyList<StatsReviewCount> BoxesOf(StatsDay day, long top)
    {
        var room = top;
        var boxes = new List<StatsReviewCount>();
        foreach(var count in day.Counts())
        {
            if(room <= 0)
                break;

            var drawn = (int)Math.Min(room, count.Count);
            boxes.Add(count with { Count = drawn });
            room -= drawn;
        }

        return boxes;
    }

    /// <summary>
    /// The blocks of a day from the baseline up, with no overlap and inside the top line. A busy day shares the scale among its
    /// kinds; every block is at least as high as its least, and what that adds is taken from the tallest.
    /// </summary>
    public static IReadOnlyList<ChartBlock> BlocksOf(StatsDay day, long top, double scaleHeight)
    {
        var counts = day.Counts();
        if(counts.Count == 0)
            return [];

        var step = scaleHeight / top;
        var capped = day.Total > top;
        var least = capped ? Math.Max(_leastBlock, step - _gap) : _leastBlock;
        var room = scaleHeight - _gap - (_gap * (counts.Count - 1));
        var heights = counts.Select(count => Math.Max(least, capped ? room * count.Count / day.Total : (count.Count * step) - _gap)).ToList();
        var excess = heights.Sum() - room;
        if(excess > 0)
        {
            var tallest = heights.IndexOf(heights.Max());
            heights[tallest] = Math.Max(least, heights[tallest] - excess);
        }

        return counts.Select((count, index) => new ChartBlock(count.Kind, heights[index])).ToList();
    }

    /// <summary>The height of a day's column, from the baseline to its highest block or box.</summary>
    public static double ColumnHeight(StatsDay day, long top, double scaleHeight)
    {
        var step = scaleHeight / top;
        if(day.Total == 0)
            return Math.Max(_leastBlock, step - _gap);

        if(!UsesBlocks(top))
            return (Math.Min(day.Total, top) * step) - _gap;

        var blocks = BlocksOf(day, top, scaleHeight);
        return blocks.Sum(block => block.Height) + (_gap * (blocks.Count - 1));
    }

    /// <summary>The space above the top line: for the chart's own line, and for a busy day's total.</summary>
    public static double SpaceAbove(bool capped, int captionLines)
    {
        return (capped ? 60 : 30) + (CaptionLine * (Math.Max(1, captionLines) - 1));
    }

    /// <summary>What stands over a day taller than the top line: the reviews not drawn when the scale draws boxes, else its count.</summary>
    public static string TotalText(StatsDay day, long top)
    {
        return UsesBlocks(top) ? day.ReviewsText : $"{day.Total - top} more";
    }

    /// <summary>
    /// The words over the days above the top line, centred where they fit. A label shifted into another day's column keeps its
    /// own column's centre as an anchor. When two labels come within 8 of each other, the lesser day's is left out.
    /// </summary>
    public static IReadOnlyList<ChartLabel> TotalLabels(IReadOnlyList<StatsDay> days, long top, ChartFrame frame, Func<string, double> width)
    {
        var placed = new List<(ChartLabel Label, double Right)>();
        var busiest = Enumerable.Range(0, days.Count).Where(day => days[day].Total > top)
            .OrderByDescending(day => days[day].Total).ThenByDescending(day => day);
        foreach(var day in busiest)
        {
            var text = TotalText(days[day], top);
            var size = width(text);
            var centre = frame.BoxLeft(day) + (frame.Box / 2);
            var left = Math.Clamp(centre - (size / 2), ChartFrame.Gutter, Math.Max(ChartFrame.Gutter, frame.Width - size));
            double? anchor = Math.Abs(left + (size / 2) - centre) >= frame.Column / 2 ? centre : null;
            if(!placed.Any(other => left < other.Right + _closest && left + size > other.Label.Left - _closest))
                placed.Add((new ChartLabel(text, left, true, day, anchor), left + size));
        }

        return placed.Select(label => label.Label).OrderBy(label => label.Day).ToList();
    }

    /// <summary>
    /// The dates under the baseline: the Monday of each week, the chosen day's date, and "Today". The chosen day's date wins:
    /// "Today" is left out when the two would come within 8 of each other.
    /// </summary>
    public static IReadOnlyList<ChartLabel> DateLabels(IReadOnlyList<StatsDay> days, int selected, ChartFrame frame, Func<string, double> width)
    {
        var labels = new List<ChartLabel>();
        var last = days.Count - 1;
        ChartLabel? chosen = selected == last ? null : DateOf(days, selected, frame, width);
        for(var day = 0; day < days.Count; day++)
        {
            if(day == last)
                AddToday(labels, frame, width, chosen);
            else if(day == selected && chosen != null)
                labels.Add(chosen.Value);
            else if(day < last - 2 && Math.Abs(day - selected) > 2 && days[day].Date.DayOfWeek == DayOfWeek.Monday)
                labels.Add(new ChartLabel(DateText(days[day]), frame.BoxLeft(day), false, day));
        }

        return labels;
    }

    private static ChartLabel DateOf(IReadOnlyList<StatsDay> days, int day, ChartFrame frame, Func<string, double> width)
    {
        var text = DateText(days[day]);
        return new ChartLabel(text, frame.BoxLeft(day) + (frame.Box / 2) - (width(text) / 2), true, day);
    }

    private static void AddToday(List<ChartLabel> labels, ChartFrame frame, Func<string, double> width, ChartLabel? chosen)
    {
        var last = frame.Days - 1;
        var left = frame.BoxLeft(last) + frame.Box - width("Today");
        if(chosen is { } date && left - (date.Left + width(date.Text)) < _closest)
            return;

        labels.Add(new ChartLabel("Today", left, true, last));
    }

    private static string DateText(StatsDay day)
    {
        return day.Date.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture);
    }
}
