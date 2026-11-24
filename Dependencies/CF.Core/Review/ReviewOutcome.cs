namespace CF.Core.Review;

/// <summary>
/// How a review ended.
/// </summary>
public enum ReviewOutcome
{
    /// <summary>
    /// Nothing was found.
    /// </summary>
    Clean,

    /// <summary>
    /// Only warnings were found.
    /// </summary>
    Warned,

    /// <summary>
    /// A blocking rule was broken.
    /// </summary>
    Blocked,

    /// <summary>
    /// The review could not be completed.
    /// </summary>
    Incomplete
}