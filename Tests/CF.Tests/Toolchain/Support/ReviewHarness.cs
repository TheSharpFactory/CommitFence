using CF.Adapters.Machine;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Rules.Settings;
using CF.Languages.CSharp;

namespace CF.Tests.Toolchain.Support;

/// <summary>
/// A data folder inside a temporary folder.
/// </summary>
internal sealed class TestDataFolder : IDataFolder
{
    public TestDataFolder(string rootFolder)
    {
        RootFolder = rootFolder;
    }

    public string RootFolder { get; }
}

/// <summary>
/// The private copy of the review: every tree name leads to a folder the test made, and a tree the test did not name leads
/// to the folder of the tree under review.
/// </summary>
internal sealed class FolderSnapshots : ISnapshotStore
{
    private readonly string _fallback;
    private readonly Dictionary<string, string> _folders = new Dictionary<string, string>(StringComparer.Ordinal);

    public FolderSnapshots(string fallback)
    {
        _fallback = fallback;
    }

    public List<string> Moves { get; } = new List<string>();

    public void Add(string tree, string folder)
    {
        _folders[tree] = folder;
    }

    public IReadOnlyList<string> ReadChangedFiles(RepositoryLocation repository)
    {
        throw new NotSupportedException();
    }

    public string MakePatch(RepositoryLocation repository, string file, string before, string after)
    {
        throw new NotSupportedException();
    }

    public IReadOnlyList<string> ReadUntrackedFiles(RepositoryLocation repository)
    {
        throw new NotSupportedException();
    }

    public string MoveTo(RepositoryLocation repository, string tree)
    {
        Moves.Add(tree);
        return _folders.TryGetValue(tree, out var folder) ? folder : _fallback;
    }
}

/// <summary>
/// Review logs in new folders of a data folder.
/// </summary>
internal sealed class FolderLogs : IReviewLogs
{
    private readonly string _root;
    private int _count;

    public FolderLogs(string root)
    {
        _root = root;
    }

    public string CreateFolder(RepositoryLocation repository)
    {
        _count++;
        var folder = Path.Combine(_root, "logs", _count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return folder;
    }
}

/// <summary>
/// The part of git the review of a change set asks for when the test hands it the change set: the empty tree and the files
/// of a tree.
/// </summary>
internal sealed class EmptyTreeGit : IGit
{
    public const string EmptyTreeName = "4b825dc642cb";

    public Dictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public string EmptyTree(RepositoryLocation repository)
    {
        return EmptyTreeName;
    }

    public string? ReadFile(RepositoryLocation repository, string tree, string path)
    {
        return Files.TryGetValue(tree + ":" + path, out var text) ? text : null;
    }

    public RepositoryLocation? FindRepository(string folder)
    {
        throw new NotSupportedException();
    }

    public bool IsShallow(RepositoryLocation repository)
    {
        throw new NotSupportedException();
    }

    public string? ResolveCommit(RepositoryLocation repository, string revision)
    {
        throw new NotSupportedException();
    }

    public string? ResolveTree(RepositoryLocation repository, string revision)
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

    public string? CheckedOutBranch(RepositoryLocation repository)
    {
        throw new NotSupportedException();
    }

    public IReadOnlyList<string> RemoteNames(RepositoryLocation repository)
    {
        throw new NotSupportedException();
    }

    public string? RemoteUrl(RepositoryLocation repository, string remoteName)
    {
        throw new NotSupportedException();
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
        throw new NotSupportedException();
    }
}

/// <summary>
/// The review of one change set, as the product runs it after git has said what changed: the real review classes and the
/// real C# plug-in, with the process, the private copy and git handed in by the test.
/// </summary>
internal sealed class ReviewHarness
{
    private readonly ChangeSetReview _review;

    public ReviewHarness(IReadOnlyList<ILanguage> languages, string dataFolder, FolderSnapshots snapshots, EmptyTreeGit git)
    {
        var fileSystem = new SystemFileSystem();
        var settingsFile = new SettingsFile(fileSystem, new SettingsReader(languages));
        var logs = new FolderLogs(dataFolder);
        _review = new ChangeSetReview(languages, snapshots, new SettingsInTree(fileSystem, settingsFile, "1.0.0.0"), new FileReviewer(fileSystem, languages), new ToolchainStage(languages, logs), new StructureReview(git, snapshots, logs, settingsFile), git);
    }

    /// <summary>
    /// The review with the real C# plug-in, whose process is the one given.
    /// </summary>
    public static ReviewHarness ForCSharp(IProcessRunner processRunner, string dataFolder, FolderSnapshots snapshots, EmptyTreeGit git)
    {
        var language = new CSharpLanguage(processRunner, new SystemFileSystem(), new TestDataFolder(dataFolder));
        return new ReviewHarness(new List<ILanguage> { language }, dataFolder, snapshots, git);
    }

    public static ChangedFile Changed(string path, ChangeStatus status = ChangeStatus.Modified, int firstLine = 1, int lastLine = int.MaxValue)
    {
        var language = path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "csharp" : null;
        return new ChangedFile(path, language, status, new List<LineRange> { LineRange.ForLines(firstLine, lastLine) });
    }

    public ReviewResult Review(ChangeSet changeSet)
    {
        var run = new ReviewRun();
        _review.Review(new RepositoryLocation("/work", "/work/.git"), changeSet, null, run);

        return run.ToResult(TimeSpan.Zero);
    }
}

/// <summary>
/// The C# plug-in with a toolchain that throws when it runs.
/// </summary>
internal sealed class ThrowingLanguage : ILanguage
{
    private readonly ILanguage _inner;

    public ThrowingLanguage(ILanguage inner)
    {
        _inner = inner;
    }

    public string Id
    {
        get { return _inner.Id; }
    }

    public string Name
    {
        get { return _inner.Name; }
    }

    public IArtifactCatalog Catalog
    {
        get { return _inner.Catalog; }
    }

    public ICodeReader Reader
    {
        get { return _inner.Reader; }
    }

    public IToolchain? Toolchain { get; } = new ThrowingToolchain();

    public IMeaningSource? Meaning
    {
        get { return null; }
    }

    public IRepositoryRules? RepositoryRules
    {
        get { return null; }
    }

    public bool OwnsFile(string path)
    {
        return _inner.OwnsFile(path);
    }

    private sealed class ThrowingToolchain : IToolchain
    {
        public IReadOnlyList<ChangedFile> FilesOfConcern(string treeFolder, IReadOnlyList<ChangedFile> files)
        {
            return files;
        }

        public bool IsProjectFile(string path)
        {
            return path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
        }

        public ToolchainResult Run(ToolchainRequest request)
        {
            throw new InvalidOperationException("the toolchain fell over");
        }
    }
}
