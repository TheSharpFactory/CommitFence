using CF.Core.Changes;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Decides what a push must compile: the units that own changed files and every unit that depends on them, directly or
/// not, plus what those need built first.
/// </summary>
/// <remarks>
/// Only the units that own changed files are always compiled again; the others may be answered from what their last
/// compile kept, when it still holds.
/// </remarks>
internal static class ChangeSelector
{
    private static readonly IReadOnlyList<string> _buildWideFiles = new List<string> { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "nuget.config" };

    /// <summary>
    /// Whether a change to the file makes the build necessary.
    /// </summary>
    /// <remarks>
    /// The files that do are a source file, a project file, a file that decides how everything below it is built, a
    /// file that a project may import, or any other file inside the folder of a project.
    /// </remarks>
    public static bool IsConcern(string fullPath, IReadOnlyList<string> projectFolders)
    {
        if(IsSourceFile(fullPath) || IsProjectFile(fullPath) || IsBuildInput(fullPath))
            return true;

        return projectFolders.Any(folder => ToolchainPaths.IsUnder(folder, fullPath));
    }

    private static bool IsSourceFile(string path)
    {
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsProjectFile(string path)
    {
        return path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A file that decides how everything below its folder is built.
    /// </summary>
    private static bool IsBuildWideFile(string path)
    {
        return _buildWideFiles.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            || Path.GetFileName(path).StartsWith("Directory.Build.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A .props or .targets file of any name: a project can import it from anywhere, and nothing but the build says
    /// which project does.
    /// </summary>
    private static bool IsImportableFile(string path)
    {
        return path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBuildInput(string path)
    {
        return IsBuildWideFile(path) || IsImportableFile(path);
    }

    // These changes seed every unit, even when nothing in its project file refers to the changed path.
    public static bool RequiresAllProjects(IReadOnlyList<ChangedFile> files)
    {
        return files.Any(file => IsImportableFile(file.Path)
            || (IsProjectFile(file.Path) && file.Status is ChangeStatus.Deleted or ChangeStatus.Renamed)
            || (file.PreviousPath != null && (IsImportableFile(file.PreviousPath) || IsProjectFile(file.PreviousPath))));
    }

    // A removed source outside all readable projects may have been linked from anywhere.
    public static bool RequiresDeletedSourceFallback(IReadOnlyList<ProjectEvaluation> evaluations, string treeFolder, IReadOnlyList<ChangedFile> files)
    {
        var projectFolders = evaluations.Where(evaluation => evaluation.Units.Count > 0)
            .Select(evaluation => ToolchainPaths.FolderOf(evaluation.Project.Path)).ToList();
        return files.Any(file => (file.Status == ChangeStatus.Deleted && IsOutsideSource(file.Path, treeFolder, projectFolders))
            || (file.PreviousPath != null && IsOutsideSource(file.PreviousPath, treeFolder, projectFolders)));
    }

    private static bool IsOutsideSource(string path, string treeFolder, IReadOnlyList<string> projectFolders)
    {
        return IsSourceFile(path) && !projectFolders.Any(folder => ToolchainPaths.IsUnder(folder, ToolchainPaths.Combine(treeFolder, path)));
    }

    /// <summary>
    /// The plan for a change: the units that own the changed files and every unit that depends on them, plus what those
    /// need built first.
    /// </summary>
    public static BuildPlan SelectForChange(UnitGraph graph, string treeFolder, IReadOnlyList<ChangedFile> changedFiles)
    {
        var notCovered = new List<string>();
        var seeds = FindSeeds(graph, treeFolder, changedFiles, notCovered);

        return CreatePlan(graph, Close(seeds.Values, graph.DependentsOf), seeds.Keys.ToHashSet(), notCovered);
    }

    /// <summary>
    /// The plan for the whole of a folder: the units that own its files, plus what those need built first. What depends
    /// on them lies outside the target and is not judged, so it is not built; and a file that any project may import
    /// selects nothing, because the projects under the target are selected by their own files.
    /// </summary>
    public static BuildPlan SelectForWholeReview(UnitGraph graph, string treeFolder, IReadOnlyList<ChangedFile> files)
    {
        var notCovered = new List<string>();
        var seeds = FindSeeds(graph, treeFolder, files.Where(file => !IsImportableFile(file.Path)).ToList(), notCovered);

        return CreatePlan(graph, seeds, new HashSet<string>(), notCovered);
    }

    private static Dictionary<string, ProjectUnit> FindSeeds(UnitGraph graph, string treeFolder, IReadOnlyList<ChangedFile> files, List<string> notCovered)
    {
        var seeds = new Dictionary<string, ProjectUnit>();
        foreach(var file in files)
        {
            AddSeeds(graph, treeFolder, file, seeds, notCovered);
            if(file.PreviousPath != null)
                AddSeeds(graph, treeFolder, AsDeletedAtItsOldPlace(file), seeds, notCovered);
        }

        return seeds;
    }

    private static BuildPlan CreatePlan(UnitGraph graph, Dictionary<string, ProjectUnit> reviewed, IReadOnlySet<string> ownerKeys, List<string> notCovered)
    {
        var needed = Close(reviewed.Values, graph.PrerequisitesOf);
        return new BuildPlan(reviewed.Keys.ToHashSet(), ownerKeys, OrderForBuild(needed.Values, graph), notCovered);
    }

    private static void AddSeeds(UnitGraph graph, string treeFolder, ChangedFile file, Dictionary<string, ProjectUnit> seeds, List<string> notCovered)
    {
        var fullPath = ToolchainPaths.Combine(treeFolder, file.Path);
        if(IsSourceFile(file.Path))
            AddSourceFile(graph, file, fullPath, seeds, notCovered);
        else if(IsProjectFile(file.Path))
            AddProjectFile(graph, file, fullPath, seeds);
        else if(IsImportableFile(file.Path))
            AddAll(graph.Units, seeds);
        else if(IsBuildWideFile(file.Path))
            AddAll(graph.Units.Where(unit => ToolchainPaths.IsUnder(ToolchainPaths.FolderOf(fullPath), unit.Project.Path)), seeds);
        else
            AddAll(graph.Units.Where(unit => ToolchainPaths.IsUnder(ToolchainPaths.FolderOf(unit.Project.Path), fullPath)), seeds);
    }

    /// <summary>
    /// A renamed file left a place as well as reaching one: for the builds, the place it left is a deleted file.
    /// </summary>
    private static ChangedFile AsDeletedAtItsOldPlace(ChangedFile renamed)
    {
        return new ChangedFile(renamed.PreviousPath!, renamed.Language, ChangeStatus.Deleted, new List<LineRange>());
    }

    private static void AddProjectFile(UnitGraph graph, ChangedFile file, string fullPath, Dictionary<string, ProjectUnit> seeds)
    {
        if(file.Status == ChangeStatus.Deleted || file.Status == ChangeStatus.Renamed)
            AddAll(graph.Units, seeds);
        else
            AddAll(graph.UnitsOf(fullPath), seeds);
    }

    private static void AddSourceFile(UnitGraph graph, ChangedFile file, string fullPath, Dictionary<string, ProjectUnit> seeds, List<string> notCovered)
    {
        if(file.Status == ChangeStatus.Deleted)
        {
            var inside = graph.Units.Where(unit => ToolchainPaths.IsUnder(ToolchainPaths.FolderOf(unit.Project.Path), fullPath)).ToList();
            AddAll(inside.Count > 0 ? inside : graph.Units, seeds);
            return;
        }

        var owners = graph.Units.Where(unit => unit.CompileFiles.Contains(fullPath)).ToList();
        if(owners.Count == 0)
            notCovered.Add(file.Path);

        AddAll(owners, seeds);
    }

    private static void AddAll(IEnumerable<ProjectUnit> units, Dictionary<string, ProjectUnit> seeds)
    {
        foreach(var unit in units)
            seeds[unit.Key] = unit;
    }

    private static Dictionary<string, ProjectUnit> Close(IEnumerable<ProjectUnit> start, Func<ProjectUnit, IReadOnlyList<ProjectUnit>> next)
    {
        var found = start.ToDictionary(unit => unit.Key);
        var pending = new Stack<ProjectUnit>(found.Values);
        while(pending.Count > 0)
        {
            foreach(var unit in next(pending.Pop()))
            {
                if(found.TryAdd(unit.Key, unit))
                    pending.Push(unit);
            }
        }

        return found;
    }

    private static IReadOnlyList<ProjectUnit> OrderForBuild(IEnumerable<ProjectUnit> units, UnitGraph graph)
    {
        var members = units.ToDictionary(unit => unit.Key);
        var ordered = new List<ProjectUnit>();
        var visited = new HashSet<string>();
        foreach(var unit in members.Values.OrderBy(candidate => candidate.Label, StringComparer.Ordinal))
            Visit(unit, members, graph, visited, ordered);

        return ordered;
    }

    /// <summary>
    /// The plan to answer for these parts, found by their keys in this tree: the units that have them, and what those
    /// need built first.
    /// </summary>
    /// <remarks>
    /// A part that this tree does not have is left out: the project is new, or gone.
    /// </remarks>
    public static BuildPlan SelectParts(UnitGraph graph, string treeFolder, IReadOnlyList<string> parts)
    {
        var wanted = parts.ToHashSet(ToolchainPaths.Comparer);
        var seeds = graph.Units.Where(unit => wanted.Contains(unit.PartKeyIn(treeFolder))).ToDictionary(unit => unit.Key);
        var needed = Close(seeds.Values, graph.PrerequisitesOf);

        return new BuildPlan(seeds.Keys.ToHashSet(), new HashSet<string>(), OrderForBuild(needed.Values, graph), new List<string>());
    }

    private static void Visit(ProjectUnit unit, Dictionary<string, ProjectUnit> members, UnitGraph graph, HashSet<string> visited, List<ProjectUnit> ordered)
    {
        if(!visited.Add(unit.Key))
            return;

        foreach(var prerequisite in graph.PrerequisitesOf(unit).Where(candidate => members.ContainsKey(candidate.Key)).OrderBy(candidate => candidate.Label, StringComparer.Ordinal))
            Visit(prerequisite, members, graph, visited, ordered);

        ordered.Add(unit);
    }
}
