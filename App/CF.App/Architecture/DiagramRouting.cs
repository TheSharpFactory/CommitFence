namespace CF.App.Architecture;

/// <summary>The room one module takes in the diagram. Its place can still move while the rows are being made.</summary>
internal sealed class Slot
{
    public Slot(double x, double width)
    {
        X = x;
        Width = width;
    }

    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Right
    {
        get { return X + Width; }
    }

    public double Middle
    {
        get { return X + (Width / 2); }
    }

    public double Bottom
    {
        get { return Y + DiagramLayout.BoxHeight; }
    }
}

/// <summary>The rows of modules with their places, as far as the routing needs to know them.</summary>
internal sealed class RowGrid
{
    public RowGrid(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        Rows = rows;
        for(var i = 0; i < rows.Count; i++)
        {
            foreach(var name in rows[i])
                RowOf[name] = i;
        }
    }

    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    public Dictionary<string, Slot> Slots { get; } = new Dictionary<string, Slot>(StringComparer.Ordinal);

    public Dictionary<string, int> RowOf { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>The top of each row. The routing moves them down where a gap needs more room.</summary>
    public List<double> RowTop { get; } = new List<double>();

    /// <summary>For a row, the x past which no line may take a place (the red line's lane lies there).</summary>
    public Dictionary<int, double> Blocked { get; } = new Dictionary<int, double>();

    public bool EndsItsRow(string name)
    {
        var row = Rows[RowOf[name]];
        return row[^1] == name;
    }

    /// <summary>For a row, the room kept free above it for the lines drawn over its modules.</summary>
    public Dictionary<int, double> Reserve { get; } = new Dictionary<int, double>();

    /// <summary>The room a row keeps above it, between the lines' tracks and its modules' top edge.</summary>
    public double RoomAbove(int row)
    {
        return Math.Max(DiagramLayout.ArrowGap, Reserve.GetValueOrDefault(row));
    }

    public Slot Last(int row)
    {
        return Slots[Rows[row][^1]];
    }
}

internal enum RouteKind
{
    Uses,
    MayUse,
    Breach
}

/// <summary>One line of one side of the chosen module, from the far module to the chosen one, as it is being worked out.</summary>
internal sealed class Route
{
    public Route(RouteKind kind, string far, int farRow, double farX)
    {
        Kind = kind;
        Far = far;
        FarRow = farRow;
        FarX = farX;
    }

    public RouteKind Kind { get; }

    public string Far { get; }

    public int FarRow { get; }

    public double FarX { get; set; }

    public double X { get; set; }

    public double Ideal { get; set; }

    /// <summary>Where the line crosses each row it passes, by row, from the upper end to the lower one.</summary>
    public SortedDictionary<int, double> At { get; } = new SortedDictionary<int, double>();

    /// <summary>For each gap (named by the row above it), the line's track in it and how many tracks the gap holds.</summary>
    public Dictionary<int, (int Track, int Count)> Tracks { get; } = new Dictionary<int, (int, int)>();

    public Dictionary<int, double> TrackY { get; } = new Dictionary<int, double>();
}

internal sealed record RouteWay(List<DiagramPoint> Points, List<(DiagramPoint From, DiagramPoint To)> Runs, DiagramPoint Start, DiagramPoint End);

/// <summary>
/// Routes the lines of one side of the chosen module (its users above it, or what it may use below it). Rows are worked
/// from the far end towards the chosen module, so a line's order is settled where it starts and never changes: no two
/// lines can cross.
/// </summary>
internal sealed class SideRouter
{
    private const double _step = DiagramLayout.PlaceStep;
    private const double _clear = DiagramLayout.Clear;

    private readonly RowGrid _grid;
    private readonly int _side;
    private readonly int _chosenRow;
    private readonly double _chosenMiddle;

    public SideRouter(RowGrid grid, int side, int chosenRow, double chosenMiddle)
    {
        _grid = grid;
        _side = side;
        _chosenRow = chosenRow;
        _chosenMiddle = chosenMiddle;
    }

