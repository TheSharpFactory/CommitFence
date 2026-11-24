namespace CF.Core.Findings;

/// <summary>
/// How serious a finding is.
/// </summary>
public enum Severity
{
    /// <summary>
    /// The push is refused.
    /// </summary>
    Block,

    /// <summary>
    /// The push goes through, with the finding shown.
    /// </summary>
    Warn
}