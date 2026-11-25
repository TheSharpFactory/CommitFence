using CF.Core.Ports;
using CF.Core.Services;

namespace CF.App.Architecture;

internal readonly record struct DiagramPoint(double X, double Y);

internal sealed record ModuleBox(string Name, int Row, double X, double Y, double Width)
{
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

/// <summary>A row of the diagram: its number counted from the top (the list's layer), where it starts, and whether the chosen module stands in it.</summary>
internal sealed record LayerMark(int Number, double Top, bool HoldsChosen);

internal enum DiagramLineKind
{
    Incoming,
    Outgoing,
    Breach
}

internal enum ArrowPointing
{
    Down,
    Up,
    Left,
    Right
}

/// <summary>An arrow head: its point, and the way it points.</summary>
internal sealed record DiagramArrow(DiagramPoint Tip, ArrowPointing Pointing);

/// <summary>
/// One line of the diagram: its corners (the view rounds them by <paramref name="Radius"/>), its arrow and, for the
/// line of a use that breaks a direction, where its word stands.
/// </summary>
internal sealed record DiagramLine(StructureDirection Direction, DiagramLineKind Kind, IReadOnlyList<DiagramPoint> Points,
    DiagramArrow Arrow, double Radius = 8, DiagramPoint? Word = null);

/// <summary>
/// Places the modules in rows and routes the chosen module's lines, with no interface code in it. Whatever the modules and
/// their allowed directions, no two lines cross or run on one another, none passes through a module, and the canvas holds
/// everything.
/// </summary>
internal sealed class DiagramLayout
{
    public const double BoxHeight = 36;
    public const double PlaceStep = 24;
    public const double Clear = 10;
    public const double RowGap = 56;
    public const double ArrowGap = 8;
    private const double _usesPin = 22;
    private const double _mayUsePort = 16;
    private const double _breachRoom = 72;
    private const double _wordWidth = 54;
    private const double _wordHeight = 22;
    private const double _bracketStart = 16;
    private const double _bracketStep = 10;

    private readonly List<DiagramLine> _lines = new List<DiagramLine>();
    private readonly Dictionary<string, ModuleBox> _boxes = new Dictionary<string, ModuleBox>(StringComparer.Ordinal);
    private readonly string _chosen;
    private readonly StructureBreach? _marked;
    private readonly StructureBreach? _breach;
    private readonly RowGrid _grid;
    private readonly List<SameRowUse> _sameRow = new List<SameRowUse>();
    private BreachForm _form;
    private double _canvasWidth;
    private double _lane;

    private enum BreachForm
    {
        None,
        Loop,
        Above,
        Below,
        SameRow
    }

    // A line between the chosen module and another module of its row: an allowed direction, or the use that breaks one.
    // Side is -1 for a module on the left, 1 on the right; Distance counts the places between them, one for neighbours.
    private sealed record SameRowUse(StructureDirection Direction, DiagramLineKind Kind, string Partner, int Side, int Distance);

    // Where a line over the row stands: its place among the lines of its side (the chosen module's port), among the lines
    // that end at the same partner (the partner's port), and its height.
    private sealed record Bracket(SameRowUse Use, int Port, int PartnerPort);

    /// <param name="layers">The rows of modules, the top first.</param>
    /// <param name="chosen">The module whose lines are drawn.</param>
    /// <param name="directions">Every allowed direction.</param>
    /// <param name="breach">The use that breaks a direction; its line is drawn when the chosen module is one of its ends.</param>
    /// <param name="viewport">The width of the panel the diagram stands in.</param>
    public DiagramLayout(ModuleLayers layers, string chosen, IReadOnlyList<StructureDirection> directions, StructureBreach? breach, double viewport)
    {
        _chosen = chosen;
        _marked = breach;
        _grid = new RowGrid(layers.Rows);
        if(!layers.Level.ContainsKey(chosen))
            return;

        _breach = breach != null && breach.FromModule != breach.ToModule && (breach.FromModule == chosen || breach.ToModule == chosen) &&
            layers.Level.ContainsKey(breach.FromModule) && layers.Level.ContainsKey(breach.ToModule) ? breach : null;
        var chosenRow = _grid.RowOf[chosen];
        var users = directions.Where(edge => edge.To == chosen && _grid.RowOf.TryGetValue(edge.From, out var row) && row < chosenRow).Distinct().ToList();
        var uses = directions.Where(edge => edge.From == chosen && _grid.RowOf.TryGetValue(edge.To, out var row) && row > chosenRow).Distinct().ToList();
        ChooseForm();
        _sameRow.AddRange(FindSameRowUses(directions));
        PlaceRows(viewport, users.Count, uses.Count);
        RouteLines(users, uses);
        IncludeEverything();
    }

