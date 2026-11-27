using CF.Core.Ports;

namespace CF.Core.Changes;

/// <summary>
/// Decides what each pushed ref changes, as follows: the remote's own commit is the
/// tree before for a ref it has; for a new ref the tree before is where the ref left the branches the remote is known
/// to have.
/// </summary>
internal sealed class PushChangeResolver
{
    private readonly IGit _git;
    private readonly RepositoryLocation _repository;
    private readonly string _remoteName;
    private readonly ChangeSetBuilder _builder;

    public PushChangeResolver(IGit git, RepositoryLocation repository, string remoteName, ChangeSetBuilder builder)
    {
        _git = git;
        _repository = repository;
        _remoteName = remoteName;
        _builder = builder;
    }

    public IReadOnlyList<ChangeSet> Resolve(PushedRefs pushedRefs)
    {
        if(pushedRefs.Problem != null)
            throw new ScopeException(pushedRefs.Problem);

        if(_git.IsShallow(_repository))
            throw new ScopeException("the history of this repository is cut short (a shallow clone), so what the push adds cannot be established");

        if(pushedRefs.Refs.Count == 0)
            return ResolveEmptyInput();

        return pushedRefs.Refs.Select(ResolveRef).ToList();
    }

    private IReadOnlyList<ChangeSet> ResolveEmptyInput()
    {
        if(_git.HasUnpublishedLocalCommits(_repository, _remoteName))
            throw new ScopeException($"git gave the hook no ref lines, but a local branch or tag holds commits that remote '{_remoteName}' is not known to have, so what is being pushed is unknown; if the push is real, an earlier command in the hook script may have used up the input");

        var reason = "git gave no ref lines and no local branch or tag holds commits the remote is not known to have, so there is nothing to review";
        return new List<ChangeSet> { ChangeSet.Nothing("push", reason) };
    }

    private ChangeSet ResolveRef(PushedRef pushedRef)
    {
        var label = pushedRef.RemoteRef;
        if(pushedRef.IsDeletion)
            return ChangeSet.Nothing(label, "deleted on the remote, so there is nothing to review");

        var tip = _git.ResolveCommit(_repository, pushedRef.LocalObject)
            ?? throw new ScopeException($"{pushedRef.LocalRef} names {pushedRef.LocalObject}, which is not a commit this repository has");

        var known = _git.RemoteTrackingTips(_repository, _remoteName);
        var remoteCommit = FindRemoteCommit(pushedRef);
        var excluded = remoteCommit == null ? known : known.Append(remoteCommit).ToList();
        if(!_git.HasCommitsOutside(_repository, tip, excluded))
            return ChangeSet.Nothing(label, "holds no commit that the remote is not known to have, so there is nothing to review");

        var before = remoteCommit ?? FindBeforeOfNewRef(tip, known) ?? _git.EmptyTree(_repository);
        var tree = _git.ResolveTree(_repository, tip)
            ?? throw new ScopeException($"{pushedRef.LocalRef} has no tree");

        var changeSet = _builder.Build(label, before, tree).WithCommit(tip);
        if(remoteCommit != null || pushedRef.IsNewOnRemote)
            return changeSet;

        return changeSet.WithNote($"The remote's commit {pushedRef.RemoteObject[..Math.Min(10, pushedRef.RemoteObject.Length)]} is not in this clone, so {label} was reviewed as a new branch: more is reviewed, never less.");
    }

    private string? FindRemoteCommit(PushedRef pushedRef)
    {
        if(pushedRef.IsNewOnRemote)
            return null;

        return _git.ResolveCommit(_repository, pushedRef.RemoteObject);
    }

    private string? FindBeforeOfNewRef(string tip, IReadOnlyList<string> known)
    {
        var meetingPoints = new List<string>();
        foreach(var knownTip in known)
        {
            var meetingPoint = _git.CommonAncestor(_repository, new List<string> { tip, knownTip });
            if(meetingPoint != null && !meetingPoints.Contains(meetingPoint))
                meetingPoints.Add(meetingPoint);
        }

        if(meetingPoints.Count == 0)
            return null;

        return _git.CommonAncestor(_repository, meetingPoints);
    }
}