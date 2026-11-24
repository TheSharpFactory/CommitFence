namespace CF.Core.Review;

/// <summary>
/// What a program tells the hook runner when it ends.
/// </summary>
public enum ExitCode
{
    /// <summary>
    /// The review passed, or passed with warnings.
    /// </summary>
    Passed = 0,

    /// <summary>
    /// A blocking rule was broken.
    /// </summary>
    Blocked = 1,

    /// <summary>
    /// The review could not be completed.
    /// </summary>
    Incomplete = 2,

    /// <summary>
    /// Wrong settings or wrong use of the command line.
    /// </summary>
    BadSettingsOrUsage = 3
}