    public IReadOnlyDictionary<string, ModuleBox> Boxes
    {
        get { return _boxes; }
    }

    public IReadOnlyList<DiagramLine> Lines
    {
        get { return _lines; }
    }

    public double Width { get; private set; }

    public double Height { get; private set; }

    /// <summary>The rows, numbered from the top as the layers of the list are, each with its top and whether it holds the chosen module.</summary>
    public IReadOnlyList<LayerMark> Layers
    {
        get
        {
            return _boxes.Values.GroupBy(box => box.Row).OrderBy(group => group.Key)
                .Select(group => new LayerMark(group.Key + 1, group.Min(box => box.Y), group.Any(box => box.Name == _chosen))).ToList();
        }
    }

    public string? Nearest(string selected, int horizontal, int vertical)
    {
        var origin = _boxes[selected];
        return _boxes.Values.Where(box => horizontal != 0
                ? (box.Middle - origin.Middle) * horizontal > 0
                : (box.Y - origin.Y) * vertical > 0)
            .OrderBy(box => Distance(box, origin, horizontal != 0))
            .ThenBy(box => box.Name, StringComparer.Ordinal).Select(box => box.Name).FirstOrDefault();
    }

    private static double Distance(ModuleBox to, ModuleBox from, bool horizontal)
    {
        var dx = to.Middle - from.Middle;
        var dy = to.Y - from.Y;
        return horizontal ? (dx * dx) + (4 * dy * dy) : (4 * dx * dx) + (dy * dy);
    }

    private string Far
    {
        get { return _breach!.FromModule == _chosen ? _breach.ToModule : _breach.FromModule; }
    }

    // The red line takes the first form that fits. The loop: its two modules are in different rows and each ends its row.
    // Otherwise it is one more line of the side its other module is on, or a line in the row they share.
    private void ChooseForm()
    {
        if(_breach == null)
            return;

        var from = _grid.RowOf[_breach.FromModule];
        var to = _grid.RowOf[_breach.ToModule];
        var far = _grid.RowOf[Far];
        var chosen = _grid.RowOf[_chosen];
        _form = from != to && _grid.EndsItsRow(_breach.FromModule) && _grid.EndsItsRow(_breach.ToModule) ? BreachForm.Loop :
            far < chosen ? BreachForm.Above : far > chosen ? BreachForm.Below : BreachForm.SameRow;
    }

    // Every line in the chosen module's row, the nearest partners first: the allowed directions to or from a module of the
    // row (modules that may use each other share a row) and, in the red line's row form, the use that breaks a direction.
    private List<SameRowUse> FindSameRowUses(IReadOnlyList<StructureDirection> directions)
    {
        var chosenRow = _grid.RowOf[_chosen];
        var row = _grid.Rows[chosenRow].ToList();
        var found = new List<SameRowUse>();
        foreach(var edge in directions.Distinct().Where(edge => edge.From != edge.To && (edge.From == _chosen || edge.To == _chosen)))
        {
            var partner = edge.From == _chosen ? edge.To : edge.From;
            if(_grid.RowOf.TryGetValue(partner, out var partnerRow) && partnerRow == chosenRow)
                found.Add(UseOf(edge, edge.From == _chosen ? DiagramLineKind.Outgoing : DiagramLineKind.Incoming, partner, row));
        }

        if(_form == BreachForm.SameRow)
            found.Add(UseOf(new StructureDirection(_breach!.FromModule, _breach.ToModule), DiagramLineKind.Breach, Far, row));

        return found.OrderBy(use => use.Distance).ThenBy(use => (int)use.Kind).ThenBy(use => use.Partner, StringComparer.Ordinal).ToList();
    }

