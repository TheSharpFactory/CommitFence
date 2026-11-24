using CF.Adapters.Machine;
using CF.Core.Ports;

namespace CF.Tests.Review.Support;

/// <summary>
/// The real file system, with a hook that is told before each text is read and may hold the reader until the test lets it
/// go.
/// </summary>
internal sealed class ObservedFileSystem : IFileSystem
{
    private readonly SystemFileSystem _inner = new SystemFileSystem();
    private readonly Action<string> _beforeRead;
    private readonly Action<string, string> _afterRead;

    public ObservedFileSystem(Action<string> beforeRead, Action<string, string> afterRead)
    {
        _beforeRead = beforeRead;
        _afterRead = afterRead;
    }

    public string ResolveRealPath(string path)
    {
        return _inner.ResolveRealPath(path);
    }

    public bool FileExists(string path)
    {
        return _inner.FileExists(path);
    }

    public string ReadAllText(string path)
    {
        _beforeRead(path);
        var text = _inner.ReadAllText(path);
        _afterRead(path, text);

        return text;
    }

    public Stream OpenRead(string path)
    {
        return _inner.OpenRead(path);
    }

    public void WriteAllText(string path, string content)
    {
        _inner.WriteAllText(path, content);
    }

    public void WriteNewFile(string path, string content)
    {
        _inner.WriteNewFile(path, content);
    }

    public void ReplaceFile(string source, string destination)
    {
        _inner.ReplaceFile(source, destination);
    }

    public void CreateFolder(string path)
    {
        _inner.CreateFolder(path);
    }

    public void DeleteFile(string path)
    {
        _inner.DeleteFile(path);
    }

    public IReadOnlyList<string> FindFiles(string folder, string extension, IReadOnlyList<string> skippedFolderNames)
    {
        return _inner.FindFiles(folder, extension, skippedFolderNames);
    }
}

/// <summary>
/// A review lock that says when someone asks for it, then asks the lock it stands in front of.
/// </summary>
internal sealed class ProbedReviewLock : IReviewLock
{
    private readonly IReviewLock _inner;
    private readonly Action<int> _asked;
    private int _askedCount;

    public ProbedReviewLock(IReviewLock inner, Action<int> asked)
    {
        _inner = inner;
        _asked = asked;
    }

    public IDisposable Acquire(RepositoryLocation repository)
    {
        _asked(Interlocked.Increment(ref _askedCount));
        return _inner.Acquire(repository);
    }
}
