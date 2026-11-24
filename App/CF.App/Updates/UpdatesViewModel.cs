using System.Diagnostics;
using Avalonia.Threading;
using CF.Core.Ports;
using CF.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Updates;

/// <summary>
/// Automatic updates: a check when the app starts and once a day after (and when asked), the notice with the release notes,
/// and the install and restart once the user says so. Nothing is downloaded before the user says yes.
/// </summary>
internal sealed partial class UpdatesViewModel : ObservableObject
{
    private const int _notesLength = 1500;
    private static readonly TimeSpan _timerInterval = TimeSpan.FromHours(1);

    private readonly IUpdater _updater;
    private readonly IUpdateCheckRecord _record;
    private readonly IClock _clock;
    private readonly string _folderPath;
    private DispatcherTimer? _timer;

    // set while an update downloads: Cancel stops it
    private UpdateCancellation? _installCancellation;

    // the version the user sent away with Later: an automatic check does not bring it back
    private Version? _dismissedVersion;

    public UpdatesViewModel(IUpdater updater, IUpdateCheckRecord record, IClock clock, string folderPath)
    {
        _updater = updater;
        _record = record;
        _clock = clock;
        _folderPath = folderPath;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsChecking { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(CanCancel), nameof(CanDismiss), nameof(CanUpdate), nameof(HasNotes), nameof(IsNoticeVisible))]
    public partial bool IsInstalling { get; set; }

    /// <summary>
    /// The files are being replaced: too late to cancel.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    public partial bool IsReplacing { get; set; }

    /// <summary>
    /// What the notice says about a check the user asked for, or about an update that is running.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Headline), nameof(HasStatus), nameof(IsNoticeVisible))]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate), nameof(IsNoticeVisible), nameof(Headline), nameof(NotesLines), nameof(HasNotes), nameof(ProblemText), nameof(HasProblem), nameof(CanUpdate), nameof(CanOpenReleasePage))]
    public partial ReleaseInfo? AvailableRelease { get; set; }

