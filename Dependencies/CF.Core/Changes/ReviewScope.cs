namespace CF.Core.Changes;

/// <summary>
/// What a review is asked to look at: the refs of a push, what is staged, a range of two revisions, or the whole of a
/// folder.
/// </summary>
public sealed class ReviewScope
{
    private ReviewScope(ScopeKind kind, string remoteName, string remoteUrl, PushedRefs pushedRefs, string baseRevision, string headRevision, string targetPath)
    {
        Kind = kind;
        RemoteName = remoteName;
        RemoteUrl = remoteUrl;
        PushedRefs = pushedRefs;
        BaseRevision = baseRevision;
        HeadRevision = headRevision;
        TargetPath = targetPath;
    }

    /// <summary>
    /// Which kind of scope this is.
    /// </summary>
    public ScopeKind Kind { get; }

    /// <summary>
    /// For a push: the name of the remote, as git gives it to the hook.
    /// </summary>
    public string RemoteName { get; }

    /// <summary>
    /// For a push: the address of the remote, as git gives it to the hook.
    /// </summary>
    public string RemoteUrl { get; }

    /// <summary>
    /// For a push: the ref lines git gave the hook.
    /// </summary>
    public PushedRefs PushedRefs { get; }

    /// <summary>
    /// For a range: the revision the change starts from.
    /// </summary>
    public string BaseRevision { get; }

    /// <summary>
    /// For a range: the revision the change ends at.
    /// </summary>
    public string HeadRevision { get; }

    /// <summary>
    /// For the whole of a folder: the folder or project file to review, as a full path; empty for the whole repository.
    /// </summary>
    public string TargetPath { get; }

    /// <summary>
    /// The refs of a push, with the remote they go to.
    /// </summary>
    public static ReviewScope ForPush(string remoteName, string remoteUrl, PushedRefs pushedRefs)
    {
        return new ReviewScope(ScopeKind.Push, remoteName, remoteUrl, pushedRefs, string.Empty, string.Empty, string.Empty);
    }

    /// <summary>
    /// What is staged in the index, against HEAD.
    /// </summary>
    public static ReviewScope ForStaged()
    {
        return new ReviewScope(ScopeKind.Staged, string.Empty, string.Empty, PushedRefs.None, string.Empty, string.Empty, string.Empty);
    }

    /// <summary>
    /// The difference between two revisions.
    /// </summary>
    public static ReviewScope ForRange(string baseRevision, string headRevision)
    {
        return new ReviewScope(ScopeKind.Range, string.Empty, string.Empty, PushedRefs.None, baseRevision, headRevision, string.Empty);
    }

    /// <summary>
    /// Everything the last commit holds under a folder, or under the folder of a project file, old code included. A
    /// target that is empty means the whole repository.
    /// </summary>
    public static ReviewScope ForWholeFolder(string targetPath)
    {
        return new ReviewScope(ScopeKind.WholeFolder, string.Empty, string.Empty, PushedRefs.None, string.Empty, string.Empty, targetPath);
    }
}