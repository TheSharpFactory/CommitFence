namespace CF.Core.Changes;

/// <summary>
/// What a review is asked to look at.
/// </summary>
public enum ScopeKind
{
    /// <summary>
    /// The refs of a push, as git's pre-push hook names them.
    /// </summary>
    Push,

    /// <summary>
    /// What is staged in the index.
    /// </summary>
    Staged,

    /// <summary>
    /// What lies between two named revisions.
    /// </summary>
    Range,

    /// <summary>
    /// The whole of a folder, as the last commit holds it.
    /// </summary>
    WholeFolder
}