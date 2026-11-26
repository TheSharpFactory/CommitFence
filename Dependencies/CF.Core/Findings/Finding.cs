using CF.Core.Code;

namespace CF.Core.Findings;

/// <summary>
/// One thing the review found wrong.
/// </summary>
public sealed class Finding
{
    /// <summary>
    /// Creates a finding.
    /// </summary>
    public Finding(
        string ruleId,
        FindingSource source,
        Severity severity,
        string language,
        string? project,
        string? targetFramework,
        string? file,
        int? line,
        int? column,
        string message,
        string? why,
        ArtifactReach? reach)
    {
        RuleId = ruleId;
        Source = source;
        Severity = severity;
        Language = language;
        Project = project;
        TargetFramework = targetFramework;
        File = file;
        Line = line;
        Column = column;
        Message = message;
        Why = why;
        Reach = reach;
    }

    /// <summary>
    /// The identifier of the rule that was broken.
    /// </summary>
    public string RuleId { get; }

    /// <summary>
    /// What produced the finding.
    /// </summary>
    public FindingSource Source { get; }

    /// <summary>
    /// How serious it is.
    /// </summary>
    public Severity Severity { get; }

    /// <summary>The build's severity word when the file sets the rule, before strictness is applied.</summary>
    public string? FileSeverity { get; init; }

    /// <summary>Whether the finding reports a compiler error that prevents the code from compiling.</summary>
    public bool IsCompileError
    {
        get { return Source == FindingSource.Compiler && Severity == Severity.Block && Reach == null; }
    }

    /// <summary>
    /// The identifier of the language, such as csharp.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// The project the file was compiled in, when known.
    /// </summary>
    public string? Project { get; }

    /// <summary>
    /// The target framework of that project, when known.
    /// </summary>
    public string? TargetFramework { get; }

    /// <summary>The project file relative to the tree, from the build that produced the finding.</summary>
    public string? ProjectFile { get; init; }

    /// <summary>The framework of that build, even if the finding was merged with other builds for display.</summary>
    public string? BuildFramework { get; init; }

    /// <summary>
    /// The file, when the finding has a place.
    /// </summary>
    public string? File { get; }

    /// <summary>
    /// The line, counted from 1, when the finding has a place.
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// The column, counted from 1, when the finding has a place.
    /// </summary>
    public int? Column { get; }

    /// <summary>
    /// What is wrong, in a sentence.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Why the team has the rule, when the rule says.
    /// </summary>
    public string? Why { get; }

    /// <summary>
    /// The lines whose change makes the finding count, when they are not just the place: the artifact's own span and
    /// what its properties were read from.
    /// </summary>
    public ArtifactReach? Reach { get; }

    /// <summary>
    /// The same finding, said to hold for these projects and target frameworks only.
    /// </summary>
    public Finding HoldingFor(string? project, string? targetFramework)
    {
        return new Finding(RuleId, Source, Severity, Language, project, targetFramework, File, Line, Column, Message, Why, Reach) { FileSeverity = FileSeverity, ProjectFile = ProjectFile, BuildFramework = BuildFramework };
    }

    internal Finding WithSeverity(Severity severity)
    {
        return new Finding(RuleId, Source, severity, Language, Project, TargetFramework, File, Line, Column, Message, Why, Reach) { FileSeverity = FileSeverity, ProjectFile = ProjectFile, BuildFramework = BuildFramework };
    }
}
