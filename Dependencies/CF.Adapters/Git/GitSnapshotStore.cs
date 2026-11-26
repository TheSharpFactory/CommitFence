using CF.Adapters.Storage;
using CF.Core.Ports;

namespace CF.Adapters.Git;

/// <summary>
/// The private copy: a folder and an index of its own in the data folder, moved from tree to tree by git (read-tree).
/// </summary>
/// <remarks>
/// It is moved so that files gone in the new tree disappear, unchanged files keep their time stamps and untracked files
/// such as bin and obj stay. The developer's working folder, index, HEAD and stash are never touched.
/// </remarks>
public sealed class GitSnapshotStore : ISnapshotStore, ISnapshotInspector
{
    private readonly GitRunner _git;
    private readonly IDataFolder _dataFolder;

    /// <summary>
    /// Creates the store.
    /// </summary>
    public GitSnapshotStore(IProcessRunner processRunner, IDataFolder dataFolder)
    {
        _git = new GitRunner(processRunner);
        _dataFolder = dataFolder;
    }

    /// <inheritdoc />
    public string MoveTo(RepositoryLocation repository, string tree)
    {
        try
        {
            return Move(repository, tree);
        }
        catch(IOException exception)
        {
            throw new GitException("the private copy cannot be written: " + exception.Message);
        }
        catch(UnauthorizedAccessException exception)
        {
            throw new GitException("the private copy cannot be written: " + exception.Message);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ReadChangedFiles(RepositoryLocation repository)
    {
        var files = _git.RunChecked(repository, WithSettings(["diff-files", "--no-ext-diff", "--name-only", "-z"]), SnapshotEnvironment(repository), null);
        return files.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <inheritdoc />
    public string MakePatch(RepositoryLocation repository, string file, string before, string after)
    {
        var folder = Path.Combine(RepositoryDataFolder.For(_dataFolder.RootFolder, repository), "diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            WritePatchInput(folder, "a", file, before);
            WritePatchInput(folder, "b", file, after);
            var arguments = new List<string>
            {
                "diff", "--no-index", "--no-color", "--no-ext-diff", "--no-textconv", "--no-prefix",
                "--unified=3", "--text", "--", "a/" + file, "b/" + file
            };
            var result = _git.RunOutsideRepository(folder, arguments);
            if(result.ExitCode != 1 || result.Output.Length == 0)
                throw new GitException(GitRunner.Describe(arguments, result));

            return result.Output;
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private static void WritePatchInput(string folder, string side, string file, string text)
    {
        var path = Path.Combine(folder, side, file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ReadUntrackedFiles(RepositoryLocation repository)
    {
        var listed = _git.RunChecked(repository, WithSettings(["ls-files", "--others", "-z"]), SnapshotEnvironment(repository), null);
        return listed.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private Dictionary<string, string?> SnapshotEnvironment(RepositoryLocation repository)
    {
        var folder = RepositoryDataFolder.For(_dataFolder.RootFolder, repository);
        return CreateEnvironment(Path.Combine(folder, "tree"), Path.Combine(folder, "index"));
    }

    /// <inheritdoc />
    public PrivateCopyStatus Inspect(RepositoryLocation repository)
    {
        var folder = RepositoryDataFolder.For(_dataFolder.RootFolder, repository);
        var lockPath = Path.Combine(folder, "lock");
        if(!File.Exists(lockPath))
            return InspectFiles(repository, folder);

        FileStream held;
        try
        {
            held = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        catch(FileNotFoundException)
        {
            return InspectFiles(repository, folder);
        }
        catch(IOException)
        {
            return new PrivateCopyStatus(PrivateCopyState.InUse, folder);
        }

        using(held)
        {
            return InspectFiles(repository, folder);
        }
    }

    private PrivateCopyStatus InspectFiles(RepositoryLocation repository, string folder)
    {
        var tree = Path.Combine(folder, "tree");
        var index = Path.Combine(folder, "index");
        var hasTree = Directory.Exists(tree);
        var hasIndex = File.Exists(index);
        if(File.Exists(index + ".lock"))
            return new PrivateCopyStatus(PrivateCopyState.IndexLocked, folder);

        if(hasTree != hasIndex)
            return new PrivateCopyStatus(PrivateCopyState.OutOfOrder, folder);

        if(!hasIndex)
            return new PrivateCopyStatus(PrivateCopyState.Absent, folder);

        var environment = CreateEnvironment(tree, index);
        var differences = _git.Run(repository, WithSettings(new List<string> { "diff-files", "--no-ext-diff", "--name-only", "-z" }), environment, null);
        if(differences.ExitCode != 0 || differences.Output.Length != 0)
            return new PrivateCopyStatus(PrivateCopyState.OutOfOrder, folder);

        return new PrivateCopyStatus(PrivateCopyState.InOrder, folder, File.GetLastWriteTime(index));
    }

    private string Move(RepositoryLocation repository, string tree)
    {
        var folder = RepositoryDataFolder.For(_dataFolder.RootFolder, repository);
        var treeFolder = Path.Combine(folder, "tree");
        var indexPath = Path.Combine(folder, "index");
        var environment = CreateEnvironment(treeFolder, indexPath);

        PrepareFolders(treeFolder, indexPath);
        var previous = File.Exists(indexPath) ? FindCurrentTree(repository, environment) : null;
        if(previous != tree)
            MoveIndexAndFiles(repository, environment, previous, tree, treeFolder, indexPath);

        RestoreChangedTrackedFiles(repository, environment);

        return treeFolder;
    }

    private void MoveIndexAndFiles(RepositoryLocation repository, IReadOnlyDictionary<string, string?> environment, string? previous, string tree, string treeFolder, string indexPath)
    {
        var lockPath = indexPath + ".lock";
        var lockWasPresent = File.Exists(lockPath);
        try
        {
            if(previous != null && TryMove(repository, environment, new List<string> { "read-tree", "-u", "-m", previous, tree }))
                return;

            StartOver(treeFolder, indexPath);
            _git.RunChecked(repository, WithSettings(new List<string> { "read-tree", "-u", "--reset", tree }), environment, null);
        }
        catch(GitException exception) when(exception.TimedOut && !lockWasPresent)
        {
            DiscardInterruptedCheckout(treeFolder, indexPath, lockPath);
            throw;
        }
    }

    private static void DiscardInterruptedCheckout(string treeFolder, string indexPath, string lockPath)
    {
        try
        {
            // A stopped read-tree can leave both the private index lock and files that the old index does not track.
            StartOver(treeFolder, indexPath);
            File.Delete(lockPath);
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            // Keep the checkout timeout as the reason this ref could not be reviewed.
        }
    }

    /// <summary>
    /// A build or a tool may have changed or removed a tracked file of the private copy.
    /// </summary>
    /// <remarks>
    /// Git compares the files with its index (diff-files, which only looks at the files' size and time unless they
    /// differ) and writes back exactly those; untracked files are not looked at.
    /// </remarks>
    private void RestoreChangedTrackedFiles(RepositoryLocation repository, IReadOnlyDictionary<string, string?> environment)
    {
        var changed = _git.RunChecked(repository, WithSettings(new List<string> { "diff-files", "--name-only", "-z" }), environment, null);
        if(changed.Length == 0)
            return;

        _git.RunChecked(repository, WithSettings(new List<string> { "checkout-index", "--force", "--index", "-z", "--stdin" }), environment, changed);
    }

    private static Dictionary<string, string?> CreateEnvironment(string treeFolder, string indexPath)
    {
        return new Dictionary<string, string?>
        {
            ["GIT_INDEX_FILE"] = indexPath,
            ["GIT_WORK_TREE"] = treeFolder
        };
    }

    private static void PrepareFolders(string treeFolder, string indexPath)
    {
        if(Directory.Exists(treeFolder))
            return;

        Directory.CreateDirectory(treeFolder);
        File.Delete(indexPath);
    }

    private static void StartOver(string treeFolder, string indexPath)
    {
        File.Delete(indexPath);
        Directory.Delete(treeFolder, true);
        Directory.CreateDirectory(treeFolder);
    }

    private string? FindCurrentTree(RepositoryLocation repository, IReadOnlyDictionary<string, string?> environment)
    {
        var result = _git.Run(repository, new List<string> { "write-tree" }, environment, null);
        return result.ExitCode == 0 ? result.Output.Trim() : null;
    }

    private bool TryMove(RepositoryLocation repository, IReadOnlyDictionary<string, string?> environment, List<string> arguments)
    {
        var result = _git.Run(repository, WithSettings(arguments), environment, null);
        return result.ExitCode == 0;
    }

    private static List<string> WithSettings(List<string> arguments)
    {
        var settings = new List<string> { "-c", "core.sparseCheckout=false", "-c", "core.longpaths=true" };
        settings.AddRange(arguments);

        return settings;
    }
}
