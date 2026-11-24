namespace CF.Core.Updates;

/// <summary>
/// Where an update is: downloading (with the percentage done) or replacing the files (too late to cancel).
/// </summary>
public sealed class UpdateProgress
{
    /// <summary>
    /// The part of the update that is running.
    /// </summary>
    public UpdateStage Stage { get; init; }

    /// <summary>
    /// While downloading: 0 to 100, or -1 when the download's size is not known.
    /// </summary>
    public int Percent { get; init; }
}
