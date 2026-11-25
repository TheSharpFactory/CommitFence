namespace CF.App.Settings;

/// <summary>
/// What stands at the left of the status row.
/// </summary>
internal enum UpdateStatusMark
{
    /// <summary>
    /// Nothing.
    /// </summary>
    None,

    /// <summary>
    /// The tick of something that is fine.
    /// </summary>
    Tick,

    /// <summary>
    /// The busy ring.
    /// </summary>
    Ring,

    /// <summary>
    /// The refused badge of a failure.
    /// </summary>
    Failure
}
