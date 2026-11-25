using System.Globalization;
using CF.Core.Ports;

namespace CF.Core.Services;

/// <summary>The structure settings and the last completed judgment of layer directions.</summary>
public sealed record StructureReport(IReadOnlyList<StructureModuleReport> Modules, StructureDirectionLists Directions,
    IReadOnlyList<StructureDirection> AllowedDirections, StructureJudgment? LastJudgment)
{
    /// <summary>The number of modules and allowed directions.</summary>
    public string CountSentence
    {
        get
        {
            var modules = $"{Modules.Count} {(Modules.Count == 1 ? "module" : "modules")}";
            return Directions is { Allow: null, Forbid: null } ? modules + ", no directions set: every use is allowed" :
                $"{modules}, {AllowedDirections.Count} allowed {(AllowedDirections.Count == 1 ? "direction" : "directions")}";
        }
    }

    /// <summary>Uses from the last judgment whose direction is still forbidden between current modules.</summary>
    public IReadOnlyList<StructureBreach> ActiveBreaches
    {
        get
        {
            if(LastJudgment == null || Directions is { Allow: null, Forbid: null })
                return [];

            var modules = Modules.Select(module => module.Name).ToHashSet(StringComparer.Ordinal);
            var allowed = AllowedDirections.ToHashSet();
            return LastJudgment.Breaches.Where(breach => breach.FromModule != breach.ToModule &&
                modules.Contains(breach.FromModule) && modules.Contains(breach.ToModule) &&
                !allowed.Contains(new StructureDirection(breach.FromModule, breach.ToModule))).ToList();
        }
    }

    /// <summary>The result of the last judgment under current directions, shared by the screen and the command.</summary>
    public string JudgmentSentence(DateTimeOffset now)
    {
        if(LastJudgment == null)
            return "Not judged yet: no review has run";

        return ActiveBreaches.Count switch
        {
            0 => "Nothing breaks them in the last " + JudgmentScope(now),
            1 => "1 use breaks a direction",
            var count => $"{count} uses break a direction"
        };
    }

    /// <summary>The kind and time of the review that last judged the directions.</summary>
    public string JudgmentScope(DateTimeOffset now)
    {
        return LastJudgment == null ? string.Empty : $"review of {LastJudgment.Folder ?? "the changes"}, {TimeOf(LastJudgment.EndedAt, now)}";
    }

    private static string TimeOf(DateTimeOffset ended, DateTimeOffset now)
    {
        var date = ended.Date == now.Date ? "today" : ended.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        return date + " " + ended.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}

/// <summary>A module with its parts and its incoming and outgoing allowed directions.</summary>
public sealed record StructureModuleReport(string Name, IReadOnlyList<StructurePart> Parts, IReadOnlyList<string> MayUse, IReadOnlyList<string> UsedBy);

/// <summary>A part of a module, with its kind as written in settings.</summary>
public sealed record StructurePart(string Kind, string Pattern)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return Kind + ":" + Pattern;
    }
}

/// <summary>The lists written in settings. An absent list is null and a present list can be empty.</summary>
public sealed record StructureDirectionLists(IReadOnlyList<StructureDirection>? Allow, IReadOnlyList<StructureDirection>? Forbid);

/// <summary>A direction from one module to another.</summary>
public sealed record StructureDirection(string From, string To)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return From + " -> " + To;
    }
}
