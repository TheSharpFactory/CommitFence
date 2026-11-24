using Avalonia.Threading;

namespace CF.App.Rules;

/// <summary>
/// Says when the settings file changes on disk, so that a change made by the command line shows in the list without a
/// restart. It only watches: nothing here reads or writes the file.
/// </summary>
internal sealed class SettingsFileWatcher : ISettingsChangeSource
{
    private static readonly TimeSpan _quietTime = TimeSpan.FromMilliseconds(150);

    private readonly FileSystemWatcher? _watcher;
    private readonly Timer _timer;

    public SettingsFileWatcher(string settingsPath)
    {
        _timer = new Timer(_ => Dispatcher.UIThread.Post(RaiseChanged), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        var folder = Path.GetDirectoryName(settingsPath);
        if(folder == null || !Directory.Exists(folder))
            return;

        _watcher = new FileSystemWatcher(folder, Path.GetFileName(settingsPath))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
        };
        _watcher.Changed += (_, _) => Restart();
        _watcher.Created += (_, _) => Restart();
        _watcher.Deleted += (_, _) => Restart();
        _watcher.Renamed += (_, _) => Restart();
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Raised on the interface's thread, shortly after the file stopped changing.
    /// </summary>
    public event Action? Changed;

    public void Dispose()
    {
        _watcher?.Dispose();
        _timer.Dispose();
    }

    private void Restart()
    {
        _timer.Change(_quietTime, Timeout.InfiniteTimeSpan);
    }

    private void RaiseChanged()
    {
        Changed?.Invoke();
    }
}
