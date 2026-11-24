namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The units of the tree and how they depend on each other.
/// </summary>
/// <remarks>
/// A unit depends on the units of the projects it references: for the same framework when the referenced project has
/// it, otherwise on all of that project's frameworks, which asks for more and never for less.
/// </remarks>
internal sealed class UnitGraph
{
    private readonly Dictionary<string, ProjectEvaluation> _byPath;
    private readonly Dictionary<string, List<ProjectUnit>> _prerequisites = new Dictionary<string, List<ProjectUnit>>();
    private readonly Dictionary<string, List<ProjectUnit>> _dependents = new Dictionary<string, List<ProjectUnit>>();

    public UnitGraph(IReadOnlyList<ProjectEvaluation> evaluations)
    {
        Evaluations = evaluations;
        _byPath = evaluations.ToDictionary(evaluation => evaluation.Project.Path, ToolchainPaths.Comparer);
        Units = evaluations.SelectMany(evaluation => evaluation.Units).ToList();
        foreach(var unit in Units)
            _dependents[unit.Key] = new List<ProjectUnit>();

        foreach(var unit in Units)
            LinkPrerequisites(unit);
    }

    public IReadOnlyList<ProjectUnit> Units { get; }

    public IReadOnlyList<ProjectEvaluation> Evaluations { get; }

    public IReadOnlyList<ProjectUnit> UnitsOf(string projectPath)
    {
        return _byPath.TryGetValue(projectPath, out var evaluation) ? evaluation.Units : new List<ProjectUnit>();
    }

    /// <summary>
    /// The names of the assemblies the projects of the tree are compiled into, taken from the compiler's output file
    /// each project says it writes.
    /// </summary>
    public IReadOnlyList<string> AssemblyNames()
    {
        return Units.Where(unit => unit.IntermediateAssembly.Length > 0).Select(unit => Path.GetFileNameWithoutExtension(unit.IntermediateAssembly)).Distinct(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<ProjectUnit> PrerequisitesOf(ProjectUnit unit)
    {
        return _prerequisites[unit.Key];
    }

    public IReadOnlyList<ProjectUnit> DependentsOf(ProjectUnit unit)
    {
        return _dependents[unit.Key];
    }

    /// <summary>
    /// The projects this unit references that are in the tree but could not be evaluated: nothing can be said about
    /// them.
    /// </summary>
    public IReadOnlyList<ProjectEvaluation> UnreadablePrerequisitesOf(ProjectUnit unit)
    {
        return unit.ReferencedProjects.Where(path => _byPath.TryGetValue(path, out var evaluation) && evaluation.Failure != null).Select(path => _byPath[path]).ToList();
    }

    /// <summary>
    /// The project files a project references, for any of its frameworks.
    /// </summary>
    public IReadOnlyList<string> ReferencedProjectsOf(string projectPath)
    {
        return UnitsOf(projectPath).SelectMany(unit => unit.ReferencedProjects).Distinct(ToolchainPaths.Comparer).ToList();
    }

    private void LinkPrerequisites(ProjectUnit unit)
    {
        var prerequisites = new List<ProjectUnit>();
        foreach(var path in unit.ReferencedProjects)
        {
            foreach(var prerequisite in PickUnits(UnitsOf(path), unit.Framework))
            {
                prerequisites.Add(prerequisite);
                _dependents[prerequisite.Key].Add(unit);
            }
        }

        _prerequisites[unit.Key] = prerequisites;
    }

    private static IReadOnlyList<ProjectUnit> PickUnits(IReadOnlyList<ProjectUnit> candidates, string framework)
    {
        var same = candidates.Where(candidate => candidate.Framework == framework).ToList();
        return same.Count > 0 ? same : candidates;
    }
}