    private SameRowUse UseOf(StructureDirection direction, DiagramLineKind kind, string partner, List<string> row)
    {
        var apart = row.IndexOf(partner) - row.IndexOf(_chosen);
        return new SameRowUse(direction, kind, partner, Math.Sign(apart), Math.Abs(apart));
    }

    private List<SameRowUse> Over(int side)
    {
        return _sameRow.Where(use => use.Side == side && use.Distance > 1).ToList();
    }

    private double WidthOf(string name, int users, int uses)
    {
        // A name's width: Cascadia Mono sets 13 pixel text 7.62 wide a character; 7.7 leaves room for the faces that stand in for it.
        var width = (name.Length * 7.7) + 22 + (name == _marked?.FromModule ? 20 : 0);
        if(name != _chosen)
            return RoomForEnds(name, width);

        var below = uses + (_form == BreachForm.Below ? 1 : 0);
        var above = users + (_form == BreachForm.Above ? 1 : 0) + Over(-1).Count + Over(1).Count;
        return Math.Max(width + 2, Math.Max((below * _mayUsePort) + 24, (above * _usesPin) + 24));
    }

    // A module at the far end of lines over the row keeps a port of its own for each.
    private double RoomForEnds(string name, double width)
    {
        var ends = Math.Max(Over(-1).Count(use => use.Partner == name), Over(1).Count(use => use.Partner == name));
        return Math.Max(width, (ends * _usesPin) + 20);
    }

    // Every layer is one row, never wrapped. A row of n modules is spread over the canvas: each module in the middle of its
    // own n-th when that leaves at least 32 between neighbours and both outer modules inside the canvas, otherwise evenly
    // spaced, at least 20 apart, filling the width. While a use breaks a direction, the right 72 are kept free for the red line's lane.
    private void PlaceRows(double viewport, int users, int uses)
    {
        var widths = _grid.Rows.SelectMany(row => row).ToDictionary(name => name, name => WidthOf(name, users, uses), StringComparer.Ordinal);
        var reserve = _marked == null ? 0 : _breachRoom;
        var need = _grid.Rows.Select(row => Math.Ceiling(row.Sum(name => widths[name]) + (20 * (row.Count - 1)) + 16)).DefaultIfEmpty(0).Max();
        var room = Math.Max(viewport - reserve, need);
        _canvasWidth = Math.Max(viewport, need + reserve);
        var top = 0.0;
        foreach(var row in _grid.Rows)
        {
            var slots = SpreadRow(row, widths, room);
            for(var i = 0; i < row.Count; i++)
            {
                slots[i].Y = top;
                _grid.Slots[row[i]] = slots[i];
            }

            _grid.RowTop.Add(top);
            top += BoxHeight + RowGap;
        }

        Height = top - RowGap;
        if(_form == BreachForm.Loop)
            MakeLane();
    }

    private static List<Slot> SpreadRow(IReadOnlyList<string> row, Dictionary<string, double> widths, double room)
    {
        var n = row.Count;
        var column = (room - 16) / n;
        var even = column >= widths[row[0]] && column >= widths[row[^1]];
        for(var i = 0; i < n - 1; i++)
            even &= column - ((widths[row[i]] + widths[row[i + 1]]) / 2) >= 32;

        if(even)
            return row.Select((name, i) => new Slot(8 + ((i + .5) * column) - (widths[name] / 2), widths[name])).ToList();

        var sum = row.Sum(name => widths[name]);
        var gap = n > 1 ? Math.Max(20, (room - 16 - sum) / (n - 1)) : 0;
        var x = (room - sum - (gap * (n - 1))) / 2;
        var slots = new List<Slot>();
        foreach(var name in row)
        {
            slots.Add(new Slot(x, widths[name]));
            x += widths[name] + gap;
        }

        return slots;
    }

    // The loop's lane is 48 right of the rightmost module of every row from one end to the other. Other lines keep out of
    // its way: in the rows between, right of the lane less 24; in the row of each end, right of that module.
    private void MakeLane()
    {
        var first = Math.Min(_grid.RowOf[_breach!.FromModule], _grid.RowOf[_breach.ToModule]);
        var last = Math.Max(_grid.RowOf[_breach.FromModule], _grid.RowOf[_breach.ToModule]);
        _lane = Enumerable.Range(first, last - first + 1).Max(row => _grid.Last(row).Right) + 48;
        _canvasWidth = Math.Max(_canvasWidth, _lane + 24);
        for(var row = first + 1; row < last; row++)
            _grid.Blocked[row] = _lane - 24;

        foreach(var name in new List<string> { _breach.FromModule, _breach.ToModule })
            _grid.Blocked[_grid.RowOf[name]] = _grid.Slots[name].Right;
    }

