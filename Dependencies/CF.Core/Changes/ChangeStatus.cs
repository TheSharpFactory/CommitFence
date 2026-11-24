namespace CF.Core.Changes;

/// <summary>
/// What happened to a file in a change.
/// </summary>
public enum ChangeStatus
{
    /// <summary>
    /// The file is new.
    /// </summary>
    Added,

    /// <summary>
    /// The file exists before and after, with other content.
    /// </summary>
    Modified,

    /// <summary>
    /// The file is gone.
    /// </summary>
    Deleted,

    /// <summary>
    /// The file moved to another path.
    /// </summary>
    Renamed
}