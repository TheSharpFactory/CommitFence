using System.Globalization;
using CF.App.Review;
using CF.App.Shell;
using CF.Core.Ports;
using CF.Core.Services;
using CF.Core.Setup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Doctor;

/// <summary>
/// The Doctor screen: whether this machine reviews the pushes of the repository. The checks run when the screen is first shown, on
/// "Check again" and on F5, off the interface's thread; while they run each row shows its progress. A report that comes back
/// after the screen has moved to another repository, or after a newer check began, is dropped. What the screen shows is what
/// <see cref="IDoctorService"/> returns, in its words.
/// </summary>
internal sealed partial class DoctorViewModel : ObservableObject, IScreenModel
{
    private const double _secondsWithDecimal = 10;

    private readonly IDoctorService _doctor;
    private readonly IClock _clock;
    private OpenedFolder _folder;
    private DoctorReport? _report;

    // which check is the latest: only its report is shown
    private int _run;
    private bool _started;

    public DoctorViewModel(IDoctorService doctor, IClock clock, OpenedFolder folder)
    {
        _doctor = doctor;
        _clock = clock;
        _folder = folder;
    }

    /// <summary>
    /// The window opens a file at a line in the program the system uses for it.
    /// </summary>
    public event Action<string, int?>? OpenFileRequested;

    /// <summary>
    /// The window opens a web page.
    /// </summary>
    public event Action<string>? OpenWebRequested;

    /// <summary>
    /// The checks are about to hide their results: the screen notes where the keyboard is.
    /// </summary>
    public event Action? ChecksStarting;

    /// <summary>
    /// The refreshed rows and "Check again" are available for restoring keyboard focus.
    /// </summary>
    public event Action? ChecksFinished;

    /// <summary>
    /// Where the app puts a text the person asked to copy. The window gives the system's clipboard.
    /// </summary>
    public ITextClipboard? Clipboard { get; set; }

    /// <summary>
    /// The checks run now.
    /// </summary>
    [ObservableProperty]
    public partial bool IsChecking { get; set; }

    public bool IsIdle
    {
        get { return !IsChecking; }
    }

    public string Meta
    {
        get { return $"Whether this machine reviews the pushes of {_folder.Name}"; }
    }

    public IReadOnlyList<DoctorGroupViewModel> Groups { get; private set; } = new List<DoctorGroupViewModel>();

    public IReadOnlyList<MachineRowViewModel> Machine { get; private set; } = new List<MachineRowViewModel>();

    /// <summary>
    /// A report has been read: the panel "This machine" has facts to show.
    /// </summary>
    public bool HasReport
    {
        get { return _report != null; }
    }

    public DoctorVerdictKind Kind
    {
        get
        {
            if(IsChecking || _report == null)
                return DoctorVerdictKind.Checking;

            if(_report.Ready)
                return DoctorVerdictKind.Ready;

            return _report.Checks.Any(check => check.Outcome == DoctorOutcome.Fails) ? DoctorVerdictKind.Fails : DoctorVerdictKind.Unfinished;
        }
    }

    public bool IsReady
    {
        get { return Kind == DoctorVerdictKind.Ready; }
    }

    public bool HasFailures
    {
        get { return Kind == DoctorVerdictKind.Fails; }
    }

    public bool IsUnfinished
    {
        get { return Kind == DoctorVerdictKind.Unfinished; }
    }

    public bool IsRunning
    {
        get { return Kind == DoctorVerdictKind.Checking; }
    }

    public string Heading
    {
        get { return Kind == DoctorVerdictKind.Checking ? "Checking" : _report!.Heading; }
    }

    /// <summary>
    /// The sentence under the heading, in runs: what the verdict means for a push, in the body's words.
    /// </summary>
    public IReadOnlyList<DoctorTextPart> Sentence
    {
        get { return Kind == DoctorVerdictKind.Checking ? new List<DoctorTextPart> { CheckingSentence() } : _report!.Verdict.Parts; }
    }

    /// <summary>
    /// "7 checks": how many checks the last report holds. None before the first report.
    /// </summary>
    public string? ChecksText
    {
        get { return _report == null ? null : CountText(_report.Checks.Count); }
    }

    /// <summary>
    /// The time of the last report, with its duration once checking has finished.
    /// </summary>
    public string? CheckedText
    {
        get
        {
            if(_report == null)
                return null;

            var at = _report.CheckedAt.ToLocalTime();
            var day = at.Date == _clock.Now.ToLocalTime().Date ? "today" : at.ToString("d MMM", CultureInfo.InvariantCulture);
            var when = $"{day}, {at.ToString("HH:mm", CultureInfo.InvariantCulture)}";
            return IsChecking ? $"Last checked {when}" : $"Checked {when}, in {DurationText(_report.Duration)}";
        }
    }

    public bool HasFacts
    {
        get { return _report != null; }
    }

