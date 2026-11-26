namespace CF.Core.Setup;

/// <summary>
/// What a setup command did or would do, as plain lines to show, and whether it failed.
/// </summary>
public sealed class SetupReport
{
    /// <summary>
    /// Creates the report.
    /// </summary>
    public SetupReport(IReadOnlyList<string> lines, bool failed)
    {
        Lines = lines;
        Failed = failed;
    }

    /// <summary>
    /// The lines to show, in order.
    /// </summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>
    /// True when a step could not be done.
    /// </summary>
    public bool Failed { get; }

    /// <summary>Whether this operation created the repository's settings file.</summary>
    public bool SettingsCreated { get; init; }

    /// <summary>The root of the work tree where this operation wrote a file.</summary>
    public string? WorktreeRoot { get; init; }
}
