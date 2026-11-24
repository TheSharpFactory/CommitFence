namespace CF.Adapters.Updates;

/// <summary>
/// The two downloads of a release: the app with the command line beside it, and the command line alone.
/// </summary>
internal enum PackageKind
{
    /// <summary>
    /// The folder holds the app, so the update is CommitFence-&lt;system&gt;.zip.
    /// </summary>
    App,

    /// <summary>
    /// The folder holds only the command line, so the update is commitfence-cli-&lt;system&gt;.zip.
    /// </summary>
    CommandLine
}