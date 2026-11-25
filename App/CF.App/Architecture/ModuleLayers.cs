using CF.Core.Services;

namespace CF.App.Architecture;

/// <summary>Orders modules by the longest chain below them. Mutually reachable modules share a layer.</summary>
internal sealed class ModuleLayers
{
    public ModuleLayers(StructureReport report)
    {
        var names = report.Modules.Select(module => module.Name).ToList();
        var edges = report.AllowedDirections.ToLookup(edge => edge.From, edge => edge.To, StringComparer.Ordinal);
        var reach = names.ToDictionary(name => name, name => Reach(name, edges), StringComparer.Ordinal);
        var groups = names.ToDictionary(name => name, name => reach[name].Where(other => reach[other].Contains(name)).Min(StringComparer.Ordinal)!, StringComparer.Ordinal);
        var lower = report.AllowedDirections.Select(edge => (From: groups[edge.From], To: groups[edge.To]))
            .Where(edge => edge.From != edge.To).ToLookup(edge => edge.From, edge => edge.To, StringComparer.Ordinal);
        var levels = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach(var group in groups.Values.Distinct(StringComparer.Ordinal))
            Height(group, lower, levels);

        Level = names.ToDictionary(name => name, name => levels[groups[name]], StringComparer.Ordinal);
        Cycles = names.GroupBy(name => groups[name]).Where(group => group.Count() > 1).Select(group => (IReadOnlyList<string>)group.Order(StringComparer.Ordinal).ToList())
            .OrderBy(group => group[0], StringComparer.Ordinal).ToList();
        var rows = names.GroupBy(name => Level[name]).OrderByDescending(row => row.Key)
            .Select(row => row.Order(StringComparer.Ordinal).ToList()).ToList();
        var linked = report.AllowedDirections.Where(edge => edge.From != edge.To && Level.ContainsKey(edge.From) && Level.ContainsKey(edge.To))
            .SelectMany(edge => new List<(string Name, string Other)> { (edge.From, edge.To), (edge.To, edge.From) })
            .ToLookup(pair => pair.Name, pair => pair.Other, StringComparer.Ordinal);
        OrderRows(rows, linked);
        Rows = rows;
        Order = rows.SelectMany(row => row).ToList();
    }

    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    public IReadOnlyList<string> Order { get; }

    public IReadOnlyDictionary<string, int> Level { get; }

    /// <summary>The groups of modules that may use each other, directly or through others. Each group shares a layer.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Cycles { get; }

    private static HashSet<string> Reach(string start, ILookup<string, string> edges)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while(queue.TryDequeue(out var name))
        {
            foreach(var next in edges[name])
            {
                if(seen.Add(next))
                    queue.Enqueue(next);
            }
        }

        return seen;
    }

    private static int Height(string group, ILookup<string, string> lower, Dictionary<string, int> levels)
    {
        if(!levels.TryGetValue(group, out var level))
        {
            level = lower[group].Select(next => Height(next, lower, levels) + 1).DefaultIfEmpty(0).Max();
            levels.Add(group, level);
        }

        return level;
    }

    /// <summary>
    /// Four sweeps (down, up, down, up) sort each row by the mean place of the modules each one is linked to, in either
    /// direction. A module linked to none keeps its own place, and equal keys keep their order, so the result does not
    /// depend on which module is chosen.
    /// </summary>
    private static void OrderRows(List<List<string>> rows, ILookup<string, string> linked)
    {
        var place = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach(var row in rows)
            SavePlaces(row, place);

        for(var pass = 0; pass < 4; pass++)
        {
            var sequence = pass % 2 == 0 ? Enumerable.Range(0, rows.Count) : Enumerable.Range(0, rows.Count).Reverse();
            foreach(var index in sequence)
            {
                var row = rows[index].OrderBy(name => KeyOf(name, linked, place)).ThenBy(name => place[name]).ToList();
                rows[index] = row;
                SavePlaces(row, place);
            }
        }
    }

    private static double KeyOf(string name, ILookup<string, string> linked, Dictionary<string, double> place)
    {
        var others = linked[name].Select(other => place[other]).ToList();
        return others.Count == 0 ? place[name] : others.Sum() / others.Count;
    }

    private static void SavePlaces(List<string> row, Dictionary<string, double> place)
    {
        for(var i = 0; i < row.Count; i++)
            place[row[i]] = (i + 0.5) / row.Count;
    }
}