    private void RouteLines(List<StructureDirection> users, List<StructureDirection> uses)
    {
        var chosenRow = _grid.RowOf[_chosen];
        var chosen = _grid.Slots[_chosen];
        var above = users.Select(edge => NewRoute(RouteKind.Uses, edge.From)).ToList();
        var below = uses.Select(edge => NewRoute(RouteKind.MayUse, edge.To)).ToList();
        if(_form == BreachForm.Above)
            above.Add(NewRoute(RouteKind.Breach, Far));

        if(_form == BreachForm.Below)
            below.Add(NewRoute(RouteKind.Breach, Far));

        var topRouter = new SideRouter(_grid, -1, chosenRow, chosen.Middle);
        var bottomRouter = new SideRouter(_grid, 1, chosenRow, chosen.Middle);
        var topLimits = TopLimits(chosen);
        var upper = topRouter.Fan(above, topLimits.Low, topLimits.High, _usesPin);
        var lower = bottomRouter.Fan(below, chosen.X + 12, chosen.Right - 12, _mayUsePort);
        KeepRoomOverRow(chosenRow);
        var growth = SideRouter.AssignTracks(_grid, upper);
        foreach(var (gap, extra) in SideRouter.AssignTracks(_grid, lower))
            growth[gap] = Math.Max(growth.GetValueOrDefault(gap), extra);

        if(chosenRow > 0)
            growth[chosenRow - 1] = Math.Max(growth.GetValueOrDefault(chosenRow - 1), RoomOverRow() + 20 - RowGap);

        MoveRowsDown(growth);
        SideRouter.PlaceTracks(_grid, upper);
        SideRouter.PlaceTracks(_grid, lower);
        foreach(var route in upper.Where(route => route.Kind == RouteKind.Uses))
            AddPlain(route, DiagramLineKind.Incoming, new StructureDirection(route.Far, _chosen));

        foreach(var route in lower.Where(route => route.Kind == RouteKind.MayUse))
            AddPlain(route, DiagramLineKind.Outgoing, new StructureDirection(_chosen, route.Far));

        foreach(var route in upper.Concat(lower).Where(route => route.Kind == RouteKind.Breach))
            AddBreachInOrder(route, upper.Contains(route) ? -1 : 1);

        AddLoopForm();
        AddSameRowLines();
        foreach(var name in _grid.Rows.SelectMany(row => row))
            CopyBox(name);
    }

    private (double Low, double High) TopLimits(Slot chosen)
    {
        return (chosen.X + 12 + (Over(-1).Count * _usesPin), chosen.Right - 12 - (Over(1).Count * _usesPin));
    }

    // The highest line over the row stands this far above it: 16 for the first, 10 more for each one above it.
    private double RoomOverRow()
    {
        var levels = Math.Max(Over(-1).Count, Over(1).Count);
        return levels == 0 ? 0 : _bracketStart + (_bracketStep * (levels - 1));
    }

    // The tracks of the lines coming down to the chosen module keep clear of the lines over its row.
    private void KeepRoomOverRow(int chosenRow)
    {
        if(RoomOverRow() > 0)
            _grid.Reserve[chosenRow] = RoomOverRow() + 6;
    }

    private Route NewRoute(RouteKind kind, string far)
    {
        return new Route(kind, far, _grid.RowOf[far], _grid.Slots[far].Middle);
    }

    // A gap that needs more than six tracks grows by what its tracks lack, and the rows below move down.
    private void MoveRowsDown(Dictionary<int, double> growth)
    {
        var added = 0.0;
        for(var row = 0; row < _grid.Rows.Count; row++)
        {
            _grid.RowTop[row] += added;
            foreach(var name in _grid.Rows[row])
                _grid.Slots[name].Y = _grid.RowTop[row];

            added += growth.GetValueOrDefault(row);
        }

        Height += added;
    }

