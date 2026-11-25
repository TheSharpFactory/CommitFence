namespace CF.App.Settings;

/// <summary>
/// What the update status row of Settings says: the states of the notice's table, and the ones only Settings has.
/// </summary>
internal enum UpdateStatusKind
{
    /// <summary>
    /// Up to date, with when the last check worked.
    /// </summary>
    UpToDate,

    /// <summary>
    /// No check has worked on this machine yet.
    /// </summary>
    NeverChecked,

    /// <summary>
    /// A check worked on an earlier run; none has answered in this one yet, so the row claims nothing.
    /// </summary>
    LastChecked,

    /// <summary>
    /// A check is running.
    /// </summary>
    Checking,

    /// <summary>
    /// A check the person asked for could not finish.
    /// </summary>
    CouldNotCheck,

    /// <summary>
    /// A newer version waits.
    /// </summary>
    Available,

    /// <summary>
    /// A newer version waits, but this copy cannot install it.
    /// </summary>
    CannotUpdate,

    /// <summary>
    /// A download runs and its size is known.
    /// </summary>
    Downloading,

    /// <summary>
    /// A download runs and its size is not known.
    /// </summary>
    SizeNotKnown,

    /// <summary>
    /// The download is being stopped.
    /// </summary>
    Cancelling,

    /// <summary>
    /// The files are being replaced.
    /// </summary>
    Replacing,

    /// <summary>
    /// Installed, and the app restarts.
    /// </summary>
    Installed,

    /// <summary>
    /// Installed, and the app could not restart.
    /// </summary>
    InstalledCouldNotRestart,

    /// <summary>
    /// The update was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The update failed.
    /// </summary>
    Failed
}
