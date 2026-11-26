using CF.Core.Ports;

namespace CF.Tests.Review.Support;

/// <summary>
/// The private copy of a tree, written into a folder of the test from the files a <see cref="FakeGit" /> holds. Every
/// move is recorded, in order.
/// </summary>
internal sealed class FakeSnapshots : ISnapshotStore
{
    private readonly FakeGit _git;
    private readonly string _folder;
    private readonly Lock _syncRoot = new Lock();
    private readonly List<string> _moves = new List<string>();

    public FakeSnapshots(FakeGit git, string folder)
    {
        _git = git;
        _folder = folder;
    }

    /// <summary>
    /// Told after the copy holds the tree, with the tree's id.
    /// </summary>
    public Action<string>? Moved { get; set; }

    public IReadOnlyList<string> Moves
    {
        get
        {
            lock(_syncRoot)
            {
                return _moves.ToList();
            }
        }
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
        var files = _git.FilesOf(tree);
        if(Directory.Exists(_folder))
            Directory.Delete(_folder, true);

        Directory.CreateDirectory(_folder);
        foreach(var file in files)
        {
            var path = Path.Combine(_folder, file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Value);
        }

        lock(_syncRoot)
        {
            _moves.Add(tree);
        }

        Moved?.Invoke(tree);
        return _folder;
    }
}
