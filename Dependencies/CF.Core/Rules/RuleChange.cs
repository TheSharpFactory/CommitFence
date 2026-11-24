namespace CF.Core.Rules;

/// <summary>
/// What to change in an existing rule. A part that is nothing stays as it is. An empty list, or an empty text for the
/// message and the reason, removes that part of the rule.
/// </summary>
public sealed class RuleChange
{
    /// <summary>
    /// Creates a change. Giving a require list without forbid makes the rule a require rule; forbid true removes the
    /// require list.
    /// </summary>
    public RuleChange(
        string? title,
        string? artifactType,
        IReadOnlyList<ConditionDraft>? where,
        IReadOnlyList<ConditionDraft>? require,
        bool? forbid,
        IReadOnlyList<string>? paths,
        string? message,
        string? why)
    {
        Title = title;
        ArtifactType = artifactType;
        Where = where;
        Require = require;
        Forbid = forbid;
        Paths = paths;
        Message = message;
        Why = why;
    }

    /// <summary>
    /// The new title, or nothing.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// The new artifact type, or nothing.
    /// </summary>
    public string? ArtifactType { get; }

    /// <summary>
    /// The new where conditions, replacing the old ones, or nothing.
    /// </summary>
    public IReadOnlyList<ConditionDraft>? Where { get; }

    /// <summary>
    /// The new require conditions, replacing the old ones, or nothing.
    /// </summary>
    public IReadOnlyList<ConditionDraft>? Require { get; }

    /// <summary>
    /// True to forbid, false to require, nothing to keep what the rule does.
    /// </summary>
    public bool? Forbid { get; }

    /// <summary>
    /// The new paths, replacing the old ones, or nothing.
    /// </summary>
    public IReadOnlyList<string>? Paths { get; }

    /// <summary>
    /// The new message, an empty text to remove it, or nothing.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// The new reason, an empty text to remove it, or nothing.
    /// </summary>
    public string? Why { get; }
}