    private void CopyBox(string name)
    {
        var slot = _grid.Slots[name];
        _boxes[name] = new ModuleBox(name, _grid.RowOf[name], slot.X, slot.Y, slot.Width);
    }

    private void AddPlain(Route route, DiagramLineKind kind, StructureDirection direction)
    {
        var way = SideRouter.Way(_grid, route, 0);
        var tip = new DiagramPoint(way.End.X, way.End.Y + ArrowGap);
        _lines.Add(new DiagramLine(direction, kind, way.Points, new DiagramArrow(tip, ArrowPointing.Down)));
    }

    // The red line in the order of its side. Its arrow points at the module that may not be used: at the far module, or at
    // the chosen one when that is the module used.
    private void AddBreachInOrder(Route route, int side)
    {
        var atFar = _breach!.ToModule == route.Far;
        var arrowAtStart = side < 0 ? atFar : !atFar;
        var way = SideRouter.Way(_grid, route, arrowAtStart ? 10 : 0);
        var direction = new StructureDirection(_breach.FromModule, _breach.ToModule);
        var arrow = arrowAtStart
            ? new DiagramArrow(new DiagramPoint(way.Start.X, way.Start.Y), ArrowPointing.Up)
            : new DiagramArrow(new DiagramPoint(way.End.X, way.End.Y + ArrowGap), ArrowPointing.Down);
        var fallback = side < 0
            ? new DiagramPoint(way.Start.X, way.Start.Y + (RowGap / 2))
            : new DiagramPoint(way.End.X, way.End.Y - (RowGap / 2) + 8);
        _lines.Add(new DiagramLine(direction, DiagramLineKind.Breach, way.Points, arrow, 8, WordAt(way.Runs, fallback)));
    }

    private void AddLoopForm()
    {
        if(_breach == null)
            return;

        var from = _grid.Slots[_breach.FromModule];
        var to = _grid.Slots[_breach.ToModule];
        var direction = new StructureDirection(_breach.FromModule, _breach.ToModule);
        if(_form == BreachForm.Loop)
            AddLoop(direction, from, to);
    }

    // Out of the user's right edge at mid-height, right to the lane, along it, and left into the right edge of the module
    // it may not use. Nothing else uses the lane or the rows' right ends that it passes.
    private void AddLoop(StructureDirection direction, Slot from, Slot to)
    {
        var fromY = from.Y + (BoxHeight / 2);
        var toY = to.Y + (BoxHeight / 2);
        var points = new List<DiagramPoint>
        {
            new DiagramPoint(from.Right, fromY), new DiagramPoint(_lane, fromY), new DiagramPoint(_lane, toY), new DiagramPoint(to.Right + 10, toY)
        };
        var runs = new List<(DiagramPoint, DiagramPoint)> { (points[1], points[2]) };
        _lines.Add(new DiagramLine(direction, DiagramLineKind.Breach, points, new DiagramArrow(new DiagramPoint(to.Right, toY), ArrowPointing.Left),
            12, WordAt(runs, null)));
    }

    // Neighbours are joined by straight lines between their facing edges, a second one when each may use the other. Modules
    // with others between them are joined by lines over the row: up from the user's top edge, along, and down into the
    // used module's top edge. The nearest partner's line is the lowest and leaves the chosen module at its outer corner,
    // so lines over the same stretch nest and never cross. The red line is drawn last, so that its word finds a free place.
    private void AddSameRowLines()
    {
        var plans = new List<(SameRowUse Use, Bracket Over, int Level, int Index, int Count)>();
        foreach(var side in new List<int> { -1, 1 })
        {
            var over = Over(side);
            for(var i = 0; i < over.Count; i++)
                plans.Add((over[i], new Bracket(over[i], i, over.Take(i).Count(other => other.Partner == over[i].Partner)), i, 0, 1));
        }

        foreach(var group in _sameRow.Where(use => use.Distance == 1).GroupBy(use => use.Partner))
        {
            var uses = group.ToList();
            for(var i = 0; i < uses.Count; i++)
                plans.Add((uses[i], new Bracket(uses[i], 0, 0), 0, i, uses.Count));
        }

        foreach(var plan in plans.OrderBy(plan => plan.Use.Kind == DiagramLineKind.Breach ? 1 : 0))
        {
            if(plan.Use.Distance > 1)
                AddOverRow(plan.Over, plan.Level);
            else
                AddStraight(plan.Use, plan.Index, plan.Count);
        }
    }

