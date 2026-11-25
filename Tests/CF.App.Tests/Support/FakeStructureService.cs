using CF.Core.Ports;
using CF.Core.Rules;
using CF.Core.Services;

namespace CF.App.Tests.Support;

/// <summary>A structure report and reversible edits for exercising the desktop's translation of the service.</summary>
internal sealed class FakeStructureService : IStructureService
{
    private readonly Dictionary<StructureChange, StructureReport> _before = new Dictionary<StructureChange, StructureReport>();

    public StructureReport Report { get; set; } = Build([], []);

    public List<string> Calls { get; } = new List<string>();

    public bool RefuseWrite { get; set; }

    public StructureReport Show(string folder)
    {
        return Report;
    }

    public StructureChange AddModule(string folder, string name)
    {
        return Save("add " + name, Report.Modules.Append(new StructureModuleReport(name, [], [], [])), Report.AllowedDirections);
    }

    public StructureChange RenameModule(string folder, string name, string newName)
    {
        var modules = Report.Modules.Select(module => module.Name == name ? module with { Name = newName } : module);
        var directions = Report.AllowedDirections.Select(edge => new StructureDirection(edge.From == name ? newName : edge.From, edge.To == name ? newName : edge.To));
        return Save("rename " + name, modules, directions);
    }

    public StructureChange RemoveModule(string folder, string name)
    {
        return Save("remove " + name, Report.Modules.Where(module => module.Name != name),
            Report.AllowedDirections.Where(edge => edge.From != name && edge.To != name));
    }

    public StructureChange AddPart(string folder, string name, string part)
    {
        var fields = part.Split(':', 2);
        return ChangeParts("add " + part, name, parts => parts.Append(new StructurePart(fields[0], fields[1])).ToList());
    }

    public StructureChange RemovePart(string folder, string name, string part)
    {
        return ChangeParts("remove " + part, name, parts => parts.Where(item => item.ToString() != part).ToList());
    }

    public StructureChange SetDirection(string folder, string from, string to, bool allowed)
    {
        var edge = new StructureDirection(from, to);
        var directions = allowed ? Report.AllowedDirections.Append(edge) : Report.AllowedDirections.Where(item => item != edge);
        return Save((allowed ? "allow " : "stop allowing ") + edge, Report.Modules, directions);
    }

    public void Undo(string folder, StructureChange change)
    {
        Calls.Add("undo " + change.Message);
        Report = _before[change];
    }

    private StructureChange ChangeParts(string action, string name, Func<IReadOnlyList<StructurePart>, IReadOnlyList<StructurePart>> edit)
    {
        return Save(action, Report.Modules.Select(module => module.Name == name ? module with { Parts = edit(module.Parts) } : module), Report.AllowedDirections);
    }

    private StructureChange Save(string action, IEnumerable<StructureModuleReport> modules, IEnumerable<StructureDirection> directions)
    {
        Calls.Add(action);
        if(RefuseWrite)
            throw new RuleChangeException("The settings file could not be written, and it is unchanged: Access denied.");

        var change = new StructureChange("commitfence.json", string.Empty, string.Empty, action);
        _before.Add(change, Report);
        Report = Build(modules, directions, Report.LastJudgment);
        return change;
    }

    public static StructureReport Build(IEnumerable<StructureModuleReport> modules, IEnumerable<StructureDirection> directions, StructureJudgment? judgment = null)
    {
        var pairs = directions.Distinct().ToList();
        var outgoing = pairs.ToLookup(edge => edge.From, edge => edge.To, StringComparer.Ordinal);
        var incoming = pairs.ToLookup(edge => edge.To, edge => edge.From, StringComparer.Ordinal);
        return new StructureReport(modules.Select(module => module with
        {
            MayUse = outgoing[module.Name].ToList(),
            UsedBy = incoming[module.Name].ToList()
        }).ToList(), new StructureDirectionLists(pairs, null), pairs, judgment);
    }
}
