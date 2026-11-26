using System.Globalization;
using CF.App.Updates;
using CF.Core.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Settings;

/// <summary>
/// The update status row of Settings: what the updates say now, in the words of the notice for the states they share and in its own
/// words for the checks the person asked for. The row shows an update whether or not the notice was sent away with Later, and the
/// answer of a check that was asked for is shown here only.
/// </summary>
internal sealed partial class UpdateStatusViewModel : ObservableObject
{
    private readonly IUpdateCheckRecord _record;
    private readonly IClock _clock;

    public UpdateStatusViewModel(UpdatesViewModel updates, IUpdateCheckRecord record, IClock clock)
    {
        Updates = updates;
        _record = record;
        _clock = clock;
        Updates.PropertyChanged += (_, _) => OnPropertyChanged(string.Empty);
    }

    public UpdatesViewModel Updates { get; }

    public UpdateStatusKind Kind
    {
        get
        {
            var running = RunningKind();
            if(running != null)
                return running.Value;

            if(Updates.IsChecking)
                return UpdateStatusKind.Checking;

            if(Updates.HasUpdate)
                return Updates.ProblemText != null ? UpdateStatusKind.CannotUpdate : UpdateStatusKind.Available;

            if(Updates.CheckProblem != null)
                return UpdateStatusKind.CouldNotCheck;

            if(Updates.HasAnswered)
                return UpdateStatusKind.UpToDate;

            return _record.LastCheckUtc == null ? UpdateStatusKind.NeverChecked : UpdateStatusKind.LastChecked;
        }
    }

    /// <summary>
    /// The first words of the row, in weight 600; empty when the state has none.
    /// </summary>
    public string Lead
    {
        get
        {
            var version = Updates.AvailableRelease?.Version;
            return Kind switch
            {
                UpdateStatusKind.Available or UpdateStatusKind.CannotUpdate => $"{version} is available.",
                UpdateStatusKind.Downloading or UpdateStatusKind.SizeNotKnown => $"Downloading {version}",
                UpdateStatusKind.Replacing => $"Replacing the files with {version}",
                UpdateStatusKind.Installed or UpdateStatusKind.InstalledCouldNotRestart => $"{version} is installed.",
                UpdateStatusKind.Cancelled => "The update was cancelled.",
                UpdateStatusKind.Failed => "The update failed.",
                UpdateStatusKind.UpToDate or UpdateStatusKind.NeverChecked or UpdateStatusKind.LastChecked or UpdateStatusKind.Checking or UpdateStatusKind.CouldNotCheck or UpdateStatusKind.Cancelling => string.Empty,
                _ => throw new ArgumentOutOfRangeException(nameof(Kind))
            };
        }
    }

    /// <summary>
    /// The rest of the line, in weight 400.
    /// </summary>
    public string Rest
    {
        get
        {
            return Kind switch
            {
                UpdateStatusKind.UpToDate => $"Up to date. Checked {WhenText}.",
                UpdateStatusKind.LastChecked => $"Last checked {WhenText}.",
                UpdateStatusKind.NeverChecked => "Not checked yet.",
                UpdateStatusKind.Checking => "Checking for updates",
                UpdateStatusKind.CouldNotCheck => "Could not check for updates.",
                UpdateStatusKind.Available or UpdateStatusKind.CannotUpdate => $"You have {Updates.CurrentVersion}.",
                UpdateStatusKind.Cancelling => "Cancelling the update",
                UpdateStatusKind.Replacing => "Too late to cancel.",
                UpdateStatusKind.Installed => "Restarting.",
                UpdateStatusKind.InstalledCouldNotRestart => "Close the app and start it again to use it.",
                UpdateStatusKind.Cancelled => $"You still have {Updates.CurrentVersion}.",
                UpdateStatusKind.Failed => Updates.Failure,
                UpdateStatusKind.Downloading or UpdateStatusKind.SizeNotKnown => string.Empty,
                _ => throw new ArgumentOutOfRangeException(nameof(Kind))
            };
        }
    }

