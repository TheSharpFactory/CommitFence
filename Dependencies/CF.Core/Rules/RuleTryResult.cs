namespace CF.Core.Rules;

/// <summary>
/// The result of trying a rule on code: whether it could be done and, when it could, one verdict for each artifact of
/// the rule's type.
/// </summary>
public sealed class RuleTryResult
{
    /// <summary>
    /// Creates a result.
    /// </summary>
    public RuleTryResult(TryStatus status, IReadOnlyList<ArtifactVerdict> verdicts, string reason, int? line, int? column)
    {
        Status = status;
        Verdicts = verdicts;
        Reason = reason;
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Whether the rule could be tried.
    /// </summary>
    public TryStatus Status { get; }

    /// <summary>
    /// The verdicts, in the order the artifacts start in the code; empty unless the rule was tried.
    /// </summary>
    public IReadOnlyList<ArtifactVerdict> Verdicts { get; }

    /// <summary>
    /// Why the rule could not be tried; empty when it was.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// The line where the code stops parsing, or nothing.
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// The column where the code stops parsing, or nothing.
    /// </summary>
    public int? Column { get; }

    /// <summary>
    /// How many artifacts have this outcome.
    /// </summary>
    public int Count(TryOutcome outcome)
    {
        return Verdicts.Count(verdict => verdict.Outcome == outcome);
    }
}