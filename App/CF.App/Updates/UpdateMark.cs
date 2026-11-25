namespace CF.App.Updates;

/// <summary>
/// The mark at the left of the update notice: the logo while nothing is done, the busy ring while the app works, a tick when it
/// is installed and the refused badge when it failed.
/// </summary>
internal enum UpdateMark
{
    Logo,
    Ring,
    Tick,
    Failure
}
