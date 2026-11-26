using System.Security.Cryptography;
using System.Text;
using CF.Core.Ports;

namespace CF.Tests.Review.Support;

/// <summary>
/// A repository kept in memory: commits with their parents and files, the refs a test names, and the answers git would
/// give about them. It records what it was asked. A diff is written from the two trees unless the test gives its own.
/// </summary>
internal sealed class FakeGit : IGit
{
    public const string EmptyTreeId = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

    private readonly Lock _syncRoot = new Lock();
    private readonly Dictionary<string, Dictionary<string, string>> _trees = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _treeOfCommit = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _parents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _revisions = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly List<(string Before, string After)> _diffs = new List<(string Before, string After)>();
    private readonly List<IReadOnlyList<IReadOnlyList<string>>> _textRequests = new List<IReadOnlyList<IReadOnlyList<string>>>();
    private readonly List<string> _uncommittedChecks = new List<string>();

    public FakeGit(string workFolder)
    {
        Repository = new RepositoryLocation(workFolder, Path.Combine(workFolder, ".git"));
        _trees[EmptyTreeId] = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public RepositoryLocation Repository { get; }

    public bool Shallow { get; set; }

    /// <summary>
    /// How many times the last commit (HEAD) was asked for by name.
    /// </summary>
    public int HeadReads { get; private set; }

    /// <summary>
    /// What happens right after HEAD is read: a test moves HEAD here, as a commit made in the middle of a review does.
    /// </summary>
    public Action? AfterHeadRead { get; set; }

    public string StagedTree { get; set; } = EmptyTreeId;

    public List<string> RemoteTips { get; } = new List<string>();

    public List<string> LocalTips { get; } = new List<string>();

    public List<string> Remotes { get; } = new List<string> { "origin" };

    public string? Branch { get; set; } = "main";

    public BranchUpstream? UpstreamOfBranch { get; set; }

    /// <summary>
    /// Whether the working folder holds work that is not committed under the folder it is asked about.
    /// </summary>
    public Func<string, bool> HasWork { get; set; } = folder => false;

    /// <summary>
    /// Replaces the diff between two trees (the ids it is given are the ones the product passes).
    /// </summary>
    public Func<string, string, string>? DiffAnswer { get; set; }

    /// <summary>
    /// Replaces the diff of the files read as text.
    /// </summary>
    public Func<IReadOnlyList<IReadOnlyList<string>>, string>? TextDiffAnswer { get; set; }

    /// <summary>
    /// A test that must never see a diff asked for sets this.
    /// </summary>
    public bool ForbidDiffs { get; set; }

    public IReadOnlyList<(string Before, string After)> Diffs
    {
        get
        {
            lock(_syncRoot)
            {
                return _diffs.ToList();
            }
        }
    }

    public IReadOnlyList<IReadOnlyList<IReadOnlyList<string>>> TextRequests
    {
        get
        {
            lock(_syncRoot)
            {
                return _textRequests.ToList();
            }
        }
    }

    public IReadOnlyList<string> UncommittedChecks
    {
        get
        {
            lock(_syncRoot)
            {
                return _uncommittedChecks.ToList();
            }
        }
    }

    /// <summary>
    /// A stable 40-digit object id for a name a test gives.
    /// </summary>
    public static string IdOf(string name)
    {
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(name))).ToLowerInvariant();
    }

    /// <summary>
    /// The tree id of the commit made under this name.
    /// </summary>
    public static string TreeIdOf(string name)
    {
        return IdOf("tree of " + name);
    }

    /// <summary>
    /// Adds a commit with the files its tree holds; the parents are the names of earlier commits.
    /// </summary>
    public string AddCommit(string name, IReadOnlyDictionary<string, string> files, params string[] parents)
    {
        var id = IdOf(name);
        _treeOfCommit[id] = TreeIdOf(name);
        _trees[TreeIdOf(name)] = new Dictionary<string, string>(files, StringComparer.Ordinal);
        _parents[id] = parents.Select(IdOf).ToList();
        _revisions[name] = id;
        return id;
    }

    /// <summary>
    /// Makes a revision (HEAD, a tag's object, a branch) name the commit.
    /// </summary>
    public void Name(string revision, string commitName)
    {
        _revisions[revision] = IdOf(commitName);
    }

    public IReadOnlyDictionary<string, string> FilesOf(string tree)
    {
        return _trees.TryGetValue(tree, out var files) ? files : throw new GitException($"git has no tree {tree}");
    }

    public RepositoryLocation? FindRepository(string folder)
    {
        var full = Path.GetFullPath(folder);
        var relative = Path.GetRelativePath(Repository.WorkFolder, full);
        var outside = relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative);
        return outside ? null : Repository;
    }

    public bool IsShallow(RepositoryLocation repository)
    {
        return Shallow;
    }

    public string? ResolveCommit(RepositoryLocation repository, string revision)
    {
        if(revision == "HEAD")
        {
            HeadReads++;
            var head = _revisions.GetValueOrDefault(revision);
            AfterHeadRead?.Invoke();
            return head;
        }

        if(_revisions.TryGetValue(revision, out var named))
            return named;

        return _treeOfCommit.ContainsKey(revision) ? revision : null;
    }

    public string? ResolveTree(RepositoryLocation repository, string revision)
    {
        var commit = ResolveCommit(repository, revision);
        if(commit != null)
            return _treeOfCommit[commit];

        return _trees.ContainsKey(revision) ? revision : null;
    }

    public string EmptyTree(RepositoryLocation repository)
    {
        return EmptyTreeId;
    }

    public IReadOnlyList<string> RemoteTrackingTips(RepositoryLocation repository, string remoteName)
    {
        return RemoteTips;
    }

    public string? CommonAncestor(RepositoryLocation repository, IReadOnlyList<string> commits)
    {
        var common = AncestorsOf(commits[0]);
        foreach(var commit in commits.Skip(1))
            common.IntersectWith(AncestorsOf(commit));

        // the best ones are those that no other common ancestor descends from
        var best = common.Where(candidate => !common.Any(other => other != candidate && AncestorsOf(other).Contains(candidate))).Order(StringComparer.Ordinal).ToList();
        return best.Count == 0 ? null : best[0];
    }

    public bool HasCommitsOutside(RepositoryLocation repository, string tip, IReadOnlyList<string> excluded)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach(var commit in excluded)
            known.UnionWith(AncestorsOf(commit));

        return AncestorsOf(tip).Any(commit => !known.Contains(commit));
    }

    public bool HasUnpublishedLocalCommits(RepositoryLocation repository, string remoteName)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach(var commit in RemoteTips)
            known.UnionWith(AncestorsOf(commit));

        return LocalTips.Any(tip => AncestorsOf(tip).Any(commit => !known.Contains(commit)));
    }

    public string WriteStagedTree(RepositoryLocation repository)
    {
        return StagedTree;
    }

    public string? ReadFile(RepositoryLocation repository, string tree, string path)
    {
        return FilesOf(tree).TryGetValue(path, out var text) ? text : null;
    }

    public string ReadDiff(RepositoryLocation repository, string before, string after)
    {
        lock(_syncRoot)
        {
            _diffs.Add((before, after));
        }

        if(ForbidDiffs)
            throw new InvalidOperationException("a diff was asked for, and this review has nothing to compare");

        return DiffAnswer != null ? DiffAnswer(before, after) : UnifiedDiffMaker.Between(FilesOf(ResolveTree(repository, before)!), FilesOf(ResolveTree(repository, after)!));
    }

    public string ReadDiffAsText(RepositoryLocation repository, string before, string after, IReadOnlyList<IReadOnlyList<string>> namesOfEachFile)
    {
        lock(_syncRoot)
        {
            _textRequests.Add(namesOfEachFile);
        }

        return TextDiffAnswer != null ? TextDiffAnswer(namesOfEachFile) : string.Empty;
    }

    public IReadOnlyList<string> ListFiles(RepositoryLocation repository, string tree, string folder)
    {
        var prefix = folder + "/";
        return FilesOf(tree).Keys.Where(path => folder.Length == 0 || path == folder || path.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();
    }

    public string? CurrentBranch(RepositoryLocation repository)
    {
        return Branch;
    }

    public bool HasUncommittedWork(RepositoryLocation repository, string folder)
    {
        lock(_syncRoot)
        {
            _uncommittedChecks.Add(folder);
        }

        return HasWork(folder);
    }

    public string? CheckedOutBranch(RepositoryLocation repository)
    {
        return Branch;
    }

    public IReadOnlyList<string> RemoteNames(RepositoryLocation repository)
    {
        return Remotes;
    }

    public string? RemoteUrl(RepositoryLocation repository, string remoteName)
    {
        return "https://example.invalid/" + remoteName;
    }

    public string? ConfiguredPushRemote(RepositoryLocation repository, string branch)
    {
        return null;
    }

    public string? PushDefault(RepositoryLocation repository)
    {
        return null;
    }

    public BranchUpstream? Upstream(RepositoryLocation repository, string branch)
    {
        return UpstreamOfBranch;
    }

    private HashSet<string> AncestorsOf(string commit)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(commit);
        while(pending.Count > 0)
        {
            var next = pending.Pop();
            if(!found.Add(next) || !_parents.TryGetValue(next, out var parents))
                continue;

            foreach(var parent in parents)
                pending.Push(parent);
        }

        return found;
    }
}
