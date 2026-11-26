using System.Globalization;
using CF.App.Parts;
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
    private readonly ISetupService? _setup;
    private string? _addedHookPath;
    private string? _hookAddedAt;
    private SetupNextStep? _commitHook;
    private OpenedFolder _folder;
    private DoctorReport? _report;

    // which check is the latest: only its report is shown
    private int _run;
    private bool _started;

    public DoctorViewModel(IDoctorService doctor, IClock clock, OpenedFolder folder, ISetupService? setup = null)
    {
        _doctor = doctor;
        _clock = clock;
        _folder = folder;
        _setup = setup;
    }

    /// <summary>
    /// The window opens a file at a line in the program the system uses for it.
    /// </summary>
    public event Action<string, int?>? OpenFileRequested;

    public event Action<string>? OpenFolderRequested;

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

    public event Action? HookAdded;

    public event Action<string>? HookWriteFinished;

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
        get { return !IsChecking && !IsAdding; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(AgainCommand))]
    public partial bool IsAdding { get; set; }

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

            var failed = _report.Checks.Where(check => check.Outcome == DoctorOutcome.Fails).ToList();
            return failed.Count switch
            {
                0 => DoctorVerdictKind.Unfinished,
                1 when failed[0].Fix?.EntryHeading != null => DoctorVerdictKind.LeftToYou,
                _ => DoctorVerdictKind.Fails
            };
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

    public bool IsLeftToYou { get { return Kind == DoctorVerdictKind.LeftToYou; } }

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
        get
        {
            var kind = Kind;
            if(kind == DoctorVerdictKind.Checking)
                return "Checking";

            return kind == DoctorVerdictKind.LeftToYou ? "1 check is left to you" : _report!.Heading;
        }
    }

    /// <summary>
    /// The sentence under the heading, in runs: what the verdict means for a push, in the body's words.
    /// </summary>
    public IReadOnlyList<DoctorTextPart> Sentence
    {
        get
        {
            var kind = Kind;
            if(kind == DoctorVerdictKind.Checking)
                return [CheckingSentence()];

            return kind == DoctorVerdictKind.LeftToYou
                ? ["Until the entry is in lefthook.yml, pushes from this machine are not reviewed."]
                : _report!.Verdict.Parts;
        }
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
        IsAdding = false;
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
        if(!IsIdle)
            return;

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
        if(_addedHookPath != null && report?.Checks.Any(check => check.HookPath == _addedHookPath) != true)
        {
            _addedHookPath = null;
            _hookAddedAt = null;
            _commitHook = null;
        }

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
            var rows = run.Select(check => new DoctorCheckViewModel(check, OpenAction, () => Clipboard,
                _setup == null ? null : AddHookLineAsync, check.HookPath == _addedHookPath ? _hookAddedAt : null, _commitHook)).ToList();
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

    private async Task AddHookLineAsync(DoctorCheckViewModel row, HookAddition addition)
    {
        if(!IsIdle || !Groups.SelectMany(group => group.Checks).Contains(row))
            return;

        var run = _run;
        var path = _folder.FolderPath;
        IsAdding = true;
        row.IsAdding = true;
        try
        {
            var report = await Task.Run(() => _setup!.AddHookLine(path, addition));
            if(run != _run)
                return;

            if(report.Failed)
            {
                var error = string.Join(Environment.NewLine, report.Lines);
                row.AddErrorDetail = report.Lines.Count > 1 ? string.Join(" ", report.Lines.Skip(1)) : null;
                row.AddError = report.Lines[0];
                HookWriteFinished?.Invoke(error);
                return;
            }

            _addedHookPath = addition.Before.Path;
            _hookAddedAt = _clock.Now.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
            var script = addition.ShownPath;
            _commitHook = report.WorktreeRoot is { } root && IsWorktreePath(script)
                ? new SetupNextStep(root, script, "so the team's pushes run CommitFence; each teammate needs CommitFence installed on their machine (Doctor checks it).",
                    "Run CommitFence before each push", () => Clipboard)
                : null;
            HookWriteFinished?.Invoke($"CommitFence added the hook line to {addition.ShownPath} at {_hookAddedAt}.");
            IsAdding = false;
            await CheckAsync();
            if(_run == run + 1)
                HookAdded?.Invoke();
        }
        finally
        {
            row.IsAdding = false;
            if(_run == run)
                IsAdding = false;
        }
    }

    private static bool IsWorktreePath(string script)
    {
        return !Path.IsPathRooted(script) && !script.StartsWith("../", StringComparison.Ordinal)
            && !script.StartsWith(".git/", StringComparison.OrdinalIgnoreCase);
    }

    private void OpenAction(DoctorAction action)
    {
        if(action.Kind == DoctorActionKind.OpenFile)
            OpenFileRequested?.Invoke(action.Value, action.Line);
        else if(action.Kind == DoctorActionKind.OpenFolder)
            OpenFolderRequested?.Invoke(action.Value);
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
