using System.Text;
using CF.Adapters.Storage;
using CF.Core.Ports;

namespace CF.Adapters.Git;

/// <summary>
/// Answers the product's questions about a repository by running the git command, on git's own objects and refs.
/// </summary>
public sealed class GitCommandLine : IGit
{
    private const string _endOfOptions = "--end-of-options";
    private const int _filesPerTextReading = 50;

    private static readonly IReadOnlyDictionary<string, string?> _literalPaths = new Dictionary<string, string?> { ["GIT_LITERAL_PATHSPECS"] = "1" };

    private readonly GitRunner _git;
    private readonly IDataFolder _dataFolder;

    /// <summary>
    /// Creates the adapter. The data folder is where its temporary copy of the index lives while git reads what is
    /// staged.
    /// </summary>
    public GitCommandLine(IProcessRunner processRunner, IDataFolder dataFolder)
    {
        _git = new GitRunner(processRunner);
        _dataFolder = dataFolder;
    }

    /// <inheritdoc />
    public RepositoryLocation? FindRepository(string folder)
    {
        var result = _git.RunOutsideRepository(folder, new List<string> { "rev-parse", "--show-toplevel", "--absolute-git-dir" });
        if(result.ExitCode == -1)
            throw new GitException(string.IsNullOrWhiteSpace(result.Error) ? GitRunner.Describe(new List<string> { "rev-parse" }, result) : result.Error);

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if(result.ExitCode != 0 || lines.Length != 2)
            return null;

        return new RepositoryLocation(lines[0], lines[1]);
    }

    /// <inheritdoc />
    public bool IsShallow(RepositoryLocation repository)
    {
        var output = _git.RunChecked(repository, new List<string> { "rev-parse", "--is-shallow-repository" });
        return output.Trim() == "true";
    }

    /// <inheritdoc />
    public string? ResolveCommit(RepositoryLocation repository, string revision)
    {
        return Resolve(repository, revision + "^{commit}");
    }

    /// <inheritdoc />
    public string? ResolveTree(RepositoryLocation repository, string revision)
    {
        return Resolve(repository, revision + "^{tree}");
    }

    /// <inheritdoc />
    public string EmptyTree(RepositoryLocation repository)
    {
        var output = _git.RunChecked(repository, new List<string> { "hash-object", "-t", "tree", "--stdin" }, new Dictionary<string, string?>(), string.Empty);
        return output.Trim();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> RemoteTrackingTips(RepositoryLocation repository, string remoteName)
    {
        var output = _git.RunChecked(repository, new List<string> { "for-each-ref", "--format=%(objectname)", $"refs/remotes/{remoteName}/" });
        return SplitLines(output);
    }

    /// <inheritdoc />
    public string? CommonAncestor(RepositoryLocation repository, IReadOnlyList<string> commits)
    {
        if(commits.Count == 1)
            return commits[0];

        var arguments = new List<string> { "merge-base", "--octopus" };
        arguments.AddRange(commits);
        var result = _git.Run(repository, arguments);
        if(result.ExitCode == 1)
            return null;

        if(result.ExitCode != 0)
            throw new GitException(GitRunner.Describe(arguments, result));

        return result.Output.Trim();
    }

    /// <inheritdoc />
    public bool HasCommitsOutside(RepositoryLocation repository, string tip, IReadOnlyList<string> excluded)
    {
        var arguments = new List<string> { "rev-list", "--max-count=1", tip };
        if(excluded.Count > 0)
        {
            arguments.Add("--not");
            arguments.AddRange(excluded);
        }

        return _git.RunChecked(repository, arguments).Trim().Length > 0;
    }

    /// <inheritdoc />
    public bool HasUnpublishedLocalCommits(RepositoryLocation repository, string remoteName)
    {
        var arguments = new List<string> { "rev-list", "--max-count=1", "--branches", "--tags", "--not", $"--remotes={remoteName}" };
        return _git.RunChecked(repository, arguments).Trim().Length > 0;
    }

    /// <inheritdoc />
    public string WriteStagedTree(RepositoryLocation repository)
    {
        var dataFolder = RepositoryDataFolder.For(_dataFolder.RootFolder, repository);
        var copyPath = Path.Combine(dataFolder, "commitfence-index-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dataFolder);
            CopyIndex(repository, copyPath);
            var environment = new Dictionary<string, string?> { ["GIT_INDEX_FILE"] = copyPath };
            var result = _git.Run(repository, new List<string> { "write-tree" }, environment, null);
            if(result.ExitCode != 0)
                throw new GitException("what is staged cannot be read: " + result.Error.Trim());

            return result.Output.Trim();
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            throw new GitException("what is staged cannot be read: " + exception.Message);
        }
        finally
        {
            // git keeps its own lock beside the index it rewrites; a git that was stopped at its time limit leaves that
            // lock behind
            DeleteIfPossible(copyPath);
            DeleteIfPossible(copyPath + ".lock");
        }
    }

    private static void DeleteIfPossible(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            // a failure while cleaning up must never replace the error that is already on its way; a scratch file left
            // in the data folder harms nothing, and the next review uses a new name
        }
    }

