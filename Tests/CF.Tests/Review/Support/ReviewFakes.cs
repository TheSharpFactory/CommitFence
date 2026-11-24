using CF.Core.Ports;
using CF.Core.Services;

namespace CF.Tests.Review.Support;

/// <summary>
/// A review lock that holds nothing: the test has one review at a time.
/// </summary>
internal sealed class NoReviewLock : IReviewLock
{
    public IDisposable Acquire(RepositoryLocation repository)
    {
        return new NothingToRelease();
    }
}

/// <summary>
/// The lock that keeps an update waiting for a review, replaced by one that holds nothing.
/// </summary>
internal sealed class NoProgramLock : IProgramLock
{
    public IDisposable HoldForReview()
    {
        return new NothingToRelease();
    }
}

internal sealed class NothingToRelease : IDisposable
{
    public void Dispose()
    {
    }
}

/// <summary>
/// The name and the version a test runs as.
/// </summary>
internal sealed class FixedProduct : IProductInfoService
{
    public const string ProgramVersion = "1.2.3.0";

    public string Name
    {
        get { return "CommitFence"; }
    }

    public string Version
    {
        get { return ProgramVersion; }
    }
}

/// <summary>
/// The data folder of a test.
/// </summary>
internal sealed class FolderOfTest : IDataFolder
{
    public FolderOfTest(string rootFolder)
    {
        RootFolder = rootFolder;
    }

    public string RootFolder { get; }
}

/// <summary>
/// Where a review keeps its logs: a new folder in the test's own folder each time.
/// </summary>
internal sealed class LogFoldersOfTest : IReviewLogs
{
    private readonly string _root;
    private int _count;

    public LogFoldersOfTest(string root)
    {
        _root = root;
    }

    public string CreateFolder(RepositoryLocation repository)
    {
        var folder = Path.Combine(_root, "log-" + Interlocked.Increment(ref _count));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
