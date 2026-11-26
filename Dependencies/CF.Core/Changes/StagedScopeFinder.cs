using CF.Core.Ports;

namespace CF.Core.Changes;

/// <summary>
/// Tells what the index holds now, so that the app can say how many files a review of the staged changes would look at, and
/// whether there is anything to review.
/// </summary>
public sealed class StagedScopeFinder
{
    private const int _shortCommit = 7;
    private const int _mostFilesRead = 60;
    private const int _mostUnstagedListed = 8;

    private readonly IGit _git;
    private readonly IWorkingStatus _status;

    /// <summary>
    /// Creates the finder.
    /// </summary>
    public StagedScopeFinder(IGit git, IWorkingStatus status)
    {
        _git = git;
        _status = status;
    }

    /// <summary>
    /// Answers for the repository that holds the folder.
    /// </summary>
    public StagedAnswer Find(string folder)
    {
        try
        {
            var repository = _git.FindRepository(folder);
            if(repository == null)
                return StagedAnswer.Unknown("The folder is not inside a git repository.");

            var status = _status.Read(repository);
            var head = _git.ResolveCommit(repository, "HEAD");
            var indexTree = _git.WriteStagedTree(repository);
            var baseTree = _git.ResolveTree(repository, "HEAD") ?? _git.EmptyTree(repository);
            var sizes = SizesOf(repository, indexTree, status.Staged);
            var unstaged = UnstagedOf(repository, baseTree, status);
            return StagedAnswer.Of(status.Staged, status.Unstaged.Count, status.StagedFileHasUnstagedChanges, head == null ? string.Empty : head[.._shortCommit], indexTree, baseTree, sizes, unstaged);
        }
        catch(GitException exception)
        {
            return StagedAnswer.Unknown("git could not be asked: " + exception.Message);
        }
    }

    /// <summary>
    /// How many lines each staged file has, read from the tree of the index. A large number of files is not read: the lines then are
    /// not told, and the files still are.
    /// </summary>
    private List<StagedFileSize> SizesOf(RepositoryLocation repository, string indexTree, IReadOnlyList<string> staged)
    {
        if(staged.Count == 0 || staged.Count > _mostFilesRead)
            return [];

        var sizes = new List<StagedFileSize>();
        foreach(var path in staged)
        {
            var text = _git.ReadFile(repository, indexTree, path);
            sizes.Add(new StagedFileSize(path, text == null ? 0 : CountLines(text)));
        }

        return sizes;
    }

    /// <summary>
    /// The first files with changes that are not staged, each said to be new when the last commit does not have it.
    /// </summary>
    private List<UnstagedFile> UnstagedOf(RepositoryLocation repository, string baseTree, WorkingStatus status)
    {
        return status.Unstaged.Take(_mostUnstagedListed).Select(path => new UnstagedFile(path, _git.ReadFile(repository, baseTree, path) == null)).ToList();
    }

    private static int CountLines(string text)
    {
        var lines = text.Count(character => character == '\n');
        return text.Length > 0 && !text.EndsWith('\n') ? lines + 1 : lines;
    }
}