    /// <summary>Places each line in every row it passes and lets it meet the chosen module between the two limits.</summary>
    public List<Route> Fan(List<Route> routes, double low, double high, double spacing)
    {
        if(routes.Count == 0)
            return routes;

        SeparateTwinLines(routes);
        var active = new List<Route>();
        foreach(var row in RowsToWork(routes))
        {
            Cross(active, row, routes.Count);
            foreach(var route in routes.Where(route => route.FarRow == row))
            {
                route.X = route.FarX;
                route.At[row] = route.FarX;
                active.Add(route);
            }

            active = active.OrderBy(route => route.X).ToList();
        }

        MeetTheChosenModule(active, low, high, spacing);
        return active;
    }

    /// <summary>Gives each line the track it runs sideways on, in each gap; returns how much each gap must grow.</summary>
    public static Dictionary<int, double> AssignTracks(RowGrid grid, List<Route> routes)
    {
        var growth = new Dictionary<int, double>();
        var byGap = new SortedDictionary<int, List<(Route Route, double From, double To)>>();
        foreach(var route in routes)
        {
            var rows = route.At.Keys.ToList();
            for(var i = 0; i < rows.Count - 1; i++)
            {
                if(!byGap.TryGetValue(rows[i], out var spans))
                {
                    spans = new List<(Route, double, double)>();
                    byGap.Add(rows[i], spans);
                }

                spans.Add((route, route.At[rows[i]], route.At[rows[i + 1]]));
            }
        }

        foreach(var (gap, spans) in byGap)
        {
            var ordered = spans.OrderBy(span => span.From).ThenBy(span => span.To).ToList();
            var tracks = TracksOf(ordered);
            var count = tracks.Max() + 1;
            growth[gap] = Math.Max(0, ((count - 1) * 6) - (DiagramLayout.RowGap - grid.RoomAbove(gap + 1) - 16));
            for(var i = 0; i < ordered.Count; i++)
                ordered[i].Route.Tracks[gap] = (tracks[i], count);
        }

        return growth;
    }

    /// <summary>Turns a track's number into a height, once the rows have moved down where a gap grew.</summary>
    public static void PlaceTracks(RowGrid grid, List<Route> routes)
    {
        foreach(var route in routes)
        {
            foreach(var (gap, slot) in route.Tracks)
            {
                var top = grid.RowTop[gap] + DiagramLayout.BoxHeight;
                var bottom = grid.RowTop[gap + 1] - grid.RoomAbove(gap + 1);
                var spacing = slot.Count > 1 ? Math.Clamp((bottom - top - 16) / (slot.Count - 1), 6, 12) : 0;
                route.TrackY[gap] = ((top + bottom) / 2) - ((slot.Count - 1) / 2.0 * spacing) + (slot.Track * spacing);
            }
        }
    }

    /// <summary>
    /// The line as a path at right angles: straight down, sideways on its track, straight down again, row by row. A start
    /// below the module's edge (headroom) leaves room for an arrow there.
    /// </summary>
    public static RouteWay Way(RowGrid grid, Route route, double headroom)
    {
        var rows = route.At.Keys.ToList();
        var x = route.At[rows[0]];
        var start = new DiagramPoint(x, grid.RowTop[rows[0]] + DiagramLayout.BoxHeight + headroom);
        var points = new List<DiagramPoint> { start };
        for(var i = 1; i < rows.Count; i++)
        {
            var target = route.At[rows[i]];
            var into = grid.RowTop[rows[i]] - DiagramLayout.ArrowGap;
            if(Math.Abs(target - x) >= .5)
            {
                var track = route.TrackY[rows[i - 1]];
                points.Add(new DiagramPoint(x, track));
                points.Add(new DiagramPoint(target, track));
                x = target;
            }

            points.Add(new DiagramPoint(x, into));
            if(i < rows.Count - 1)
                points.Add(new DiagramPoint(x, grid.RowTop[rows[i]] + DiagramLayout.BoxHeight));
        }

        var simple = Straighten(points);
        var end = simple[^1];
        return new RouteWay(simple, RunsOf(simple), new DiagramPoint(simple[0].X, grid.RowTop[rows[0]] + DiagramLayout.BoxHeight), end);
    }

