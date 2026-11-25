using CF.Core.Code;

namespace CF.Core.Services;

/// <summary>One line of a file of the last commit that sets a rule aside, with the lines around it.</summary>
/// <param name="File">The path of the file in the repository, with forward slashes.</param>
/// <param name="Line">The line of the comment, directive or attribute, from 1.</param>
/// <param name="RuleId">The rule it names; "all warnings" for a pragma that names none.</param>
/// <param name="Form">The syntax the source uses.</param>
/// <param name="Reason">The reason as written, or null when there is none.</param>
/// <param name="IsFinding">Whether the exemption is a finding of its own: an allow comment with no reason.</param>
/// <param name="Lines">
/// Up to three lines before the exemption's line, that line, and up to three after, from the same commit; empty when the scan
/// was made without them.
/// </param>
public sealed record ExemptionEntry(string File, int Line, string RuleId, ExemptionForm Form, string? Reason, bool IsFinding, IReadOnlyList<FileLine> Lines);
