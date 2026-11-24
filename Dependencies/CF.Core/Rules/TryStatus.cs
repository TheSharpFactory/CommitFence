namespace CF.Core.Rules;

/// <summary>
/// Whether a rule could be tried on some code.
/// </summary>
public enum TryStatus
{
    /// <summary>
    /// The rule was run on the code; the verdicts hold the outcomes, none when the code has no artifact of the rule's
    /// type.
    /// </summary>
    Tried,

    /// <summary>
    /// The rule needs what the compiler knows, so it cannot be tried on code alone.
    /// </summary>
    NeedsBuild,

    /// <summary>
    /// The code does not parse, so nothing can be said about its artifacts.
    /// </summary>
    DoesNotParse
}