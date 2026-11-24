using CF.Core.Ports;

namespace CF.Adapters.Storage;

/// <summary>
/// The review lock as a file in the repository's data folder, held open without sharing for as long as the review runs.
/// </summary>
/// <remarks>
/// The operating system does not wait for a lock, so a second review tries again until a bounded time has passed.
/// </remarks>
public sealed class FileReviewLock : IReviewLock
{
    private static readonly TimeSpan _waitLimit = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _retryInterval = TimeSpan.FromMilliseconds(100);

    private readonly IDataFolder _dataFolder;

    /// <summary>
    /// Creates the lock.
    /// </summary>
    public FileReviewLock(IDataFolder dataFolder)
    {
        _dataFolder = dataFolder;
    }

    /// <inheritdoc />
    public IDisposable Acquire(RepositoryLocation repository)
    {
        var folder = RepositoryDataFolder.For(_dataFolder.RootFolder, repository);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "lock");

        return HeldFile.TryHold(path, _waitLimit, _retryInterval) ?? throw new TimeoutException($"another review of this repository has held the lock for more than {_waitLimit.TotalMinutes:0} minutes ({path})");
    }
}