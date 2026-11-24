namespace CF.Core.Setup;

/// <summary>
/// What the person asked of the install command.
/// </summary>
public sealed class InstallOptions
{
    /// <summary>
    /// Creates the options.
    /// </summary>
    public InstallOptions(bool dryRun, bool changePath)
    {
        DryRun = dryRun;
        ChangePath = changePath;
    }

    /// <summary>
    /// True to only say what would be done.
    /// </summary>
    public bool DryRun { get; }

    /// <summary>
    /// False to leave the PATH alone.
    /// </summary>
    public bool ChangePath { get; }
}