    /// <summary>
    /// The line under the first, in the quiet type: the reason of a check that failed (also while an update is known), why this copy
    /// cannot update itself, or what is left after a failure.
    /// </summary>
    public string Meta
    {
        get
        {
            return Kind switch
            {
                UpdateStatusKind.CouldNotCheck => Updates.CheckProblem ?? string.Empty,
                UpdateStatusKind.CannotUpdate => string.Join('\n', new[] { Updates.ProblemText, FailedCheckText }.Where(line => !string.IsNullOrEmpty(line))),
                UpdateStatusKind.Available => FailedCheckText ?? string.Empty,
                UpdateStatusKind.Failed => $"You still have {Updates.CurrentVersion}. Try again, or download it from the release page.",
                UpdateStatusKind.UpToDate or UpdateStatusKind.NeverChecked or UpdateStatusKind.LastChecked or UpdateStatusKind.Checking or UpdateStatusKind.Downloading
                    or UpdateStatusKind.SizeNotKnown or UpdateStatusKind.Cancelling or UpdateStatusKind.Replacing or UpdateStatusKind.Installed
                    or UpdateStatusKind.InstalledCouldNotRestart or UpdateStatusKind.Cancelled => string.Empty,
                _ => throw new ArgumentOutOfRangeException(nameof(Kind))
            };
        }
    }

    /// <summary>
    /// The rest of the line as it follows the first words: a space between them when there are first words.
    /// </summary>
    public string RestText
    {
        get { return HasLead ? " " + Rest : Rest; }
    }

    public bool HasLead
    {
        get { return Lead.Length > 0; }
    }

    public bool HasMeta
    {
        get { return Meta.Length > 0; }
    }

    /// <summary>
    /// The row reads as one text for a screen reader, which hears it whenever it changes.
    /// </summary>
    public string SpokenText
    {
        get { return string.Join(' ', new[] { Lead, Rest, PercentText, Meta }.Where(part => part.Length > 0)); }
    }

    public UpdateStatusMark Mark
    {
        get
        {
            return Kind switch
            {
                UpdateStatusKind.UpToDate or UpdateStatusKind.Installed or UpdateStatusKind.InstalledCouldNotRestart => UpdateStatusMark.Tick,
                UpdateStatusKind.Checking or UpdateStatusKind.Cancelling or UpdateStatusKind.Replacing => UpdateStatusMark.Ring,
                UpdateStatusKind.Failed => UpdateStatusMark.Failure,
                UpdateStatusKind.Available or UpdateStatusKind.CannotUpdate => UpdateStatusMark.Download,
                UpdateStatusKind.NeverChecked or UpdateStatusKind.LastChecked or UpdateStatusKind.CouldNotCheck or UpdateStatusKind.Downloading
                    or UpdateStatusKind.SizeNotKnown or UpdateStatusKind.Cancelled => UpdateStatusMark.None,
                _ => throw new ArgumentOutOfRangeException(nameof(Kind))
            };
        }
    }

    public bool ShowsMark
    {
        get { return Mark != UpdateStatusMark.None; }
    }

    public bool ShowsTick
    {
        get { return Mark == UpdateStatusMark.Tick; }
    }

    public bool ShowsDownloadMark
    {
        get { return Mark == UpdateStatusMark.Download; }
    }

    public bool ShowsRing
    {
        get { return Mark == UpdateStatusMark.Ring; }
    }

    public bool ShowsFailureBadge
    {
        get { return Mark == UpdateStatusMark.Failure; }
    }

    public bool ShowsBar
    {
        get { return Kind is UpdateStatusKind.Downloading or UpdateStatusKind.SizeNotKnown; }
    }

    public bool ShowsPercent
    {
        get { return Kind == UpdateStatusKind.Downloading; }
    }

    public string PercentText
    {
        get { return ShowsPercent ? Updates.PercentText : string.Empty; }
    }

    public double BarFraction
    {
        get { return Updates.BarFraction; }
    }

    /// <summary>
    /// The row is taller and its items stand at the top: a state with a second line.
    /// </summary>
    public bool IsTall
    {
        get { return HasMeta; }
    }

    /// <summary>
    /// The buttons stand on a line of their own, at the right: a failure has two and a reason that is long.
    /// </summary>
    public bool ButtonsOnOwnLine
    {
        get { return Kind == UpdateStatusKind.Failed; }
    }

    /// <summary>
    /// Where the buttons stand in the row: at its right end, or on a line of their own under the words.
    /// </summary>
    public int ButtonsRow
    {
        get { return ButtonsOnOwnLine ? 2 : 0; }
    }

    public int ButtonsColumn
    {
        get { return ButtonsOnOwnLine ? 0 : 2; }
    }

    public int ButtonsColumnSpan
    {
        get { return ButtonsOnOwnLine ? 3 : 1; }
    }

