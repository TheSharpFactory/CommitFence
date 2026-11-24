namespace CF.Core.Ports;

/// <summary>
/// What the product asks of git, always about git's own objects and refs: never the working folder, and nothing is
/// written to the developer's repository except objects (the staged tree).
/// </summary>
/// <remarks>
/// Every method throws <see cref="GitException" /> when git cannot be run or fails unexpectedly.
/// </remarks>
public interface IGit
{
    /// <summary>
    /// The repository the folder belongs to, or nothing when it is not inside one.
    /// </summary>
    RepositoryLocation? FindRepository(string folder);

    /// <summary>
    /// Whether the history is cut short (a shallow clone), so that common ancestors cannot be trusted.
    /// </summary>
    bool IsShallow(RepositoryLocation repository);

    /// <summary>
    /// The commit the revision names, a tag being followed to its commit; nothing when it names no commit the
    /// repository has.
    /// </summary>
    string? ResolveCommit(RepositoryLocation repository, string revision);

    /// <summary>
    /// The tree the revision names, a commit or tag being followed to its tree; nothing when it names none.
    /// </summary>
    string? ResolveTree(RepositoryLocation repository, string revision);

    /// <summary>
    /// The tree that has no files.
    /// </summary>
    string EmptyTree(RepositoryLocation repository);

    /// <summary>
    /// The commits the repository has recorded for the branches of this remote, as of the last time it talked to it.
    /// </summary>
    IReadOnlyList<string> RemoteTrackingTips(RepositoryLocation repository, string remoteName);

    /// <summary>
    /// The best common ancestor of all the given commits, or nothing when they have none.
    /// </summary>
    string? CommonAncestor(RepositoryLocation repository, IReadOnlyList<string> commits);

    /// <summary>
    /// Whether the tip reaches a commit that none of the excluded commits reach.
    /// </summary>
    bool HasCommitsOutside(RepositoryLocation repository, string tip, IReadOnlyList<string> excluded);

    /// <summary>
    /// Whether any local branch or tag holds a commit that the branches recorded for this remote do not hold.
    /// </summary>
    bool HasUnpublishedLocalCommits(RepositoryLocation repository, string remoteName);

    /// <summary>
    /// The tree of what is staged in the index. Throws when the index holds an unfinished merge.
    /// </summary>
    string WriteStagedTree(RepositoryLocation repository);

    /// <summary>
    /// The text of the file at this path, written with forward slashes, in the tree; nothing when the tree has no such
    /// file.
    /// </summary>
    string? ReadFile(RepositoryLocation repository, string tree, string path);

    /// <summary>
    /// The unified diff, with no context lines and with renames detected, from one tree or commit to another.
    /// </summary>
    string ReadDiff(RepositoryLocation repository, string before, string after);

    /// <summary>
    /// The diff of exactly these files, read as text even when git would take a file for binary. Each file is given by
    /// its names (the name it has, and the name it left when it was renamed), written with forward slashes, relative to
    /// the top of the repository; git is given them literally. There may be any number of files.
    /// </summary>
    string ReadDiffAsText(RepositoryLocation repository, string before, string after, IReadOnlyList<IReadOnlyList<string>> namesOfEachFile);

    /// <summary>
    /// The paths of the files (not links to other repositories) the tree holds under the folder, in git's order and
    /// written with forward slashes. The folder is relative to the top of the repository and given to git literally;
    /// empty means the whole tree.
    /// </summary>
    IReadOnlyList<string> ListFiles(RepositoryLocation repository, string tree, string folder);

    /// <summary>
    /// The name of the branch the working folder is on, or the short id of the commit when HEAD is detached; nothing when
    /// the repository cannot say.
    /// </summary>
    string? CurrentBranch(RepositoryLocation repository);

    /// <summary>
    /// Whether the working folder holds anything not committed under the folder (changed, staged or untracked; empty
    /// means the whole repository). It reads the status without taking a lock or touching the index.
    /// </summary>
    bool HasUncommittedWork(RepositoryLocation repository, string folder);

    /// <summary>
    /// The branch that is checked out, or <c>null</c> when HEAD is detached or the repository has no branch.
    /// </summary>
    string? CheckedOutBranch(RepositoryLocation repository);

    /// <summary>
    /// The names of the repository's remotes, in the order git lists them.
    /// </summary>
    IReadOnlyList<string> RemoteNames(RepositoryLocation repository);

    /// <summary>
    /// The address a push to the remote goes to, or <c>null</c> when the remote has none.
    /// </summary>
    string? RemoteUrl(RepositoryLocation repository, string remoteName);

    /// <summary>
    /// The remote branch the local branch follows, or <c>null</c> when it follows none.
    /// </summary>
    BranchUpstream? Upstream(RepositoryLocation repository, string branch);
}
