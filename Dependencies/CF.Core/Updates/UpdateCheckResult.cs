namespace CF.Core.Updates;

/// <summary>
/// What a check for a newer release found.
/// </summary>
public sealed class UpdateCheckResult
{
    /// <summary>
    /// Whether there is a newer release, or none, or the check failed.
    /// </summary>
    public required UpdateCheckStatus Status { get; init; }

    /// <summary>
    /// The latest release (set when the check succeeded).
    /// </summary>
    public ReleaseInfo? Release { get; init; }

    /// <summary>
    /// Why the check failed, for the user.
    /// </summary>
    public string? Problem { get; init; }
}