    /// <summary>
    /// 0 to 100 while downloading; -1 when the size is not known.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloadPercentKnown), nameof(DownloadFraction))]
    public partial int DownloadPercent { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoticeVisible))]
    public partial bool IsDismissed { get; set; }

    /// <summary>
    /// Starts the app again from where it is and ends this run: false when it could not start. The window gives it.
    /// </summary>
    public Func<ProcessStartInfo, bool>? StartAndExit { get; set; }

    /// <summary>
    /// Opens an address in the browser. The window gives it.
    /// </summary>
    public Func<Uri, Task>? OpenUri { get; set; }

    public Version CurrentVersion
    {
        get { return _updater.CurrentVersion; }
    }

    public bool HasUpdate
    {
        get { return AvailableRelease != null; }
    }

    public bool HasStatus
    {
        get { return StatusText != null; }
    }

    /// <summary>
    /// The notice shows while a newer release waits and was not sent away, and while it has something to say about a check
    /// the user asked for or an update that runs.
    /// </summary>
    public bool IsNoticeVisible
    {
        get { return HasStatus || (HasUpdate && !IsDismissed); }
    }

    /// <summary>
    /// The first line of the notice: what is going on, or the version that is available.
    /// </summary>
    public string Headline
    {
        get
        {
            if(StatusText != null)
                return StatusText;

            return AvailableRelease == null ? string.Empty : $"CommitFence {AvailableRelease.Version} is available. You have {CurrentVersion}.";
        }
    }

    /// <summary>
    /// What the release notes say, laid out line by line.
    /// </summary>
    public IReadOnlyList<ReleaseNoteLine> NotesLines
    {
        get { return AvailableRelease == null ? new List<ReleaseNoteLine>() : ReleaseNotes.Lines(AvailableRelease.Notes, _notesLength); }
    }

    public bool HasNotes
    {
        get { return NotesLines.Count > 0 && !IsInstalling; }
    }

    /// <summary>
    /// Why the app cannot install the release here, or null when it can.
    /// </summary>
    public string? ProblemText
    {
        get { return AvailableRelease == null ? null : _updater.FindProblem(AvailableRelease); }
    }

    public bool HasProblem
    {
        get { return HasUpdate && ProblemText != null; }
    }

    /// <summary>
    /// "Update now" shows when the release waits and the app can install it.
    /// </summary>
    public bool CanUpdate
    {
        get { return HasUpdate && ProblemText == null && !IsInstalling; }
    }

    /// <summary>
    /// "Open the release page" stands in place of "Update now" where the app cannot update itself.
    /// </summary>
    public bool CanOpenReleasePage
    {
        get { return HasUpdate && ProblemText != null && AvailableRelease?.PageUri != null; }
    }

    /// <summary>
    /// Later: not while an update runs.
    /// </summary>
    public bool CanDismiss
    {
        get { return !IsInstalling; }
    }

    /// <summary>
    /// Check and Update are off while a check or an install runs.
    /// </summary>
    public bool IsIdle
    {
        get { return !IsChecking && !IsInstalling; }
    }

    /// <summary>
    /// Cancel: while an update downloads and unpacks, not once the files are being replaced.
    /// </summary>
    public bool CanCancel
    {
        get { return IsInstalling && !IsReplacing; }
    }

    public bool IsDownloadPercentKnown
    {
        get { return DownloadPercent >= 0; }
    }

    /// <summary>
    /// How much of the download is done, 0 to 1 (0 while the size is not known).
    /// </summary>
    public double DownloadFraction
    {
        get { return DownloadPercent < 0 ? 0 : DownloadPercent / 100.0; }
    }

    /// <summary>
    /// Called once the window is up: removes what the last update left, checks if a day has passed, and looks again every
    /// hour while the app stays open.
    /// </summary>
    public void Start()
    {
        _ = _updater.CleanUpAsync(10);
        _timer = new DispatcherTimer { Interval = _timerInterval };
        _timer.Tick += OnTimerTick;
        _timer.Start();
        _ = CheckIfDueAsync();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _ = CheckIfDueAsync();
    }

    /// <summary>
    /// The automatic check: only when a day has passed since the last one that worked. A failure says nothing (no network
    /// is normal); the next tick tries again.
    /// </summary>
    public async Task CheckIfDueAsync()
    {
        if(!IsIdle)
            return;

        if(UpdateSchedule.IsCheckDue(_record.LastCheckUtc, _clock.Now.UtcDateTime))
            await CheckAsync(false);
    }

    [RelayCommand]
    private async Task CheckNowAsync()
    {
        await CheckAsync(true);
    }

    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        await InstallAsync();
    }

    /// <summary>
    /// Asks for the latest release. A newer one shows in the notice; a check the user asked for also says when there is none
    /// or when it failed.
    /// </summary>
    public async Task CheckAsync(bool userAsked)
    {
        if(!IsIdle)
            return;

        IsChecking = true;
        if(userAsked)
            StatusText = "Checking for updates…";

        UpdateCheckResult result;
        try
        {
            result = await _updater.CheckAsync(CancellationToken.None);
        }
        finally
        {
            IsChecking = false;
        }

        if(result.Status == UpdateCheckStatus.Failed || result.Release == null)
        {
            StatusText = userAsked ? $"Could not check for updates. {result.Problem}" : null;
            return;
        }

        _record.Save(_clock.Now.UtcDateTime);
        if(result.Status == UpdateCheckStatus.UpToDate)
        {
            AvailableRelease = null;
            StatusText = userAsked ? $"You have the latest version ({CurrentVersion})." : null;
            return;
        }

        StatusText = null;
        AvailableRelease = result.Release;
        IsDismissed = !userAsked && Equals(_dismissedVersion, result.Release.Version);
    }

    /// <summary>
    /// Downloads and checks the release, replaces this copy and restarts. Where the app cannot replace itself, nothing
    /// is changed (the notice offers the release page).
    /// </summary>
    public async Task InstallAsync()
    {
        var release = AvailableRelease;
        if(release == null || !IsIdle || _updater.FindProblem(release) != null)
            return;

        IsReplacing = false;
        IsInstalling = true;
        DownloadPercent = 0;
        StatusText = $"Downloading version {release.Version}…";
        using(var cancellation = new UpdateCancellation())
        {
            _installCancellation = cancellation;

            // Progress posts its reports to this thread, so one can arrive after the install has returned: it must change nothing
            var ended = false;
            try
            {
                var progress = new Progress<UpdateProgress>(report =>
                {
                    if(!ended)
                        OnInstallProgress(report);
                });
                await Task.Run(() => _updater.InstallAsync(release, progress, cancellation));
            }
            catch(OperationCanceledException) when(cancellation.IsCancelled)
            {
                StopInstalling($"The update was cancelled. You still have version {CurrentVersion}.");
                return;
            }
            catch(Exception exception)
            {
                StopInstalling($"The update failed. {exception.Message} You still have version {CurrentVersion}.");
                return;
            }
            finally
            {
                ended = true;
                _installCancellation = null;
            }
        }

        // what a finished install means is set here, whether or not the "replacing" report ever arrived: the files are replaced and
        // Cancel is gone
        IsReplacing = true;
        StatusText = $"Version {release.Version} is installed. Restarting…";
        if(StartAndExit != null && StartAndExit(_updater.CreateStartInfo(_folderPath)))
            return;

        StopInstalling($"Version {release.Version} is installed. Close the app and start it again to use it.");
    }

    /// <summary>
    /// Stops the download. Once the files are being replaced it is too late, and Cancel says so: the swap takes a moment and
    /// finishes.
    /// </summary>
    [RelayCommand]
    private void CancelUpdate()
    {
        if(_installCancellation == null || _installCancellation.IsCancelled)
            return;

        var cancelled = _installCancellation.TryCancel();

        // cancelling can end the update on this very call, which has said what happened already
        if(IsInstalling)
            StatusText = cancelled ? "Cancelling the update…" : "Too late to cancel: the files are being replaced.";
    }

    /// <summary>
    /// Sends the notice away: a newer version stays away until a later one comes, or the user asks to check.
    /// </summary>
    [RelayCommand]
    private void Dismiss()
    {
        if(IsInstalling)
            return;

        _dismissedVersion = AvailableRelease?.Version;
        StatusText = null;
        IsDismissed = true;
    }

    [RelayCommand]
    private async Task OpenReleasePageAsync()
    {
        var page = AvailableRelease?.PageUri;
        if(page != null && OpenUri != null)
            await OpenUri(page);
    }

    private void StopInstalling(string statusText)
    {
        IsInstalling = false;
        IsReplacing = false;
        StatusText = statusText;
    }

    private void OnInstallProgress(UpdateProgress progress)
    {
        if(progress.Stage == UpdateStage.Replacing)
        {
            IsReplacing = true;
            if(AvailableRelease != null && _installCancellation != null)
                StatusText = $"Replacing the files with version {AvailableRelease.Version}…";

            return;
        }

        DownloadPercent = progress.Percent;
        if(progress.Percent >= 0 && AvailableRelease != null && _installCancellation != null && !_installCancellation.IsCancelled)
            StatusText = $"Downloading version {AvailableRelease.Version}… {progress.Percent}%";
    }
}