using System.Diagnostics;
using Avalonia.Threading;
using CF.Core.Ports;
using CF.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Updates;

/// <summary>
/// Automatic updates: a check when the app starts and once a day after (and when asked), the notice with its title, its line,
/// its bar and its buttons for each state of an update, and the install and restart once the user says so. Nothing is downloaded
/// before the user says yes. The notice and Settings read their words from here.
/// </summary>
internal sealed partial class UpdatesViewModel : ObservableObject
{
    private const int _notesLength = 1500;
    private static readonly TimeSpan _timerInterval = TimeSpan.FromHours(1);

    private static readonly string[] _noticeProperties =
    [
        nameof(NoticeState), nameof(IsNoticeVisible), nameof(Title), nameof(Line), nameof(Mark), nameof(ShowsBar), nameof(ShowsPercent), nameof(PercentText),
        nameof(BarFraction), nameof(Buttons), nameof(ShowsLater), nameof(ShowsWhatsNew), nameof(ShowsCancel), nameof(ShowsClose),
        nameof(ShowsReleasePage), nameof(ShowsTryAgain), nameof(ShowsUpdateNow), nameof(IsFinished), nameof(IsRunning), nameof(CanDismiss),
        nameof(CanUpdate), nameof(CanOpenReleasePage), nameof(HasUpdate), nameof(ProblemText), nameof(HasProblem), nameof(NotesLines), nameof(HasNotes),
        nameof(IsBusyMark), nameof(IsLogoMark), nameof(IsTickMark), nameof(IsFailureMark)
    ];

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

    /// <summary>
    /// Why the last update failed, in the sentence the body gave.
    /// </summary>
    public string Failure { get; private set; } = string.Empty;

    /// <summary>
    /// Whether a check of this run has answered, with a newer version or without one. The record keeps only when a check worked, so
    /// until this is true the screens claim nothing about the version being the latest.
    /// </summary>
    public bool HasAnswered { get; private set; }

    /// <summary>
    /// Why a check the user asked for could not finish, in the sentence the body gave; null while that check has not failed.
    /// </summary>
    public string? CheckProblem { get; private set; }

    /// <summary>
    /// Where an update that was started has got to.
    /// </summary>
    private enum InstallPhase
    {
        None,
        Downloading,
        Cancelling,
        Replacing,
        Installed,
        CouldNotRestart,
        Cancelled,
        Failed
    }

