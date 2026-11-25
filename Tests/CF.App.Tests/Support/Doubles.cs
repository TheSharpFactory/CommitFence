using System.Diagnostics;
using CF.App.Rules;
using CF.Core.Ports;
using CF.Core.Updates;

namespace CF.App.Tests.Support;

/// <summary>
/// When a fake updater delivers its progress reports.
/// </summary>
public enum ProgressDelivery
{
    /// <summary>Before the install returns.</summary>
    Before,

    /// <summary>Held back until the test delivers them, after the install has returned.</summary>
    After,

    /// <summary>Never.</summary>
    Never
}

/// <summary>
/// A clock that stands where the test puts it.
/// </summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; set; }
}

/// <summary>
/// The record of the last check for a newer release, in memory.
/// </summary>
internal sealed class MemoryCheckRecord : IUpdateCheckRecord
{
    public DateTime? LastCheckUtc { get; set; }

    public void Save(DateTime checkedUtc)
    {
        LastCheckUtc = checkedUtc;
    }
}

/// <summary>
/// A data folder that is the one the test gives.
/// </summary>
internal sealed class FixedDataFolder : IDataFolder
{
    public FixedDataFolder(string rootFolder)
    {
        RootFolder = rootFolder;
    }

    public string RootFolder { get; }
}

/// <summary>
/// A process runner that must never be asked: the tests of the app start no process.
/// </summary>
internal sealed class NoProcessRunner : IProcessRunner
{
    public ProcessResult Run(ProcessRequest request)
    {
        throw new InvalidOperationException("A test of the app started a process: " + request.FileName);
    }
}

/// <summary>
/// A settings file (or .editorconfig) that changes when the test says so, in place of the watcher of the disk.
/// </summary>
internal sealed class ManualChanges : ISettingsChangeSource
{
    public event Action? Changed;

    public void Raise()
    {
        Changed?.Invoke();
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// An updater that answers from what the test sets and records what it was asked; it touches no file and no network.
/// </summary>
internal sealed class FakeUpdater : IUpdater
{
    public Version CurrentVersion { get; } = Version.Parse(Samples.InstalledVersion);

    public UpdateCheckResult Result { get; set; } = UpToDate();

    public string? Problem { get; set; }

    public Exception? InstallFailure { get; set; }

    public int Checks { get; private set; }

    /// <summary>
    /// The progress the last install was given: a test reports through it after the install has ended, as a late report would.
    /// </summary>
    public IProgress<UpdateProgress>? LastProgress { get; private set; }

    /// <summary>
    /// When the reports of an install are delivered.
    /// </summary>
    public ProgressDelivery Delivery { get; set; }

    /// <summary>
    /// Delivers the reports that were held back (the ones of an install with <see cref="ProgressDelivery.After"/>).
    /// </summary>
    public void DeliverHeld()
    {
        foreach(var report in _held)
            LastProgress?.Report(report);

        _held.Clear();
    }

    private readonly List<UpdateProgress> _held = new List<UpdateProgress>();

    private void Report(UpdateProgress report)
    {
        if(Delivery == ProgressDelivery.Before)
            LastProgress?.Report(report);
        else if(Delivery == ProgressDelivery.After)
            _held.Add(report);
    }

    public int Installs { get; private set; }

    public string? StartArgument { get; private set; }

    /// <summary>
    /// When set, an install waits for it (or for the cancellation) before it ends.
    /// </summary>
    public TaskCompletionSource? HoldInstall { get; set; }

    /// <summary>
    /// Set when an install has begun and reported its first progress.
    /// </summary>
    public TaskCompletionSource InstallStarted { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// When set, a check waits for it before it answers.
    /// </summary>
    public TaskCompletionSource? HoldCheck { get; set; }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        Checks++;
        if(HoldCheck != null)
            await HoldCheck.Task;

        return Result;
    }

    public string? FindProblem(ReleaseInfo release)
    {
        return Problem;
    }

    public async Task InstallAsync(ReleaseInfo release, IProgress<UpdateProgress>? progress, UpdateCancellation? cancellation)
    {
        Installs++;
        LastProgress = progress;
        Report(new UpdateProgress { Stage = UpdateStage.Downloading, Percent = 40 });
        InstallStarted.TrySetResult();
        if(HoldInstall != null)
            await HoldInstall.Task.WaitAsync(cancellation?.Token ?? CancellationToken.None);

        if(InstallFailure != null)
            throw InstallFailure;

        if(cancellation != null && !cancellation.TryBeginReplacing())
            throw new OperationCanceledException(cancellation.Token);

        Report(new UpdateProgress { Stage = UpdateStage.Replacing });
    }

    public Task CleanUpAsync(int attempts)
    {
        return Task.CompletedTask;
    }

    public ProcessStartInfo CreateStartInfo(string? argument)
    {
        StartArgument = argument;
        var startInfo = new ProcessStartInfo("commitfenceapp");
        if(argument != null)
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }

    public static UpdateCheckResult UpToDate()
    {
        return new UpdateCheckResult { Status = UpdateCheckStatus.UpToDate, Release = new ReleaseInfo { Version = Version.Parse(Samples.InstalledVersion) } };
    }

    public static UpdateCheckResult Available(string version, string? notes = null, bool withPage = true)
    {
        var release = new ReleaseInfo
        {
            Version = Version.Parse(version),
            Notes = notes,
            PageUri = withPage ? new Uri("https://github.com/TheSharpFactory/CommitFence/releases/tag/v" + version) : null,
            PackageName = "CommitFence-win-x64.zip",
            PackageUri = new Uri("https://example.com/a.zip"),
            ChecksumsUri = new Uri("https://example.com/SHA256SUMS.txt"),
            SignatureUri = new Uri("https://example.com/SHA256SUMS.txt.sig")
        };
        return new UpdateCheckResult { Status = UpdateCheckStatus.Available, Release = release };
    }
}