    // Between neighbours, at mid-height; two lines stand 12 apart.
    private void AddStraight(SameRowUse use, int index, int count)
    {
        var chosen = _grid.Slots[_chosen];
        var partner = _grid.Slots[use.Partner];
        var userIsChosen = use.Direction.From == _chosen;
        var from = userIsChosen ? chosen : partner;
        var to = userIsChosen ? partner : chosen;
        var toRight = to.Middle > from.Middle;
        var y = chosen.Y + (count == 1 ? BoxHeight / 2 : 12 + (12 * index));
        var breach = use.Kind == DiagramLineKind.Breach;
        var gap = breach ? 10 : ArrowGap;
        var points = new List<DiagramPoint>
        {
            new DiagramPoint(toRight ? from.Right : from.X, y), new DiagramPoint(toRight ? to.X - gap : to.Right + gap, y)
        };
        var arrow = new DiagramArrow(new DiagramPoint(toRight ? to.X : to.Right, y), toRight ? ArrowPointing.Right : ArrowPointing.Left);
        var fallback = new DiagramPoint(points[1].X, chosen.Y - (RowGap / 2));
        _lines.Add(new DiagramLine(use.Direction, use.Kind, points, arrow, 12, breach ? WordAt(SideRouter.RunsOf(points), fallback) : null));
    }

    private void AddOverRow(Bracket line, int level)
    {
        var use = line.Use;
        var chosen = _grid.Slots[_chosen];
        var partner = _grid.Slots[use.Partner];
        var toRight = use.Side > 0;
        var chosenX = toRight ? chosen.Right - 10 - (line.Port * _usesPin) : chosen.X + 10 + (line.Port * _usesPin);
        var partnerX = toRight ? partner.X + 10 + (line.PartnerPort * _usesPin) : partner.Right - 10 - (line.PartnerPort * _usesPin);
        var (fromX, toX) = use.Direction.From == _chosen ? (chosenX, partnerX) : (partnerX, chosenX);
        var over = chosen.Y - _bracketStart - (_bracketStep * level);
        var breach = use.Kind == DiagramLineKind.Breach;
        var gap = breach ? 10 : ArrowGap;
        var points = new List<DiagramPoint>
        {
            new DiagramPoint(fromX, chosen.Y), new DiagramPoint(fromX, over), new DiagramPoint(toX, over), new DiagramPoint(toX, chosen.Y - gap)
        };
        var arrow = new DiagramArrow(new DiagramPoint(toX, chosen.Y), ArrowPointing.Down);
        var fallback = new DiagramPoint(toX, chosen.Y - (RowGap / 2));
        _lines.Add(new DiagramLine(use.Direction, use.Kind, points, arrow, 12, breach ? WordAt(SideRouter.RunsOf(points), fallback, true) : null));
    }

    // The word stands on the line's longest upright part when that is at least 40 long; otherwise on its longest sideways
    // part when that is at least 64 long (for a line over the row, at the place nearest the middle that no other line
    // runs under); otherwise 37 right of the line's far end.
    private DiagramPoint WordAt(List<(DiagramPoint From, DiagramPoint To)> runs, DiagramPoint? fallback, bool keepClear = false)
    {
        var upright = runs.Where(run => Math.Abs(run.From.X - run.To.X) < .5).OrderByDescending(run => Math.Abs(run.To.Y - run.From.Y))
            .Select(run => ((DiagramPoint From, DiagramPoint To)?)run).FirstOrDefault();
        var sideways = runs.Where(run => Math.Abs(run.From.X - run.To.X) >= .5).OrderByDescending(run => Math.Abs(run.To.X - run.From.X))
            .Select(run => ((DiagramPoint From, DiagramPoint To)?)run).FirstOrDefault();
        if(upright is { } up && Math.Abs(up.To.Y - up.From.Y) >= 40)
            return new DiagramPoint(up.From.X, (up.From.Y + up.To.Y) / 2);

        if(sideways is { } side && Math.Abs(side.To.X - side.From.X) >= 64)
        {
            var middle = new DiagramPoint((side.From.X + side.To.X) / 2, side.From.Y);
            if(!keepClear)
                return middle;

            if(ClearPlaceOnRun(side.From, side.To) is { } place)
                return place;
        }

        var far = fallback ?? new DiagramPoint(0, 0);
        return new DiagramPoint(far.X + 37, far.Y);
    }

