using CF.Core.Ports;

namespace CF.Adapters.Storage;

/// <summary>
/// The settings lock as a file in the data folder (never beside the team's file), named from a hash of the settings
/// file's full path and held open without sharing for as long as the change takes.
/// </summary>
public sealed class FileSettingsLock : ISettingsLock
{
    private static readonly TimeSpan _waitLimit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _retryInterval = TimeSpan.FromMilliseconds(50);

    private readonly IDataFolder _dataFolder;

    /// <summary>
    /// Creates the lock.
    /// </summary>
    public FileSettingsLock(IDataFolder dataFolder)
    {
        _dataFolder = dataFolder;
    }

    /// <inheritdoc />
    public IDisposable Take(string settingsPath)
    {
        var folder = Path.Combine(_dataFolder.RootFolder, "settings-locks");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, PathHash.Of(settingsPath) + ".lock");

        return HeldFile.TryHold(path, _waitLimit, _retryInterval) ?? throw new TimeoutException($"another command has held the lock of {settingsPath} for more than {_waitLimit.TotalSeconds:0} seconds ({path})");
    }
}