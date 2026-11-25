namespace CF.App.Updates;

/// <summary>
/// What the update notice says now: one of the ten states of its table, or nothing.
/// </summary>
internal enum UpdateNoticeState
{
    None,
    Available,
    CannotUpdate,
    Downloading,
    SizeNotKnown,
    Cancelling,
    Replacing,
    Installed,
    InstalledCouldNotRestart,
    Cancelled,
    Failed
}
