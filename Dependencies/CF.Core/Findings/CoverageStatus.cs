namespace CF.Core.Findings;

/// <summary>
/// What became of one part of the review.
/// </summary>
public enum CoverageStatus
{
    /// <summary>
    /// The part was reviewed.
    /// </summary>
    Reviewed,

    /// <summary>
    /// The part could not be reviewed.
    /// </summary>
    Failed,

    /// <summary>
    /// The part was left out on purpose.
    /// </summary>
    Skipped
}