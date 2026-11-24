namespace CF.Core.Setup;

/// <summary>
/// Judges the lowest version a repository accepts (the requires key of commitfence.json)
/// against the version of the running program.
/// </summary>
public static class VersionRequirement
{
    /// <summary>
    /// Whether the text can be written as the requires value: one to four whole numbers separated by dots.
    /// </summary>
    public static bool IsValid(string requires)
    {
        return ProductVersion.TryParse(requires) != null;
    }

    /// <summary>
    /// The plain message to show when the running program is older than the repository requires, or nothing when it is
    /// not older.
    /// </summary>
    /// <remarks>
    /// A program whose own version cannot be read is taken as older, since nothing can be shown about it.
    /// </remarks>
    public static string? Refusal(string? requires, string currentVersion)
    {
        if(requires == null)
            return null;

        var lowest = ProductVersion.TryParse(requires);
        if(lowest == null)
            return $"this repository requires a version that cannot be read: '{requires}'";

        var current = ProductVersion.TryParseOwn(currentVersion);
        if(current != null && current.CompareTo(lowest) >= 0)
            return null;

        return $"this repository requires CommitFence {lowest} or newer, and this program is {currentVersion}: update CommitFence, then try again";
    }
}