namespace CF.Core.Updates;

/// <summary>
/// What a check for a newer release found.
/// </summary>
public enum UpdateCheckStatus
{
    /// <summary>
    /// The latest release is this version or an older one.
    /// </summary>
    UpToDate,

    /// <summary>
    /// A newer release exists.
    /// </summary>
    Available,

    /// <summary>
    /// The check could not be made.
    /// </summary>
    Failed
}
