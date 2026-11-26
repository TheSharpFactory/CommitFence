namespace CF.App.Review;

/// <summary>
/// What the verdict place of the Review screen says: the verdicts a push can get, and the
/// states of the screen that have no badge.
/// </summary>
internal enum VerdictKind
{
    /// <summary>
    /// Nothing reviewed yet.
    /// </summary>
    NoReview,

    /// <summary>
    /// The folder is not inside a repository.
    /// </summary>
    NoRepository,

    /// <summary>
    /// The repository has no push to review.
    /// </summary>
    NoPush,

    /// <summary>
    /// A review is running.
    /// </summary>
    Running,

    /// <summary>
    /// Files are staged and not reviewed yet.
    /// </summary>
    StagedReady,

    /// <summary>
    /// Nothing is staged, so there is nothing to review.
    /// </summary>
    NothingStaged,

    /// <summary>
    /// The index could not be read, so nothing can be said of the staged changes.
    /// </summary>
    StagedUnreadable,

    /// <summary>
    /// The staged changes were reviewed and have findings. They count; they refuse nothing.
    /// </summary>
    StagedFindings,

    /// <summary>
    /// The staged changes were reviewed and have no findings.
    /// </summary>
    StagedClean,

    /// <summary>
    /// A folder was reviewed and has findings. They count; they refuse nothing.
    /// </summary>
    FolderFindings,

    /// <summary>
    /// A folder was reviewed and has no findings.
    /// </summary>
    FolderClean,

    /// <summary>
    /// A blocking finding refuses the push.
    /// </summary>
    Refused,

    /// <summary>
    /// The push is allowed, with warnings.
    /// </summary>
    AllowedWithWarnings,

    /// <summary>
    /// The push is allowed and nothing was found.
    /// </summary>
    AllowedClean,

    /// <summary>
    /// The review could not finish.
    /// </summary>
    Incomplete
}