    /// <summary>
    /// Asked for by "What's new": the window opens Settings, where the notes of the version are shown.
    /// </summary>
    public event Action? WhatsNewRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsChecking { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(CanCancel), nameof(CanDismiss), nameof(CanUpdate), nameof(HasNotes))]
    public partial bool IsInstalling { get; set; }

    /// <summary>
    /// The files are being replaced: too late to cancel.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    public partial bool IsReplacing { get; set; }

    /// <summary>
    /// What a check the user asked for found: "Checking for updates…", "You have the latest version", or why it failed. It is
    /// Settings' to show; the notice answers a check only by showing a newer version.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    public partial ReleaseInfo? AvailableRelease { get; set; }

    /// <summary>
    /// 0 to 100 while downloading; -1 when the size is not known.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloadPercentKnown), nameof(DownloadFraction))]
    public partial int DownloadPercent { get; set; }

    [ObservableProperty]
    public partial bool IsDismissed { get; set; }

    [ObservableProperty]
    private partial InstallPhase Phase { get; set; }

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
    /// What the notice is saying: a newer version waits and was not sent away, an update runs, or one has just ended.
    /// </summary>
    public UpdateNoticeState NoticeState
    {
        get
        {
            return Phase switch
            {
                InstallPhase.Downloading => IsDownloadPercentKnown ? UpdateNoticeState.Downloading : UpdateNoticeState.SizeNotKnown,
                InstallPhase.Cancelling => UpdateNoticeState.Cancelling,
                InstallPhase.Replacing => UpdateNoticeState.Replacing,
                InstallPhase.Installed => UpdateNoticeState.Installed,
                InstallPhase.CouldNotRestart => UpdateNoticeState.InstalledCouldNotRestart,
                InstallPhase.Cancelled => UpdateNoticeState.Cancelled,
                InstallPhase.Failed => UpdateNoticeState.Failed,
                InstallPhase.None => WaitingState(),
                _ => throw new ArgumentOutOfRangeException(nameof(Phase))
            };
        }
    }

    public bool IsNoticeVisible
    {
        get { return NoticeState != UpdateNoticeState.None; }
    }

    /// <summary>
    /// The title of the notice, in the words of its table.
    /// </summary>
    public string Title
    {
        get
        {
            var version = AvailableRelease?.Version;
            return NoticeState switch
            {
                UpdateNoticeState.Available or UpdateNoticeState.CannotUpdate => $"CommitFence {version} is available.",
                UpdateNoticeState.Downloading or UpdateNoticeState.SizeNotKnown => $"Downloading {version}",
                UpdateNoticeState.Cancelling => "Cancelling the update",
                UpdateNoticeState.Replacing => $"Replacing the files with {version}",
                UpdateNoticeState.Installed or UpdateNoticeState.InstalledCouldNotRestart => $"{version} is installed.",
                UpdateNoticeState.Cancelled => "The update was cancelled.",
                UpdateNoticeState.Failed => "The update failed.",
                UpdateNoticeState.None => string.Empty,
                _ => throw new ArgumentOutOfRangeException(nameof(NoticeState))
            };
        }
    }

    /// <summary>
    /// The line under the title.
    /// </summary>
    public string Line
    {
        get
        {
            return NoticeState switch
            {
                UpdateNoticeState.Available => $"You have {CurrentVersion}. Nothing is downloaded until you say so.",
                UpdateNoticeState.CannotUpdate => $"You have {CurrentVersion}. {ProblemText}",
                UpdateNoticeState.Downloading or UpdateNoticeState.SizeNotKnown => "The app restarts when it is in place.",
                UpdateNoticeState.Cancelling => $"You will keep {CurrentVersion}.",
                UpdateNoticeState.Replacing => "Too late to cancel. It takes a moment.",
                UpdateNoticeState.Installed => "Restarting.",
                UpdateNoticeState.InstalledCouldNotRestart => "Close the app and start it again to use it.",
                UpdateNoticeState.Cancelled => $"You still have {CurrentVersion}.",
                UpdateNoticeState.Failed => $"{Failure} You still have {CurrentVersion}.",
                UpdateNoticeState.None => string.Empty,
                _ => throw new ArgumentOutOfRangeException(nameof(NoticeState))
            };
        }
    }

    public UpdateMark Mark
    {
        get
        {
            return NoticeState switch
            {
                UpdateNoticeState.Cancelling or UpdateNoticeState.Replacing => UpdateMark.Ring,
                UpdateNoticeState.Installed or UpdateNoticeState.InstalledCouldNotRestart => UpdateMark.Tick,
                UpdateNoticeState.Failed => UpdateMark.Failure,
                UpdateNoticeState.None or UpdateNoticeState.Available or UpdateNoticeState.CannotUpdate or UpdateNoticeState.Downloading or UpdateNoticeState.SizeNotKnown or UpdateNoticeState.Cancelled => UpdateMark.Logo,
                _ => throw new ArgumentOutOfRangeException(nameof(NoticeState))
            };
        }
    }

    public bool IsLogoMark
    {
        get { return Mark == UpdateMark.Logo; }
    }

    public bool IsBusyMark
    {
        get { return Mark == UpdateMark.Ring; }
    }

    public bool IsTickMark
    {
        get { return Mark == UpdateMark.Tick; }
    }

    public bool IsFailureMark
    {
        get { return Mark == UpdateMark.Failure; }
    }

    /// <summary>
    /// The bar shows while a download runs. There is none while the files are replaced: the ring says it runs.
    /// </summary>
    public bool ShowsBar
    {
        get { return NoticeState is UpdateNoticeState.Downloading or UpdateNoticeState.SizeNotKnown; }
    }

    /// <summary>
    /// The percent beside the bar, when the size is known.
    /// </summary>
    public bool ShowsPercent
    {
        get { return NoticeState == UpdateNoticeState.Downloading; }
    }

    public string PercentText
    {
        get { return $"{DownloadPercent}%"; }
    }

    /// <summary>
    /// How much of the bar is filled, 0 to 1, or -1 for a piece that moves when the size is not known.
    /// </summary>
    public double BarFraction
    {
        get { return IsDownloadPercentKnown ? DownloadFraction : -1; }
    }

    /// <summary>
    /// The buttons of the state, in the order the notice draws them.
    /// </summary>
    public IReadOnlyList<UpdateButton> Buttons
    {
        get
        {
            var buttons = new List<UpdateButton>();
            AddIf(buttons, ShowsLater, UpdateButton.Later);
            AddIf(buttons, ShowsWhatsNew, UpdateButton.WhatsNew);
            AddIf(buttons, ShowsCancel, UpdateButton.Cancel);
            AddIf(buttons, ShowsClose, UpdateButton.Close);
            AddIf(buttons, ShowsReleasePage, UpdateButton.OpenReleasePage);
            AddIf(buttons, ShowsTryAgain, UpdateButton.TryAgain);
            AddIf(buttons, ShowsUpdateNow, UpdateButton.UpdateNow);
            return buttons;
        }
    }

    public bool ShowsLater
    {
        get { return NoticeState is UpdateNoticeState.Available or UpdateNoticeState.CannotUpdate; }
    }

    public bool ShowsWhatsNew
    {
        get { return NoticeState == UpdateNoticeState.Available; }
    }

    public bool ShowsCancel
    {
        get { return NoticeState is UpdateNoticeState.Downloading or UpdateNoticeState.SizeNotKnown; }
    }

    public bool ShowsClose
    {
        get { return NoticeState is UpdateNoticeState.InstalledCouldNotRestart or UpdateNoticeState.Cancelled or UpdateNoticeState.Failed; }
    }

    public bool ShowsReleasePage
    {
        get { return NoticeState is UpdateNoticeState.CannotUpdate or UpdateNoticeState.Failed && AvailableRelease?.PageUri != null; }
    }

    public bool ShowsTryAgain
    {
        get { return NoticeState == UpdateNoticeState.Failed; }
    }

    public bool ShowsUpdateNow
    {
        get { return NoticeState == UpdateNoticeState.Available; }
    }

    /// <summary>
    /// The state is the end of an update that was started: "Close" ends it.
    /// </summary>
    public bool IsFinished
    {
        get { return NoticeState is UpdateNoticeState.InstalledCouldNotRestart or UpdateNoticeState.Cancelled or UpdateNoticeState.Failed; }
    }

    /// <summary>
    /// An update is running: nothing sends the notice away.
    /// </summary>
    public bool IsRunning
    {
        get { return IsInstalling; }
    }

    /// <summary>
    /// What the release notes say, laid out line by line. They are shown on Settings, not in the notice.
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
    /// "Update now" is possible when the release waits and the app can install it.
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
    /// Later and Close: not while an update runs.
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
    /// "Try again" after a failure starts the update again.
    /// </summary>
    [RelayCommand]
    private async Task TryAgainAsync()
    {
        await InstallAsync();
    }

    /// <summary>
    /// "What's new": Settings opens at the version, with the notes shown. The notice stays.
    /// </summary>
    [RelayCommand]
    private void WhatsNew()
    {
        WhatsNewRequested?.Invoke();
    }

    /// <summary>
    /// Asks for the latest release. A newer one shows in the notice; a check the user asked for also says, in its status text,
    /// when there is none or when it failed.
    /// </summary>
    public async Task CheckAsync(bool userAsked)
    {
        if(!IsIdle)
            return;

        IsChecking = true;
        CheckProblem = null;
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
            CheckProblem = userAsked ? result.Problem ?? string.Empty : null;
            StatusText = userAsked ? $"Could not check for updates. {result.Problem}" : null;
            return;
        }

        _record.Save(_clock.Now.UtcDateTime);
        HasAnswered = true;
        OnPropertyChanged(nameof(HasAnswered));
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
        Phase = InstallPhase.Downloading;
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
                StopInstalling(InstallPhase.Cancelled);
                return;
            }
            catch(Exception exception)
            {
                Failure = exception.Message;
                StopInstalling(InstallPhase.Failed);
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
        Phase = InstallPhase.Installed;
        if(StartAndExit != null && StartAndExit(_updater.CreateStartInfo(_folderPath)))
            return;

        StopInstalling(InstallPhase.CouldNotRestart);
    }

    /// <summary>
    /// Stops the download. Once the files are being replaced it is too late, and the notice says so: the swap takes a moment and
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
            Phase = cancelled ? InstallPhase.Cancelling : InstallPhase.Replacing;
    }

    /// <summary>
    /// Later sends the notice away, and Close ends a finished state the same way: a newer version stays away until a later one
    /// comes, or the user asks to check. Not while an update runs.
    /// </summary>
    [RelayCommand]
    private void Dismiss()
    {
        if(IsInstalling)
            return;

        _dismissedVersion = AvailableRelease?.Version;
        StatusText = null;
        Phase = InstallPhase.None;
        IsDismissed = true;
    }

    /// <summary>
    /// Esc with the keyboard in the notice: Later or Close, and nothing while an update runs. True when it did something.
    /// </summary>
    public bool EscapePressed()
    {
        if(IsInstalling || !IsNoticeVisible)
            return false;

        Dismiss();
        return true;
    }

    [RelayCommand]
    private async Task OpenReleasePageAsync()
    {
        var page = AvailableRelease?.PageUri;
        if(page != null && OpenUri != null)
            await OpenUri(page);
    }

    partial void OnPhaseChanged(InstallPhase value)
    {
        RaiseNotice();
    }

    partial void OnAvailableReleaseChanged(ReleaseInfo? oldValue, ReleaseInfo? newValue)
    {
        // the end of an update describes that release only: when another is found, the notice offers it instead
        if(!IsInstalling && !Equals(oldValue?.Version, newValue?.Version))
            Phase = InstallPhase.None;

        RaiseNotice();
    }

    partial void OnIsDismissedChanged(bool value)
    {
        RaiseNotice();
    }

    partial void OnDownloadPercentChanged(int value)
    {
        RaiseNotice();
    }

    private UpdateNoticeState WaitingState()
    {
        if(!HasUpdate || IsDismissed)
            return UpdateNoticeState.None;

        return ProblemText != null ? UpdateNoticeState.CannotUpdate : UpdateNoticeState.Available;
    }

    private void RaiseNotice()
    {
        foreach(var name in _noticeProperties)
        {
            OnPropertyChanged(name);
        }
    }

    private static void AddIf(List<UpdateButton> buttons, bool condition, UpdateButton button)
    {
        if(condition)
            buttons.Add(button);
    }

    private void StopInstalling(InstallPhase phase)
    {
        IsInstalling = false;
        IsReplacing = false;
        Phase = phase;
    }

    private void OnInstallProgress(UpdateProgress progress)
    {
        if(progress.Stage == UpdateStage.Replacing)
        {
            IsReplacing = true;
            if(_installCancellation != null && Phase == InstallPhase.Downloading)
                Phase = InstallPhase.Replacing;

            return;
        }

        DownloadPercent = progress.Percent;
    }
}
