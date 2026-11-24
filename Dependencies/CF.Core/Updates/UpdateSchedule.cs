namespace CF.Core.Updates;

/// <summary>
/// When to check for a newer release.
/// </summary>
public static class UpdateSchedule
{
    /// <summary>
    /// The time between two checks.
    /// </summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// A check is due once a day: never checked, checked a day ago or more, or the clock went back since.
    /// </summary>
    public static bool IsCheckDue(DateTime? lastCheckUtc, DateTime nowUtc)
    {
        if(lastCheckUtc == null)
            return true;

        var since = nowUtc - lastCheckUtc.Value;
        return since >= CheckInterval || since < TimeSpan.Zero;
    }
}
