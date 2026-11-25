namespace CF.Core.Code;

/// <summary>An exemption in a source file, with its one-based line and column.</summary>
/// <param name="Line">The line of the comment, directive or attribute.</param>
/// <param name="Column">The column where it begins.</param>
/// <param name="RuleId">The identifier it names, or null for a pragma that disables all warnings.</param>
/// <param name="Form">The syntax the source uses.</param>
/// <param name="Reason">The reason as written, if any.</param>
/// <param name="CommentOnlyLine">Whether an allow comment is the only text on its line.</param>
public sealed record SourceExemption(int Line, int Column, string? RuleId, ExemptionForm Form, string? Reason, bool CommentOnlyLine = false)
{
    /// <summary>The rule or group of warnings the exemption names.</summary>
    public string Target
    {
        get { return RuleId ?? "all warnings"; }
    }
}