    // Along a sideways run, the place nearest its middle where the word's shape touches no line drawn so far.
    private DiagramPoint? ClearPlaceOnRun(DiagramPoint from, DiagramPoint to)
    {
        var low = Math.Min(from.X, to.X) + (_wordWidth / 2) + 6;
        var high = Math.Max(from.X, to.X) - (_wordWidth / 2) - 6;
        var middle = (from.X + to.X) / 2;
        for(var step = 0; middle - step >= low || middle + step <= high; step += 8)
        {
            foreach(var x in new List<double> { middle - step, middle + step })
            {
                if(x >= low && x <= high && !TouchesALine(new DiagramPoint(x, from.Y)))
                    return new DiagramPoint(x, from.Y);
            }
        }

        return null;
    }

    private bool TouchesALine(DiagramPoint centre)
    {
        var halfWidth = (_wordWidth / 2) + 6;
        var halfHeight = (_wordHeight / 2) + 4;
        return _lines.SelectMany(line => SideRouter.RunsOf(line.Points)).Any(run =>
            Math.Max(run.From.X, run.To.X) >= centre.X - halfWidth && Math.Min(run.From.X, run.To.X) <= centre.X + halfWidth &&
            Math.Max(run.From.Y, run.To.Y) >= centre.Y - halfHeight && Math.Min(run.From.Y, run.To.Y) <= centre.Y + halfHeight);
    }

    // The canvas holds every module, line, arrow and word: it grows to the right and downwards, and everything moves right
    // or down when a module, a line or a word stands left of or above the first module.
    private void IncludeEverything()
    {
        var reach = new List<DiagramPoint>();
        foreach(var box in _boxes.Values)
        {
            reach.Add(new DiagramPoint(box.X, box.Y));
            reach.Add(new DiagramPoint(box.Right, box.Bottom));
        }

        foreach(var line in _lines)
        {
            reach.AddRange(line.Points.SelectMany(point => new List<DiagramPoint> { new DiagramPoint(point.X - 4, point.Y - 4), new DiagramPoint(point.X + 4, point.Y + 4) }));
            reach.Add(new DiagramPoint(line.Arrow.Tip.X - 6, line.Arrow.Tip.Y - 10));
            reach.Add(new DiagramPoint(line.Arrow.Tip.X + 6, line.Arrow.Tip.Y + 10));
            if(line.Word is { } word)
            {
                reach.Add(new DiagramPoint(word.X - (_wordWidth / 2) - 3, word.Y - (_wordHeight / 2) - 3));
                reach.Add(new DiagramPoint(word.X + (_wordWidth / 2) + 3, word.Y + (_wordHeight / 2) + 3));
            }
        }

        var shift = Math.Max(0, -reach.Select(point => point.X).DefaultIfEmpty(0).Min());
        var down = Math.Max(0, -reach.Select(point => point.Y).DefaultIfEmpty(0).Min());
        Width = Math.Max(_canvasWidth, reach.Select(point => point.X).DefaultIfEmpty(0).Max()) + shift;
        Height = Math.Max(Height, reach.Select(point => point.Y).DefaultIfEmpty(0).Max()) + down;
        if(shift == 0 && down == 0)
            return;

        foreach(var name in _boxes.Keys.ToList())
            _boxes[name] = _boxes[name] with { X = _boxes[name].X + shift, Y = _boxes[name].Y + down };

        for(var i = 0; i < _lines.Count; i++)
            _lines[i] = Moved(_lines[i], shift, down);
    }

    private static DiagramLine Moved(DiagramLine line, double dx, double dy)
    {
        return line with
        {
            Points = line.Points.Select(point => new DiagramPoint(point.X + dx, point.Y + dy)).ToList(),
            Arrow = line.Arrow with { Tip = new DiagramPoint(line.Arrow.Tip.X + dx, line.Arrow.Tip.Y + dy) },
            Word = line.Word is { } word ? new DiagramPoint(word.X + dx, word.Y + dy) : null
        };
    }
}