    /// <summary>
    /// The buttons of the state, in the order the row draws them.
    /// </summary>
    public IReadOnlyList<UpdateStatusButton> Buttons
    {
        get
        {
            var page = Updates.AvailableRelease?.PageUri != null;
            return Kind switch
            {
                UpdateStatusKind.UpToDate or UpdateStatusKind.NeverChecked or UpdateStatusKind.LastChecked or UpdateStatusKind.Cancelled => [UpdateStatusButton.CheckNow],
                UpdateStatusKind.CouldNotCheck => [UpdateStatusButton.TryCheckAgain],
                UpdateStatusKind.Available => [UpdateStatusButton.UpdateNow],
                UpdateStatusKind.CannotUpdate => page ? [UpdateStatusButton.OpenReleasePage] : [],
                UpdateStatusKind.Downloading or UpdateStatusKind.SizeNotKnown => [UpdateStatusButton.Cancel],
                UpdateStatusKind.Failed => page ? [UpdateStatusButton.OpenReleasePage, UpdateStatusButton.TryUpdateAgain] : [UpdateStatusButton.TryUpdateAgain],
                UpdateStatusKind.Checking or UpdateStatusKind.Cancelling or UpdateStatusKind.Replacing or UpdateStatusKind.Installed or UpdateStatusKind.InstalledCouldNotRestart => [],
                _ => throw new ArgumentOutOfRangeException(nameof(Kind))
            };
        }
    }

    public bool ShowsCheckNow
    {
        get { return Buttons.Contains(UpdateStatusButton.CheckNow); }
    }

    public bool ShowsTryCheckAgain
    {
        get { return Buttons.Contains(UpdateStatusButton.TryCheckAgain); }
    }

    public bool ShowsUpdateNow
    {
        get { return Buttons.Contains(UpdateStatusButton.UpdateNow); }
    }

    public bool ShowsCancel
    {
        get { return Buttons.Contains(UpdateStatusButton.Cancel); }
    }

    public bool ShowsReleasePage
    {
        get { return Buttons.Contains(UpdateStatusButton.OpenReleasePage); }
    }

    public bool ShowsTryUpdateAgain
    {
        get { return Buttons.Contains(UpdateStatusButton.TryUpdateAgain); }
    }

    /// <summary>
    /// In one pane the status leads the page while an update waits, runs or failed: every state but "Up to date", "Last checked",
    /// "Not checked yet" and the checks.
    /// </summary>
    public bool LeadsOnePane
    {
        get { return Kind is not (UpdateStatusKind.UpToDate or UpdateStatusKind.NeverChecked or UpdateStatusKind.LastChecked or UpdateStatusKind.Checking); }
    }

    /// <summary>
    /// "Check now" and F5: asks for the latest release. The end of a cancelled update is closed first, since the answer of a check
    /// replaces it.
    /// </summary>
    [RelayCommand]
    private async Task CheckNowAsync()
    {
        if(Kind == UpdateStatusKind.Cancelled)
            Updates.DismissCommand.Execute(null);

        await Updates.CheckAsync(true);
    }

    private UpdateStatusKind? RunningKind()
    {
        return Updates.NoticeState switch
        {
            UpdateNoticeState.Downloading => UpdateStatusKind.Downloading,
            UpdateNoticeState.SizeNotKnown => UpdateStatusKind.SizeNotKnown,
            UpdateNoticeState.Cancelling => UpdateStatusKind.Cancelling,
            UpdateNoticeState.Replacing => UpdateStatusKind.Replacing,
            UpdateNoticeState.Installed => UpdateStatusKind.Installed,
            UpdateNoticeState.InstalledCouldNotRestart => UpdateStatusKind.InstalledCouldNotRestart,
            UpdateNoticeState.Cancelled => UpdateStatusKind.Cancelled,
            UpdateNoticeState.Failed => UpdateStatusKind.Failed,
            UpdateNoticeState.None or UpdateNoticeState.Available or UpdateNoticeState.CannotUpdate => null,
            _ => throw new ArgumentOutOfRangeException(nameof(UpdatesViewModel.NoticeState))
        };
    }

    /// <summary>
    /// "today, 09:14" or "21 Nov, 16:10", in the local time of the last check that worked.
    /// </summary>
    private string WhenText
    {
        get
        {
            if(_record.LastCheckUtc is not { } last)
                return string.Empty;

            var at = DateTime.SpecifyKind(last, DateTimeKind.Utc).ToLocalTime();
            var day = at.Date == _clock.Now.ToLocalTime().Date ? "today" : at.ToString("d MMM", CultureInfo.InvariantCulture);
            return $"{day}, {at.ToString("HH:mm", CultureInfo.InvariantCulture)}";
        }
    }

    /// <summary>
    /// The check that was asked for and failed, said under the words of an update that is known; null when no such check failed.
    /// </summary>
    private string? FailedCheckText
    {
        get { return Updates.CheckProblem == null ? null : $"Could not check for updates. {Updates.CheckProblem}".TrimEnd(); }
    }
}
