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
        var rows = names.GroupBy(name => Level[name]).OrderByDescending(row => row.Key).Select(row => row.ToList()).ToList();
        if(rows.Count > 0)
            rows[0].Sort(StringComparer.Ordinal);

        var above = report.AllowedDirections.Where(edge => Level[edge.From] > Level[edge.To])
            .ToLookup(edge => edge.To, edge => edge.From, StringComparer.Ordinal);
        OrderRows(rows, above);
        Rows = rows;
        Order = rows.SelectMany(row => row).ToList();
    }

    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    public IReadOnlyList<string> Order { get; }

    public IReadOnlyDictionary<string, int> Level { get; }

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

    private static void OrderRows(List<List<string>> rows, ILookup<string, string> above)
    {
        var place = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach(var row in rows)
            SavePlaces(row, place);

        for(var pass = 0; pass < 2; pass++)
        {
            for(var i = 1; i < rows.Count; i++)
            {
                var row = rows[i].OrderBy(name => above[name].Any() ? above[name].Average(parent => place[parent]) + (place[name] * .01) : place[name]).ToList();
                rows[i] = row;
                SavePlaces(row, place);
            }
        }
    }

    private static void SavePlaces(List<string> row, Dictionary<string, double> place)
    {
        for(var i = 0; i < row.Count; i++)
            place[row[i]] = (double)i / Math.Max(1, row.Count - 1);
    }
}