    public static List<(DiagramPoint From, DiagramPoint To)> RunsOf(IReadOnlyList<DiagramPoint> points)
    {
        var runs = new List<(DiagramPoint, DiagramPoint)>();
        for(var i = 0; i < points.Count - 1; i++)
        {
            if(Math.Abs(points[i].X - points[i + 1].X) > .01 || Math.Abs(points[i].Y - points[i + 1].Y) > .01)
                runs.Add((points[i], points[i + 1]));
        }

        return runs;
    }

    private static List<DiagramPoint> Straighten(List<DiagramPoint> points)
    {
        var result = new List<DiagramPoint>();
        foreach(var point in points)
        {
            if(result.Count > 0 && Math.Abs(result[^1].X - point.X) < .01 && Math.Abs(result[^1].Y - point.Y) < .01)
                continue;

            if(result.Count > 1 && Math.Abs(result[^2].X - result[^1].X) < .01 && Math.Abs(result[^1].X - point.X) < .01)
                result.RemoveAt(result.Count - 1);

            result.Add(point);
        }

        return result;
    }

    private static int[] TracksOf(List<(Route Route, double From, double To)> spans)
    {
        var tracks = new int[spans.Count];
        var i = 0;
        while(i < spans.Count)
        {
            var direction = Direction(spans[i]);
            var j = i;
            if(direction > 0)
            {
                while(j + 1 < spans.Count && Direction(spans[j + 1]) > 0 && spans[j + 1].From < spans[j].To + 12)
                    j++;

                for(var k = i; k <= j; k++)
                    tracks[k] = j - k;
            }
            else if(direction < 0)
            {
                while(j + 1 < spans.Count && Direction(spans[j + 1]) < 0 && spans[j + 1].To < spans[j].From + 12)
                    j++;

                for(var k = i; k <= j; k++)
                    tracks[k] = k - i;
            }

            i = j + 1;
        }

        return tracks;
    }

    private static int Direction((Route Route, double From, double To) span)
    {
        return span.To - span.From > .5 ? 1 : span.From - span.To > .5 ? -1 : 0;
    }

    // Two lines that end at one module's edge (the red one and the line between the same two modules) stand 18 apart.
    private static void SeparateTwinLines(List<Route> routes)
    {
        foreach(var route in routes.Where(route => routes.Any(other => other != route && other.Far == route.Far)).ToList())
            route.FarX += route.Kind == RouteKind.Breach ? 9 : -9;
    }

    private IEnumerable<int> RowsToWork(List<Route> routes)
    {
        var end = _side < 0 ? routes.Min(route => route.FarRow) : routes.Max(route => route.FarRow);
        for(var row = end; row != _chosenRow; row -= _side)
            yield return row;
    }

    // The lines already started keep their order and take places of their own in this row, as near as they can to a
    // straight way to the chosen module.
    private void Cross(List<Route> active, int row, int total)
    {
        foreach(var route in active)
        {
            var share = (double)Math.Abs(row - route.FarRow) / Math.Abs(_chosenRow - route.FarRow);
            route.Ideal = route.FarX + ((_chosenMiddle - route.FarX) * share);
        }

        var places = PlacesIn(row, Math.Max(active.Count, total));
        var chosen = Nearest(active.Select(route => route.Ideal).ToList(), places);
        KeepSmallSteps(active, row, chosen);
        for(var i = 0; i < active.Count; i++)
        {
            active[i].X = chosen[i];
            active[i].At[row] = chosen[i];
        }
    }

