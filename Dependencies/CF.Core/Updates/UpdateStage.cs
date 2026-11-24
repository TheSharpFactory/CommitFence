namespace CF.Core.Updates;

/// <summary>
/// The part of an update that is running.
/// </summary>
public enum UpdateStage
{
    /// <summary>
    /// The download is being fetched.
    /// </summary>
    Downloading,

    /// <summary>
    /// The files are being replaced; it is too late to cancel.
    /// </summary>
    Replacing
}
