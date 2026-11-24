namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// What the build's own evaluation says about one project: its units, or why it could not be evaluated.
/// </summary>
internal sealed class ProjectEvaluation
{
    private ProjectEvaluation(ProjectFile project, IReadOnlyList<ProjectUnit> units, string? failure)
    {
        Project = project;
        Units = units;
        Failure = failure;
    }

    public ProjectFile Project { get; }

    public IReadOnlyList<ProjectUnit> Units { get; }

    /// <summary>
    /// Why the project could not be evaluated, or nothing when it could.
    /// </summary>
    public string? Failure { get; }

    public static ProjectEvaluation Succeeded(ProjectFile project, IReadOnlyList<ProjectUnit> units)
    {
        return new ProjectEvaluation(project, units, null);
    }

    public static ProjectEvaluation Failed(ProjectFile project, string failure)
    {
        return new ProjectEvaluation(project, new List<ProjectUnit>(), failure);
    }
}