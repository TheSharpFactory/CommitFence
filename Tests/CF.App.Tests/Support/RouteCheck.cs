using CF.App.Architecture;

namespace CF.App.Tests.Support;

/// <summary>Checks a placed diagram: no two lines touch or run on one another, none passes through a module, all is inside the canvas.</summary>
internal static class RouteCheck
{
    private const double _epsilon = .01;

    /// <summary>The first defect found, or an empty string.</summary>
    public static string Problem(DiagramLayout layout)
    {
        var lines = layout.Lines.Select(line => Segments(line.Points)).ToList();
        for(var i = 0; i < lines.Count; i++)
        {
            for(var j = i + 1; j < lines.Count; j++)
            {
                if(Between(lines[i], lines[j]) is { } problem)
                    return $"lines {i} and {j}: {problem}";
            }
        }

        foreach(var line in layout.Lines)
        {
            if(line.Points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.X < 0 || point.X > layout.Width || point.Y < 0 || point.Y > layout.Height))
                return "a line leaves the canvas";
        }

        foreach(var box in layout.Boxes.Values)
        {
            if(box.X < 0 || box.Right > layout.Width || box.Y < 0 || box.Bottom > layout.Height)
                return $"{box.Name} leaves the canvas";

            if(lines.Any(line => line.Any(segment => Through(segment, box))))
                return $"a line passes through {box.Name}";
        }

        return string.Empty;
    }

    private static List<(DiagramPoint From, DiagramPoint To)> Segments(IReadOnlyList<DiagramPoint> points)
    {
        return points.Zip(points.Skip(1)).Where(pair => pair.First != pair.Second).ToList();
    }

    private static string? Between(List<(DiagramPoint From, DiagramPoint To)> first, List<(DiagramPoint From, DiagramPoint To)> second)
    {
        foreach(var a in first)
        {
            foreach(var b in second)
            {
                var upright = Upright(a);
                if(upright == Upright(b))
                {
                    if(Overlap(a, b, upright))
                        return "overlap";

                    continue;
                }

                var (vertical, horizontal) = upright ? (a, b) : (b, a);
                if(Within(vertical.From.X, horizontal.From.X, horizontal.To.X) && Within(horizontal.From.Y, vertical.From.Y, vertical.To.Y))
                    return "cross";
            }
        }

        return null;
    }

    private static bool Upright((DiagramPoint From, DiagramPoint To) segment)
    {
        return Math.Abs(segment.From.X - segment.To.X) < _epsilon;
    }

    private static bool Within(double value, double one, double other)
    {
        return value >= Math.Min(one, other) - _epsilon && value <= Math.Max(one, other) + _epsilon;
    }

    private static bool Overlap((DiagramPoint From, DiagramPoint To) a, (DiagramPoint From, DiagramPoint To) b, bool upright)
    {
        var across = upright ? Math.Abs(a.From.X - b.From.X) : Math.Abs(a.From.Y - b.From.Y);
        if(across >= 4)
            return false;

        var (aLow, aHigh) = Range(a, upright);
        var (bLow, bHigh) = Range(b, upright);
        return Math.Min(aHigh, bHigh) - Math.Max(aLow, bLow) > 1;
    }

    private static (double Low, double High) Range((DiagramPoint From, DiagramPoint To) segment, bool upright)
    {
        var one = upright ? segment.From.Y : segment.From.X;
        var other = upright ? segment.To.Y : segment.To.X;
        return (Math.Min(one, other), Math.Max(one, other));
    }

    private static bool Through((DiagramPoint From, DiagramPoint To) segment, ModuleBox box)
    {
        var left = box.X + 1;
        var right = box.Right - 1;
        var top = box.Y + 1;
        var bottom = box.Bottom - 1;
        if(Upright(segment))
            return segment.From.X > left && segment.From.X < right && Math.Max(segment.From.Y, segment.To.Y) > top && Math.Min(segment.From.Y, segment.To.Y) < bottom;

        return segment.From.Y > top && segment.From.Y < bottom && Math.Max(segment.From.X, segment.To.X) > left && Math.Min(segment.From.X, segment.To.X) < right;
    }
}
