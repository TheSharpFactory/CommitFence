using CF.Core.Code;

namespace CF.Core.Rules;

/// <summary>
/// The outcome of a rule for one artifact of its type: where the artifact is, what the rule says, and why.
/// </summary>
public sealed class ArtifactVerdict
{
    /// <summary>
    /// Creates a verdict.
    /// </summary>
    public ArtifactVerdict(TryOutcome outcome, SourceSpan span, string reason)
    {
        Outcome = outcome;
        Span = span;
        Reason = reason;
    }

    /// <summary>
    /// Breaks it, follows it, or not covered.
    /// </summary>
    public TryOutcome Outcome { get; }

    /// <summary>
    /// Where the artifact is in the code.
    /// </summary>
    public SourceSpan Span { get; }

    /// <summary>
    /// The reason, in a sentence a person can check against the code.
    /// </summary>
    public string Reason { get; }
}