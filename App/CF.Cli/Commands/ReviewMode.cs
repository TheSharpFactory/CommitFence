namespace CF.Cli.Commands;

/// <summary>
/// What the review command was asked to look at.
/// </summary>
internal enum ReviewMode
{
    /// <summary>
    /// Named files, read where they are.
    /// </summary>
    Files,

    /// <summary>
    /// The refs git gives a pre-push hook on standard input.
    /// </summary>
    Hook,

    Push,

    /// <summary>
    /// What is staged.
    /// </summary>
    Staged,

    /// <summary>
    /// Two named revisions.
    /// </summary>
    Range,

    /// <summary>
    /// The whole of a folder, a project or the repository, as the last commit holds it.
    /// </summary>
    All
}