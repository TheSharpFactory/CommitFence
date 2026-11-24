using CF.Core.Findings;

namespace CF.Core.Code;

/// <summary>
/// A rule that a file of the repository itself sets for a language's tools, such as a line of an .editorconfig.
/// </summary>
public sealed class FileRule
{
    /// <summary>
    /// Creates a rule of a file.
    /// </summary>
    public FileRule(string id, string? title, string setting, FileRuleState state, Severity? severity, string file, int line, IReadOnlyList<FileLine>? lines = null)
    {
        Id = id;
        Title = title;
        Setting = setting;
        State = state;
        Severity = severity;
        File = file;
        Line = line;
        Lines = lines ?? new List<FileLine>();
    }

    /// <summary>
    /// The rule's identifier, as the file writes it.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// What the rule is about, or nothing when neither the language's tools nor the file say.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// The setting as the file writes it, without a comment.
    /// </summary>
    public string Setting { get; }

    /// <summary>
    /// Whether the build applies the rule.
    /// </summary>
    public FileRuleState State { get; }

    /// <summary>
    /// What a breach does to a push when the rule is on: block or warn. Nothing when the rule is not on.
    /// </summary>
    public Severity? Severity { get; }

    /// <summary>
    /// The file that sets the rule, as people name it.
    /// </summary>
    public string File { get; }

    /// <summary>
    /// The line of the file, counted from 1.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// The lines of the file around the one that sets the rule, that line included: up to three before and three after.
    /// Empty when the language does not hand them.
    /// </summary>
    public IReadOnlyList<FileLine> Lines { get; }
}
