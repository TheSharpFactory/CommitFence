using CF.Core.Findings;

namespace CF.Core.Rules;

/// <summary>
/// A new rule as a person gives it. Nothing is checked until the service adds it.
/// </summary>
public sealed class RuleDraft
{
    /// <summary>
    /// Creates a draft. The language may be left out when the product has only one.
    /// </summary>
    public RuleDraft(
        string id,
        string? language,
        string artifactType,
        Severity severity,
        string title,
        IReadOnlyList<ConditionDraft> where,
        IReadOnlyList<ConditionDraft> require,
        bool forbid,
        IReadOnlyList<string> paths,
        string? message,
        string? why)
    {
        Id = id;
        Language = language;
        ArtifactType = artifactType;
        Severity = severity;
        Title = title;
        Where = where;
        Require = require;
        Forbid = forbid;
        Paths = paths;
        Message = message;
        Why = why;
    }

    /// <summary>
    /// The identifier of the rule.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The language identifier, or nothing for the only language there is.
    /// </summary>
    public string? Language { get; }

    /// <summary>
    /// The artifact type the rule is for.
    /// </summary>
    public string ArtifactType { get; }

    /// <summary>
    /// Whether a break blocks the push or only warns.
    /// </summary>
    public Severity Severity { get; }

    /// <summary>
    /// What the rule says, in a line.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Which artifacts the rule looks at.
    /// </summary>
    public IReadOnlyList<ConditionDraft> Where { get; }

    /// <summary>
    /// What must hold of them.
    /// </summary>
    public IReadOnlyList<ConditionDraft> Require { get; }

    /// <summary>
    /// True when no such artifact may exist.
    /// </summary>
    public bool Forbid { get; }

    /// <summary>
    /// The files the rule applies to; all files when empty.
    /// </summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>
    /// The message of a finding, or nothing to use the title.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Why the team has the rule, or nothing.
    /// </summary>
    public string? Why { get; }

    /// <summary>
    /// Whether the new rule is on. A new rule is on; a copy of a rule that is off stays off.
    /// </summary>
    public bool Enabled { get; init; } = true;
}