    private static void CopyIndex(RepositoryLocation repository, string copyPath)
    {
        // git's write-tree also rewrites the index it reads (its cache of trees), so it is given a copy: the
        // developer's index stays byte for byte as it was
        var named = Environment.GetEnvironmentVariable("GIT_INDEX_FILE");
        var source = string.IsNullOrEmpty(named) ? Path.Combine(repository.GitDirectory, "index") : Path.GetFullPath(named, repository.WorkFolder);
        if(File.Exists(source))
            File.Copy(source, copyPath);
    }

    /// <inheritdoc />
    public string? ReadFile(RepositoryLocation repository, string tree, string path)
    {
        if(!TreeHoldsFile(repository, tree, path))
            return null;

        return _git.RunChecked(repository, new List<string> { "cat-file", "blob", tree + ":" + path });
    }

    private bool TreeHoldsFile(RepositoryLocation repository, string tree, string path)
    {
        // git itself says whether the path is in the tree (a listing that is empty when it is not); a git that fails
        // here throws, so a failure is never read as "no such file"
        var listing = _git.RunChecked(repository, new List<string> { "ls-tree", "--full-tree", "-z", tree, "--", path }, _literalPaths, null);
        foreach(var entry in listing.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = entry.IndexOf('\t');
            var isFile = entry.AsSpan(0, Math.Max(tab, 0)).Contains(" blob ", StringComparison.Ordinal);
            if(isFile && entry.Substring(tab + 1) == path)
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public string ReadDiff(RepositoryLocation repository, string before, string after)
    {
        return _git.RunChecked(repository, DiffArguments(before, after, false));
    }

    /// <inheritdoc />
    public string ReadDiffAsText(RepositoryLocation repository, string before, string after, IReadOnlyList<IReadOnlyList<string>> namesOfEachFile)
    {
        // the command line of the system has a limit on its length: the files are read in batches, and the two names of
        // a renamed file stay in the one batch so that git still sees the rename
        var diff = new StringBuilder();
        foreach(var batch in namesOfEachFile.Chunk(_filesPerTextReading))
        {
            var arguments = DiffArguments(before, after, true);
            arguments.AddRange(batch.SelectMany(names => names));
            diff.Append(_git.RunChecked(repository, arguments, _literalPaths, null));
        }

        return diff.ToString();
    }

    private static List<string> DiffArguments(string before, string after, bool asText)
    {
        var arguments = new List<string> { "-c", "core.quotepath=false", "diff", "--no-ext-diff", "--no-textconv", "--no-color", "--find-renames", "--unified=0" };
        if(asText)
            arguments.Add("--text");

        arguments.AddRange(new[] { "--src-prefix=a/", "--dst-prefix=b/", before, after, "--" });
        return arguments;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListFiles(RepositoryLocation repository, string tree, string folder)
    {
        var arguments = new List<string> { "ls-tree", "-r", "-z", "--full-tree", tree };
        if(folder.Length > 0)
        {
            arguments.Add("--");
            arguments.Add(folder);
        }

        var listing = _git.RunChecked(repository, arguments, _literalPaths, null);
        return listing.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(IsBlobEntry).Select(PathOfEntry).ToList();
    }

    private static bool IsBlobEntry(string entry)
    {
        var tab = entry.IndexOf('\t');
        return tab > 0 && entry.AsSpan(0, tab).Contains(" blob ", StringComparison.Ordinal);
    }

    private static string PathOfEntry(string entry)
    {
        return entry.Substring(entry.IndexOf('\t') + 1);
    }

    /// <inheritdoc />
    public string? CurrentBranch(RepositoryLocation repository)
    {
        var branch = _git.Run(repository, new List<string> { "symbolic-ref", "--short", "--quiet", "HEAD" });
        if(branch.ExitCode == 0 && branch.Output.Trim().Length > 0)
            return branch.Output.Trim();

        var commit = _git.Run(repository, new List<string> { "rev-parse", "--short", "--verify", "--quiet", "HEAD" });
        if(commit.ExitCode == 0 && commit.Output.Trim().Length > 0)
            return commit.Output.Trim();

        return null;
    }

    /// <inheritdoc />
    public string? CheckedOutBranch(RepositoryLocation repository)
    {
        var result = _git.Run(repository, new List<string> { "symbolic-ref", "--short", "--quiet", "HEAD" });
        return result.ExitCode == 0 && result.Output.Trim().Length > 0 ? result.Output.Trim() : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> RemoteNames(RepositoryLocation repository)
    {
        return SplitLines(_git.RunChecked(repository, new List<string> { "remote" }));
    }

    /// <inheritdoc />
    public string? RemoteUrl(RepositoryLocation repository, string remoteName)
    {
        var result = _git.Run(repository, new List<string> { "remote", "get-url", "--push", _endOfOptions, remoteName });
        return result.ExitCode == 0 && result.Output.Trim().Length > 0 ? result.Output.Trim() : null;
    }

    /// <inheritdoc />
    public BranchUpstream? Upstream(RepositoryLocation repository, string branch)
    {
        var remote = ReadConfig(repository, $"branch.{branch}.remote");
        var merge = ReadConfig(repository, $"branch.{branch}.merge");
        if(remote == null || merge == null)
            return null;

        return new BranchUpstream(remote, merge);
    }

    private string? ReadConfig(RepositoryLocation repository, string key)
    {
        var result = _git.Run(repository, new List<string> { "config", "--get", key });
        return result.ExitCode == 0 && result.Output.Trim().Length > 0 ? result.Output.Trim() : null;
    }

    /// <inheritdoc />
    public bool HasUncommittedWork(RepositoryLocation repository, string folder)
    {
        // no lock and no refresh of the index, and no file system monitor started by the status
        var arguments = new List<string> { "--no-optional-locks", "-c", "core.fsmonitor=false", "status", "--porcelain=v1", "-z", "--untracked-files=normal", "--" };
        if(folder.Length > 0)
            arguments.Add(Pathspec(folder));

        return _git.RunChecked(repository, arguments).Length > 0;
    }

    /// <summary>
    /// A folder as a pathspec: from the top of the repository, taken literally.
    /// </summary>
    private static string Pathspec(string folder)
    {
        return ":(top,literal)" + folder;
    }

    private string? Resolve(RepositoryLocation repository, string expression)
    {
        var result = _git.Run(repository, new List<string> { "rev-parse", "--verify", "--quiet", _endOfOptions, expression });
        if(result.ExitCode == 0)
            return result.Output.Trim();

        if(result.ExitCode == 1)
            return null;

        throw new GitException(GitRunner.Describe(new List<string> { "rev-parse" }, result));
    }

    private static IReadOnlyList<string> SplitLines(string output)
    {
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}