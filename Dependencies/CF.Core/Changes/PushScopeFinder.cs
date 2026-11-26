using CF.Core.Ports;

namespace CF.Core.Changes;

/// <summary>
/// Finds what <c>git push</c> would send for the branch that is checked out, as git would tell the pre-push hook: the
/// branch and its commit, and the destination ref and its tracked commit on the configured push remote.
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
        return Find(folder, null);
    }

    /// <summary>Answers for the named branch, or for the current branch when no name is given.</summary>
    public PushScopeAnswer Find(string folder, string? branch)
    {
        try
        {
            return FindInRepository(folder, branch);
        }
        catch(GitException exception)
        {
            return PushScopeAnswer.None("git could not be asked: " + exception.Message, string.Empty);
        }
    }

    private PushScopeAnswer FindInRepository(string folder, string? wantedBranch)
    {
        var repository = _git.FindRepository(folder);
        if(repository == null)
            return PushScopeAnswer.None("The folder is not inside a git repository.", string.Empty);

        var branch = wantedBranch ?? _git.CheckedOutBranch(repository);
        if(branch == null)
            return PushScopeAnswer.None("No branch is checked out (HEAD is detached), so there is no push to review.", string.Empty);

        if(branch.StartsWith(_headsPrefix, StringComparison.Ordinal))
            branch = branch.Substring(_headsPrefix.Length);

        var revision = wantedBranch == null ? "HEAD" : _headsPrefix + branch;
        var commit = _git.ResolveCommit(repository, revision);
        if(commit == null && wantedBranch != null)
            return PushScopeAnswer.None($"'{wantedBranch}' is not a local branch with a commit. Choose a branch with --head, or use --base and --head for a range.", branch);

        if(commit == null)
            return PushScopeAnswer.None($"The branch {branch} has no commit yet, so there is nothing to push.", branch);

        var remotes = _git.RemoteNames(repository);
        if(remotes.Count == 0)
            return PushScopeAnswer.None($"This repository has no remote, so the branch {branch} has nowhere to be pushed.", branch);

        var remote = _git.ConfiguredPushRemote(repository, branch);
        if(remote != null && remote != "." && !remotes.Contains(remote))
            return PushScopeAnswer.None($"The configured push remote '{remote}' for {branch} is not a remote of this repository.", branch);

        var target = ChooseTarget(repository, branch, remotes, remote, out var problem);
        if(target == null)
            return PushScopeAnswer.None(problem, branch);

        var tracking = _git.ResolveCommit(repository, $"refs/remotes/{target.RemoteName}/{target.RemoteRef.Substring(_headsPrefix.Length)}");
        var remoteObject = tracking ?? new string('0', commit.Length);
        var url = _git.RemoteUrl(repository, target.RemoteName) ?? target.RemoteName;
        var pushed = PushedRefs.FromRefs(new List<PushedRef> { new PushedRef(_headsPrefix + branch, commit, target.RemoteRef, remoteObject) });

        return PushScopeAnswer.Found(ReviewScope.ForPush(target.RemoteName, url, pushed).WithSource(ReviewSource.CommandLine), branch, target.RemoteName);
    }

    /// <summary>
    /// The branch on the remote that git would push to: the remote is chosen first, then <c>push.default</c> decides
    /// the branch. Nothing, with the reason, when git itself would refuse the push.
    /// </summary>
    private BranchUpstream? ChooseTarget(RepositoryLocation repository, string branch, IReadOnlyList<string> remotes, string? configuredRemote, out string problem)
    {
        problem = string.Empty;
        var upstream = _git.Upstream(repository, branch);
        var remote = configuredRemote == "." ? null : configuredRemote;
        remote ??= upstream != null && remotes.Contains(upstream.RemoteName) ? upstream.RemoteName
            : remotes.Contains(_preferredRemote) ? _preferredRemote : remotes[0];
        var followed = upstream != null && upstream.RemoteName == remote && upstream.RemoteRef.StartsWith(_headsPrefix, StringComparison.Ordinal) ? upstream : null;
        var sameName = new BranchUpstream(remote, _headsPrefix + branch);

        var pushDefault = (_git.PushDefault(repository) ?? "simple").ToLowerInvariant();
        if(pushDefault is "upstream" or "tracking")
        {
            if(followed == null)
                problem = $"push.default is {pushDefault}, but {branch} does not follow a branch of {remote}, so git would not push it.";

            return followed;
        }

        if(pushDefault != "current" && followed != null && followed.RemoteRef != sameName.RemoteRef)
        {
            problem = $"Git would refuse to push {branch}: it follows {followed.RemoteRef.Substring(_headsPrefix.Length)} on {remote} and push.default is {pushDefault}, which needs the same name. Set push.default to upstream or current, or rename the branch.";
            return null;
        }

        return sameName;
    }
}