    /// <summary>
    /// Takes the screen to another repository: what was found for the one before is gone, a check still running for it is dropped,
    /// and, if the screen has been shown, the checks run again for the new one.
    /// </summary>
    public async Task ShowFolderAsync(OpenedFolder folder)
    {
        _folder = folder;
        _run++;
        IsChecking = false;
        SetReport(null);
        AgainCommand.NotifyCanExecuteChanged();
        if(_started)
            await CheckAsync();
    }

    /// <summary>
    /// The screen is shown: the first time, the checks run.
    /// </summary>
    public async Task ShownAsync()
    {
        if(_started)
            return;

        await CheckAsync();
    }

    /// <summary>
    /// Runs the checks off the interface's thread and shows their report, unless the screen has moved on while they ran.
    /// </summary>
    public async Task CheckAsync()
    {
        _started = true;
        var run = ++_run;
        var path = _folder.FolderPath;
        ChecksStarting?.Invoke();
        IsChecking = true;
        var rows = Groups.SelectMany(group => group.Checks).ToList();
        MarkRows(rows, true);
        RaiseVerdict();
        var progress = new Progress<int>(active =>
        {
            if(run == _run && IsChecking)
                MarkProgress(rows, active);
        });
        DoctorReport? report = null;
        try
        {
            report = await Task.Run(() => _doctor.Check(path, progress));
            if(run == _run)
                SetReport(report);
        }
        finally
        {
            if(run == _run)
            {
                IsChecking = false;
                if(report == null)
                    MarkRows(rows, false);

                RaiseVerdict();
                ChecksFinished?.Invoke();
            }
        }
    }

    /// <summary>
    /// "Check again" and F5.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Again()
    {
        _ = CheckAsync();
    }

    private void SetReport(DoctorReport? report)
    {
        _report = report;
        Groups = report == null ? new List<DoctorGroupViewModel>() : GroupsOf(report);
        Machine = report == null ? new List<MachineRowViewModel>() : MachineOf(report.Machine);
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// Tells the screen that the verdict, the sentence and the button "Check again" have changed.
    /// </summary>
    private void RaiseVerdict()
    {
        AgainCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(string.Empty);
    }

    private static void MarkRows(IReadOnlyList<DoctorCheckViewModel> rows, bool busy)
    {
        foreach(var check in rows)
        {
            check.Progress = DoctorCheckProgress.Waiting;
            check.IsBusy = busy;
        }
    }

    private static void MarkProgress(List<DoctorCheckViewModel> rows, int active)
    {
        for(var index = 0; index < Math.Min(active, rows.Count); index++)
            rows[index].Progress = index + 1 == active ? DoctorCheckProgress.Checking : DoctorCheckProgress.Finished;
    }

    private List<DoctorGroupViewModel> GroupsOf(DoctorReport report)
    {
        var groups = new List<DoctorGroupViewModel>();
        foreach(var run in RunsOf(report.Checks))
        {
            var rows = run.Select(check => new DoctorCheckViewModel(check, OpenAction, () => Clipboard)).ToList();
            groups.Add(new DoctorGroupViewModel(run[0].Group, rows));
        }

        return groups;
    }

    /// <summary>
    /// The checks cut into runs of one group, in the order the body gave them.
    /// </summary>
    private static List<List<DoctorCheckResult>> RunsOf(IReadOnlyList<DoctorCheckResult> checks)
    {
        var runs = new List<List<DoctorCheckResult>>();
        foreach(var check in checks)
        {
            if(runs.Count == 0 || runs[^1][0].Group != check.Group)
                runs.Add(new List<DoctorCheckResult>());

            runs[^1].Add(check);
        }

        return runs;
    }

    private static List<MachineRowViewModel> MachineOf(DoctorMachineFacts machine)
    {
        return
        [
            new MachineRowViewModel("Program", machine.ProgramPath),
            new MachineRowViewModel("Version", machine.ProgramVersion),
            new MachineRowViewModel("Git", machine.GitVersion),
            new MachineRowViewModel(".NET SDK", machine.SdkVersion),
            new MachineRowViewModel("Data", machine.DataFolder, false)
        ];
    }

    private void OpenAction(DoctorAction action)
    {
        if(action.Kind == DoctorActionKind.OpenFile)
            OpenFileRequested?.Invoke(action.Value, action.Line);
        else if(action.Kind == DoctorActionKind.OpenWeb)
            OpenWebRequested?.Invoke(action.Value);
    }

    private DoctorTextPart CheckingSentence()
    {
        if(_report == null || Groups.Count == 0)
            return "Checking whether this machine is ready for pushes.";

        var groups = string.Join(", ", Groups.Select(group => group.Name).Distinct(StringComparer.Ordinal));
        return $"{CountText(_report.Checks.Count)}: {groups}.";
    }

    private static string CountText(int count)
    {
        return count == 1 ? "1 check" : $"{count} checks";
    }

    private static string DurationText(TimeSpan duration)
    {
        var seconds = duration.TotalSeconds;
        return seconds < _secondsWithDecimal ? seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s" : Math.Round(seconds).ToString("0", CultureInfo.InvariantCulture) + " s";
    }
}