    // A step sideways of less than 12 reads as a slip: the line keeps its place from the row before when that place is
    // clear of the row's modules, out of the red line's way and at least 16 from its neighbours.
    private void KeepSmallSteps(List<Route> active, int row, double[] chosen)
    {
        for(var i = 0; i < chosen.Length; i++)
        {
            var before = active[i].X;
            var step = Math.Abs(chosen[i] - before);
            if(step >= 12 || step < .5)
                continue;

            var clear = _grid.Rows[row].All(name => before <= _grid.Slots[name].X - _clear || before >= _grid.Slots[name].Right + _clear);
            var allowed = !_grid.Blocked.TryGetValue(row, out var limit) || before <= limit;
            var roomBefore = i == 0 || before - chosen[i - 1] >= 16;
            var roomAfter = i == chosen.Length - 1 || chosen[i + 1] - before >= 16;
            if(clear && allowed && roomBefore && roomAfter)
                chosen[i] = before;
        }
    }

    // In each gap between two modules, from the gap's middle outwards every 24, at least 10 from either module; beyond each
    // end of the row, 16 out and then every 24. A line never runs behind a module.
    private List<double> PlacesIn(int row, int lines)
    {
        var names = _grid.Rows[row];
        var places = new List<double>();
        for(var i = 0; i < names.Count - 1; i++)
        {
            var from = _grid.Slots[names[i]].Right + _clear;
            var to = _grid.Slots[names[i + 1]].X - _clear;
            var middle = (from + to) / 2;
            for(var k = 0; middle + (k * _step) <= to + .01; k++)
            {
                places.Add(middle + (k * _step));
                if(k > 0)
                    places.Add(middle - (k * _step));
            }
        }

        var ends = Math.Max(24, lines + 2);
        var left = _grid.Slots[names[0]].X;
        var right = _grid.Slots[names[^1]].Right;
        var all = new List<double>(places);
        for(var k = 0; k < ends; k++)
        {
            all.Add(left - 16 - (k * _step));
            all.Add(right + 16 + (k * _step));
        }

        var open = all.Where(x => !_grid.Blocked.TryGetValue(row, out var limit) || x <= limit).Order().ToList();
        var inside = open.Where(x => x > -14).ToList();
        return inside.Count >= lines ? inside : open;
    }

    /// <summary>With the lines' order kept, the places that together are nearest to where each line would like to be.</summary>
    private static double[] Nearest(List<double> wanted, List<double> places)
    {
        var n = wanted.Count;
        var m = places.Count;
        var cost = new double[n + 1, m + 1];
        for(var i = 1; i <= n; i++)
        {
            for(var j = 0; j <= m; j++)
                cost[i, j] = 1e12;
        }

        for(var i = 1; i <= n; i++)
        {
            for(var j = i; j <= m; j++)
                cost[i, j] = Math.Min(cost[i, j - 1], cost[i - 1, j - 1] + Math.Abs(places[j - 1] - wanted[i - 1]));
        }

        var got = new double[n];
        var line = n;
        for(var j = m; line > 0 && j > 0; j--)
        {
            if(cost[line, j] != cost[line, j - 1])
            {
                got[line - 1] = places[j - 1];
                line--;
            }
        }

        return got;
    }

    // The lines meet the chosen module in their order, each as near as it can be to the place it comes from, kept inside
    // the limits (clear of the module's corners and of the red bracket's end).
    private void MeetTheChosenModule(List<Route> active, double low, double high, double spacing)
    {
        var count = active.Count;
        var at = active.Select(route => Math.Max(low, Math.Min(high, route.X))).ToList();
        for(var i = 1; i < count; i++)
            at[i] = Math.Max(at[i], at[i - 1] + spacing);

        if(at[count - 1] > high)
        {
            at[count - 1] = high;
            for(var i = count - 2; i >= 0; i--)
                at[i] = Math.Min(at[i], at[i + 1] - spacing);
        }

        for(var i = 0; i < count; i++)
            active[i].At[_chosenRow] = at[i];
    }
}
