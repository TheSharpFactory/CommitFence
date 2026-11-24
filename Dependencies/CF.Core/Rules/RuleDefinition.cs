using CF.Core.Findings;

namespace CF.Core.Rules;

/// <summary>
/// A code rule, as a team writes it: for an artifact type, where some conditions hold, require that others hold or
/// forbid it.
/// </summary>
public sealed class RuleDefinition
{
    /// <summary>
    /// Creates a rule. Forbid and a non-empty require are never both given.
    /// </summary>
    public RuleDefinition(
        string id,
        string language,
        string artifactType,
        Severity severity,
        string title,
        IReadOnlyList<RuleCondition> where,
        IReadOnlyList<RuleCondition> require,
        bool forbid,
        IReadOnlyList<string> paths,
        string? message,
        string? why,
        bool enabled)
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
        Enabled = enabled;
    }

    /// <summary>
    /// The team's own identifier of the rule, such as TEAM010.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The identifier of the language the rule is for, such as csharp.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// The artifact type the rule is attached to (the for key), such as method.
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
    /// Which artifacts the rule looks at; all of them when empty. Every condition must hold.
    /// </summary>
    public IReadOnlyList<RuleCondition> Where { get; }

    /// <summary>
    /// What must hold of those artifacts. Every condition must hold.
    /// </summary>
    public IReadOnlyList<RuleCondition> Require { get; }

    /// <summary>
    /// True when no such artifact may exist at all.
    /// </summary>
    public bool Forbid { get; }

    /// <summary>
    /// The files the rule applies to, as patterns with * and ** and ?; all files when empty.
    /// </summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>
    /// The message of a finding, when the team wants other words than the title.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Why the team has the rule.
    /// </summary>
    public string? Why { get; }

    /// <summary>
    /// Whether the rule is evaluated in a review. A rule that is off stays in the settings file and is shown, but is
    /// never run.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// The same rule with another severity.
    /// </summary>
    public RuleDefinition WithSeverity(Severity severity)
    {
        return new RuleDefinition(Id, Language, ArtifactType, severity, Title, Where, Require, Forbid, Paths, Message, Why, Enabled);
    }

    /// <summary>
    /// The same rule, switched on or off.
    /// </summary>
    public RuleDefinition WithEnabled(bool enabled)
    {
        return new RuleDefinition(Id, Language, ArtifactType, Severity, Title, Where, Require, Forbid, Paths, Message, Why, enabled);
    }
}