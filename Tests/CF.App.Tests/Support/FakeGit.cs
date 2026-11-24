using CF.Core.Ports;

namespace CF.App.Tests.Support;

/// <summary>
/// The few things git is asked when a push is looked for, answered from what the test sets; any other question is a mistake.
/// </summary>
internal sealed class FakeGit : IGit
{
    public RepositoryLocation? Repository { get; set; } = new RepositoryLocation("/work/repo", "/work/repo/.git");

    public string? Branch { get; set; } = "main";

    public string? HeadCommit { get; set; }

    public List<string> Remotes { get; } = new List<string>();

    public BranchUpstream? FollowedBranch { get; set; }

    /// <summary>
    /// The commit the repository holds for a remote branch, by the name of its tracking ref.
    /// </summary>
    public Dictionary<string, string> TrackingCommits { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public Dictionary<string, string> RemoteAddresses { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public RepositoryLocation? FindRepository(string folder)
    {
        return Repository;
    }

    public string? CheckedOutBranch(RepositoryLocation repository)
    {
        return Branch;
    }

    public string? ResolveCommit(RepositoryLocation repository, string revision)
    {
        if(revision == "HEAD")
            return HeadCommit;

        return TrackingCommits.GetValueOrDefault(revision);
    }

    public IReadOnlyList<string> RemoteNames(RepositoryLocation repository)
    {
        return Remotes;
    }

    public string? RemoteUrl(RepositoryLocation repository, string remoteName)
    {
        return RemoteAddresses.GetValueOrDefault(remoteName);
    }

    public BranchUpstream? Upstream(RepositoryLocation repository, string branch)
    {
        return FollowedBranch;
    }

    public bool IsShallow(RepositoryLocation repository)
    {
        throw new NotSupportedException();
    }

    public string? ResolveTree(RepositoryLocation repository, string revision)
    {
        throw new NotSupportedException();
    }

    public string EmptyTree(RepositoryLocation repository)
    {
        throw new NotSupportedException();
    }

    public IReadOnlyList<string> RemoteTrackingTips(RepositoryLocation repository, string remoteName)
    {
        throw new NotSupportedException();
    }

    public string? CommonAncestor(RepositoryLocation repository, IReadOnlyList<string> commits)
    {
        throw new NotSupportedException();
    }

    public bool HasCommitsOutside(RepositoryLocation repository, string tip, IReadOnlyList<string> excluded)
    {
        throw new NotSupportedException();
    }

    public bool HasUnpublishedLocalCommits(RepositoryLocation repository, string remoteName)
    {
        throw new NotSupportedException();
    }

    public string WriteStagedTree(RepositoryLocation repository)
    {
        throw new NotSupportedException();
    }

    public string? ReadFile(RepositoryLocation repository, string tree, string path)
    {
        throw new NotSupportedException();
    }

    public string ReadDiff(RepositoryLocation repository, string before, string after)
    {
        throw new NotSupportedException();
    }

    public string ReadDiffAsText(RepositoryLocation repository, string before, string after, IReadOnlyList<IReadOnlyList<string>> namesOfEachFile)
    {
        throw new NotSupportedException();
    }

    public IReadOnlyList<string> ListFiles(RepositoryLocation repository, string tree, string folder)
    {
        throw new NotSupportedException();
    }

    public string? CurrentBranch(RepositoryLocation repository)
    {
        throw new NotSupportedException();
    }

    public bool HasUncommittedWork(RepositoryLocation repository, string folder)
    {
        throw new NotSupportedException();
    }
}