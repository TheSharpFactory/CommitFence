using CF.Core.Ports;

namespace CF.Adapters.Storage;

/// <summary>
/// One new folder for each review's logs, inside the repository's data folder: <c>logs\&lt;time&gt;-&lt;random&gt;</c>.
/// </summary>
/// <remarks>
/// The few newest folders are kept for looking at what happened; older ones are removed.
/// </remarks>
public sealed class ReviewLogFolders : IReviewLogs
{
    private const int _foldersKept = 5;

    private readonly IDataFolder _dataFolder;

    /// <summary>
    /// Creates the store.
    /// </summary>
    public ReviewLogFolders(IDataFolder dataFolder)
    {
        _dataFolder = dataFolder;
    }

    /// <inheritdoc />
    public string CreateFolder(RepositoryLocation repository)
    {
        var logs = Path.Combine(RepositoryDataFolder.For(_dataFolder.RootFolder, repository), "logs");
        Directory.CreateDirectory(logs);
        var folder = Path.Combine(logs, $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N").Substring(0, 8)}");
        Directory.CreateDirectory(folder);
        RemoveOld(logs);

        return folder;
    }

    private static void RemoveOld(string logs)
    {
        var old = Directory.EnumerateDirectories(logs).OrderByDescending(folder => Path.GetFileName(folder), StringComparer.Ordinal).Skip(_foldersKept);
        foreach(var folder in old)
            TryDelete(folder);
    }

    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, true);
        }
        catch(IOException)
        {
            // an old log that cannot be removed now is removed by a later review
        }
        catch(UnauthorizedAccessException)
        {
            // the same: leftovers never fail a review
        }
    }
}