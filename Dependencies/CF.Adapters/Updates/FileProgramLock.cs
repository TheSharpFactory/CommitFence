using CF.Core.Ports;

namespace CF.Adapters.Updates;

/// <summary>
/// The program lock as a file in the program's folder, held open shared for as long as a review runs. An update takes
/// the same file alone before it replaces the files, so the two never overlap.
/// </summary>
/// <remarks>
/// A copy that is not a release download (a build run from the source) cannot be updated in place, so it holds nothing.
/// </remarks>
public sealed class FileProgramLock : IProgramLock
{
    private static readonly TimeSpan _defaultWaitLimit = TimeSpan.FromSeconds(60);

    private readonly InstallLocation _location;
    private readonly TimeSpan _waitLimit;

    /// <summary>
    /// Creates the lock for the running program.
    /// </summary>
    public FileProgramLock()
        : this(InstallLocation.Find(Environment.ProcessPath, InstallLocation.CurrentPlatform), _defaultWaitLimit)
    {
    }

    internal FileProgramLock(InstallLocation location, TimeSpan waitLimit)
    {
        _location = location;
        _waitLimit = waitLimit;
    }

    /// <inheritdoc />
    public IDisposable HoldForReview()
    {
        if(_location.Problem != null)
            return new ProgramLockFile.NothingHeld();

        return ProgramLockFile.TryHoldShared(_location.Folder, _waitLimit) ?? throw new TimeoutException($"an update of the program has held the lock for more than {_waitLimit.TotalSeconds:0} seconds ({ProgramLockFile.PathIn(_location.Folder)})");
    }
}