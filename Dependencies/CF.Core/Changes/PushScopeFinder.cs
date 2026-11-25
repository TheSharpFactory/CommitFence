using CF.Core.Ports;

namespace CF.Core.Changes;

/// <summary>
/// Finds what <c>git push</c> would send for the branch that is checked out, as git would tell the pre-push hook: the
/// branch and its commit, the branch it follows and that branch's commit. A branch that was never pushed is a new
/// branch on the remote it follows, else on <c>origin</c>, else on the first remote.
/// </summary>
public sealed class PushScopeFinder
{
    private const string _headsPrefix = "refs/heads/";
    private const string _preferredRemote = "origin";

    private readonly IGit _git;

    /// <summary>
    /// Creates the finder.
    /// </summary>
    public PushScopeFinder(IGit git)
    {
        _git = git;
    }

    /// <summary>
    /// Answers for the repository that holds the folder.
    /// </summary>
    public PushScopeAnswer Find(string folder)
    {
        try
        {
            return FindInRepository(folder);
        }
        catch(GitException exception)
        {
            return PushScopeAnswer.None("git could not be asked: " + exception.Message, string.Empty);
        }
    }

    private PushScopeAnswer FindInRepository(string folder)
    {
        var repository = _git.FindRepository(folder);
        if(repository == null)
            return PushScopeAnswer.None("The folder is not inside a git repository.", string.Empty);

        var branch = _git.CheckedOutBranch(repository);
        if(branch == null)
            return PushScopeAnswer.None("No branch is checked out (HEAD is detached), so there is no push to review.", string.Empty);

        var commit = _git.ResolveCommit(repository, "HEAD");
        if(commit == null)
            return PushScopeAnswer.None($"The branch {branch} has no commit yet, so there is nothing to push.", branch);

        var remotes = _git.RemoteNames(repository);
        if(remotes.Count == 0)
            return PushScopeAnswer.None($"This repository has no remote, so the branch {branch} has nowhere to be pushed.", branch);

        var target = ChooseTarget(repository, branch, remotes);
        var tracking = _git.ResolveCommit(repository, $"refs/remotes/{target.RemoteName}/{target.RemoteRef.Substring(_headsPrefix.Length)}");
        var remoteObject = tracking ?? new string('0', commit.Length);
        var url = _git.RemoteUrl(repository, target.RemoteName) ?? target.RemoteName;
        var pushed = PushedRefs.FromRefs(new List<PushedRef> { new PushedRef(_headsPrefix + branch, commit, target.RemoteRef, remoteObject) });

        return PushScopeAnswer.Found(ReviewScope.ForPush(target.RemoteName, url, pushed).WithSource(ReviewSource.CommandLine), branch, target.RemoteName);
    }

    private BranchUpstream ChooseTarget(RepositoryLocation repository, string branch, IReadOnlyList<string> remotes)
    {
        var upstream = _git.Upstream(repository, branch);
        if(upstream != null && remotes.Contains(upstream.RemoteName) && upstream.RemoteRef.StartsWith(_headsPrefix, StringComparison.Ordinal))
            return upstream;

        var remote = remotes.Contains(_preferredRemote) ? _preferredRemote : remotes[0];
        return new BranchUpstream(remote, _headsPrefix + branch);
    }
}
