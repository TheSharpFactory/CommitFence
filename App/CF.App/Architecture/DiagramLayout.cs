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

internal enum DiagramLineKind
{
    Incoming,
    Outgoing,
    Breach
}

internal sealed record DiagramLine(StructureDirection Direction, DiagramLineKind Kind, IReadOnlyList<DiagramPoint> Points, bool ArrowUp = false);

/// <summary>Places boxes and routes the selected module's connections independently of the desktop view.</summary>
internal sealed class DiagramLayout
{
    public const double BoxHeight = 36;
    private const double _incomingPort = 22;
    private const double _outgoingPort = 16;
    private const double _laneGap = 40;
    private const double _arrowLength = 8;

    private readonly List<List<ModuleBox>> _rows = new List<List<ModuleBox>>();
    private readonly Dictionary<string, ModuleBox> _boxes = new Dictionary<string, ModuleBox>(StringComparer.Ordinal);
    private readonly List<DiagramLine> _lines = new List<DiagramLine>();

    public DiagramLayout(ModuleLayers layers, string chosen, IReadOnlyList<StructureDirection> directions, StructureBreach? breach, double viewport)
    {
        var incoming = directions.Where(edge => edge.To == chosen).ToList();
        var outgoing = directions.Where(edge => edge.From == chosen).ToList();
        var widths = layers.Order.ToDictionary(name => name, name => WidthOf(name, chosen, incoming.Count, outgoing.Count, breach), StringComparer.Ordinal);
        var bodyWidth = layers.Rows.Select(row => row.Sum(name => widths[name]) + (20 * (row.Count - 1)) + 16).DefaultIfEmpty(0).Max();
        Width = Math.Max(viewport, bodyWidth);
        if(!layers.Level.TryGetValue(chosen, out var level))
            return;

        var acrossIn = incoming.Where(edge => layers.Level[edge.From] == level).ToList();
        var acrossOut = outgoing.Where(edge => layers.Level[edge.To] == level).ToList();
        var rowGap = Math.Max(56, 24 + (Math.Max(acrossIn.Count, acrossOut.Count) * 12));
        PlaceRows(layers, widths, Width, rowGap);
        if(!_boxes.TryGetValue(chosen, out var selected))
            return;

        RouteIncoming(incoming.Where(edge => _boxes[edge.From].Row < selected.Row).ToList(), selected, breach != null, acrossIn.Count);
        RouteAcross(selected, acrossIn, incoming.Count, true);
        RouteOutgoing(outgoing.Where(edge => _boxes[edge.To].Row > selected.Row).ToList(), selected, breach, acrossOut.Count);
        RouteAcross(selected, acrossOut, outgoing.Count, false);
        if(breach != null && _boxes.TryGetValue(breach.FromModule, out var from) && _boxes.TryGetValue(breach.ToModule, out var to))
            RouteBreach(from, to);

        IncludeLines();
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

    private static double WidthOf(string name, string chosen, int incoming, int outgoing, StructureBreach? breach)
    {
        var width = (name.Length * 8.0) + 26 + (name == breach?.FromModule ? 20 : 0);
        return name == chosen ? Math.Max(width + 6, Math.Max((incoming * _incomingPort) + 24, (outgoing * _outgoingPort) + 24)) : width;
    }

    private void PlaceRows(ModuleLayers layers, Dictionary<string, double> widths, double width, double rowGap)
    {
        foreach(var row in layers.Rows)
        {
            var sum = row.Sum(name => widths[name]);
            var gaps = row.Count - 1;
            var gap = gaps == 0 ? 0 : Math.Clamp((width - 16 - sum) / gaps, 20, 112);
            var x = (width - sum - (gap * gaps)) / 2;
            var index = _rows.Count;
            var boxes = new List<ModuleBox>();
            foreach(var name in row)
            {
                var box = new ModuleBox(name, index, x, index * (BoxHeight + rowGap), widths[name]);
                _boxes.Add(name, box);
                boxes.Add(box);
                x += box.Width + gap;
            }

            _rows.Add(boxes);
        }

        Height = _rows.Count == 0 ? 0 : (_rows.Count * (BoxHeight + rowGap)) - rowGap;
    }

    private void RouteIncoming(List<StructureDirection> incoming, ModuleBox selected, bool reserveRight, int extra)
    {
        var sources = incoming.Select(edge => _boxes[edge.From]).OrderBy(box => box.Middle).ThenBy(box => box.Row).ToList();
        var lanes = new Dictionary<string, double>(StringComparer.Ordinal);
        if(reserveRight || !TryNaturalLanes(sources, selected, lanes))
        {
            lanes.Clear();
            var earlierLane = double.PositiveInfinity;
            foreach(var box in sources.Where(box => box.Row < selected.Row - 1).OrderByDescending(box => box.Row).ThenByDescending(box => box.Middle))
            {
                var left = _rows.Skip(box.Row + 1).Take(selected.Row - box.Row - 1).Min(row => row[0].X);
                var lane = Math.Min(left - 16, earlierLane - _laneGap);
                lanes.Add(box.Name, lane);
                earlierLane = lane;
            }
        }

        var ordered = sources.OrderBy(source => lanes.GetValueOrDefault(source.Name, source.Middle)).ToList();
        for(var i = 0; i < ordered.Count; i++)
        {
            var source = ordered[i];
            var path = new List<DiagramPoint> { new DiagramPoint(source.Middle, source.Bottom) };
            if(lanes.TryGetValue(source.Name, out var lane))
            {
                path.Add(new DiagramPoint(lane, source.Bottom + 48));
                path.Add(new DiagramPoint(lane, _rows[selected.Row - 1][0].Bottom));
            }

            path.Add(new DiagramPoint(Port(selected.Middle, i, ordered.Count + extra, _incomingPort), selected.Y - _arrowLength));
            _lines.Add(new DiagramLine(new StructureDirection(source.Name, selected.Name), DiagramLineKind.Incoming, path));
        }
    }

    private bool TryNaturalLanes(List<ModuleBox> sources, ModuleBox selected, Dictionary<string, double> lanes)
    {
        var previous = double.NegativeInfinity;
        foreach(var source in sources.Where(box => box.Row < selected.Row - 1))
        {
            var lo = Math.Max(4, previous + _laneGap);
            var blocked = _rows.Skip(source.Row + 1).Take(selected.Row - source.Row - 1).SelectMany(row => row).ToList();
            var candidates = blocked.SelectMany(box => new List<double> { box.X - 10, box.Right + 10 }).Append(lo).Append(Math.Clamp(source.Middle, 4, Width - 4));
            var lane = candidates.Where(x => x >= lo && x <= Width - 4 && blocked.All(box => x <= box.X - 10 || x >= box.Right + 10))
                .OrderBy(x => Math.Abs(x - source.Middle)).DefaultIfEmpty(double.NaN).First();
            if(double.IsNaN(lane))
                return false;

            lanes.Add(source.Name, lane);
            previous = lane;
        }

        // A newcomer must start on the same side of each older lane as its own lane (or its final gap).
        foreach(var source in sources)
        {
            var end = lanes.GetValueOrDefault(source.Name, source.Middle);
            foreach(var older in sources.Where(box => box.Row < source.Row))
            {
                var olderLane = lanes[older.Name];
                if((source.Middle - olderLane) * (end - olderLane) <= 0)
                    return false;
            }
        }

        return true;
    }

    private void RouteOutgoing(List<StructureDirection> outgoing, ModuleBox selected, StructureBreach? breach, int extra)
    {
        var taken = new Dictionary<int, List<double>>();
        var trial = outgoing.Select(edge => (Edge: edge, Path: OutPath(selected, _boxes[edge.To], selected.Middle, taken, breach)))
            .OrderBy(item => item.Path[1].X).ThenBy(item => _boxes[item.Edge.To].Middle).ToList();
        for(var i = 0; i < trial.Count; i++)
        {
            var (edge, path) = trial[i];
            var start = Port(selected.Middle, i, trial.Count + extra, _outgoingPort);
            path[0] = new DiagramPoint(start, selected.Bottom);
            _lines.Add(new DiagramLine(edge, DiagramLineKind.Outgoing, path));
        }
    }

    private void RouteAcross(ModuleBox selected, List<StructureDirection> directions, int total, bool incoming)
    {
        var ordered = directions.OrderBy(edge => _boxes[incoming ? edge.From : edge.To].Middle).ToList();
        for(var i = 0; i < ordered.Count; i++)
        {
            var edge = ordered[i];
            var other = _boxes[incoming ? edge.From : edge.To];
            var y = incoming ? selected.Y : selected.Bottom;
            var lane = y + ((incoming ? -1 : 1) * (20 + (i * 12)));
            var port = Port(selected.Middle, total - ordered.Count + i, total, incoming ? _incomingPort : _outgoingPort);
            var from = incoming ? other.Middle : port;
            var to = incoming ? port : other.Middle;
            var points = new List<DiagramPoint> { new DiagramPoint(from, y), new DiagramPoint(from, lane), new DiagramPoint(to, lane),
                new DiagramPoint(to, y + (incoming ? -_arrowLength : _arrowLength)) };
            _lines.Add(new DiagramLine(edge, incoming ? DiagramLineKind.Incoming : DiagramLineKind.Outgoing, points, !incoming));
        }
    }

    private List<DiagramPoint> OutPath(ModuleBox source, ModuleBox target, double start, Dictionary<int, List<double>> taken, StructureBreach? breach)
    {
        var path = new List<DiagramPoint> { new DiagramPoint(start, source.Bottom) };
        for(var row = source.Row + 1; row < target.Row; row++)
        {
            var ideal = start + ((target.Middle - start) * (row - source.Row) / (target.Row - source.Row));
            if(!taken.TryGetValue(row, out var used))
            {
                used = new List<double>();
                taken.Add(row, used);
            }

            var x = GapAt(row, ideal, used, breach);
            used.Add(x);
            path.Add(new DiagramPoint(x, _rows[row][0].Y));
            path.Add(new DiagramPoint(x, _rows[row][0].Bottom));
        }

        path.Add(new DiagramPoint(target.Middle, target.Y - _arrowLength));
        return path;
    }

    private double GapAt(int rowIndex, double ideal, List<double> used, StructureBreach? breach)
    {
        var row = _rows[rowIndex];
        var slots = new List<double>();
        for(var i = 1; i < row.Count; i++)
        {
            var middle = (row[i - 1].Right + row[i].X) / 2;
            for(var step = 0; middle + (step * 12) <= row[i].X - 8; step++)
            {
                slots.Add(middle + (step * 12));
                if(step > 0)
                    slots.Add(middle - (step * 12));
            }
        }

        for(var i = 0; i <= used.Count; i++)
        {
            slots.Add(row[0].X - 16 - (i * 12));
            slots.Add(row[^1].Right + 16 + (i * 12));
        }

        var barrier = row.Where(box => box.Name == breach?.FromModule || box.Name == breach?.ToModule)
            .Select(box => box.Right - 8).DefaultIfEmpty(double.PositiveInfinity).Min();
        return slots.Where(x => x < barrier && used.All(other => Math.Abs(x - other) >= 10)).OrderBy(x => Math.Abs(x - ideal)).First();
    }

    private void RouteBreach(ModuleBox from, ModuleBox to)
    {
        var first = Math.Min(from.Row, to.Row);
        var last = Math.Max(from.Row, to.Row);
        var lane = _rows.Skip(first).Take(last - first + 1).Select(row => row[^1].Right).Max() + 48;
        var fromY = from.Y + (BoxHeight / 2);
        var toY = to.Y + (BoxHeight / 2);
        var path = new List<DiagramPoint> { new DiagramPoint(from.Right, fromY) };
        AddRedEnd(path, from, lane, fromY, false);
        AddRedEnd(path, to, lane, toY, true);
        path.Add(new DiagramPoint(to.Right + 10, toY));
        _lines.Add(new DiagramLine(new StructureDirection(from.Name, to.Name), DiagramLineKind.Breach, path));
    }

    private void AddRedEnd(List<DiagramPoint> path, ModuleBox box, double lane, double middle, bool arrives)
    {
        if(box == _rows[box.Row][^1])
        {
            path.Add(new DiagramPoint(lane, middle));
            return;
        }

        var x = (box.Right + _rows[box.Row].First(next => next.X > box.Right).X) / 2;
        var y = arrives ? box.Y - 14 : box.Bottom + 14;
        var points = new List<DiagramPoint> { new DiagramPoint(x, middle), new DiagramPoint(x, y), new DiagramPoint(lane, y) };
        if(arrives)
            points.Reverse();

        path.AddRange(points);
    }

    private void IncludeLines()
    {
        var left = _lines.SelectMany(line => line.Points).Select(point => point.X - 4).DefaultIfEmpty(0).Min();
        var shift = Math.Max(0, -left);
        var right = _lines.SelectMany(line => line.Points.Select(point => point.X + (line.Kind == DiagramLineKind.Breach ? 30 : 4))).DefaultIfEmpty(0).Max();
        Width = Math.Max(Width, right) + shift;
        var top = _lines.SelectMany(line => line.Points).Select(point => point.Y - 4).DefaultIfEmpty(0).Min();
        var down = Math.Max(0, -top);
        Height = Math.Max(Height, _lines.SelectMany(line => line.Points).Select(point => point.Y + 4).DefaultIfEmpty(0).Max()) + down;
        if(shift == 0 && down == 0)
            return;

        foreach(var box in _boxes.Values.ToList())
            _boxes[box.Name] = box with { X = box.X + shift, Y = box.Y + down };

        for(var i = 0; i < _lines.Count; i++)
            _lines[i] = _lines[i] with { Points = _lines[i].Points.Select(point => new DiagramPoint(point.X + shift, point.Y + down)).ToList() };
    }

    private static double Port(double middle, int index, int count, double spacing)
    {
        return middle + ((index - ((count - 1) / 2.0)) * spacing);
    }
}
