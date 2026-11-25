namespace CF.App.Settings;

/// <summary>
/// The buttons of the update status row.
/// </summary>
internal enum UpdateStatusButton
{
    /// <summary>
    /// Asks for the latest release.
    /// </summary>
    CheckNow,

    /// <summary>
    /// Asks for the latest release again after a check that could not finish.
    /// </summary>
    TryCheckAgain,

    /// <summary>
    /// Downloads and installs the version that waits.
    /// </summary>
    UpdateNow,

    /// <summary>
    /// Stops the download.
    /// </summary>
    Cancel,

    /// <summary>
    /// Opens the release on GitHub.
    /// </summary>
    OpenReleasePage,

    /// <summary>
    /// Starts the update again after it failed.
    /// </summary>
    TryUpdateAgain
}
