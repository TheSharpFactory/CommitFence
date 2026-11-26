using CF.App.Parts;
using CF.App.Review;
using CF.Core.Setup;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Doctor;

internal enum DoctorCheckProgress
{
    Waiting,
    Checking,
    Finished
}

/// <summary>
/// One check of the list: its mark, its title, what was found, and, when it fails, its fix open under it with the lines of the file
/// it is about and its buttons. While the checks run, each row shows its progress.
/// </summary>
internal sealed partial class DoctorCheckViewModel : ObservableObject
{
    private readonly DoctorCheckResult _result;

    public DoctorCheckViewModel(DoctorCheckResult result, Action<DoctorAction> open, Func<ITextClipboard?> clipboard)
    {
        _result = result;
        Lines = LinesOf(result.Fix?.Excerpt, false);
        AfterMoveLines = LinesOf(result.Fix?.AfterMove, true);
        Actions = (result.Fix?.Actions ?? new List<DoctorAction>()).Select((action, index) => new DoctorActionViewModel(action, index == 0, open, clipboard)).ToList();
    }

    /// <summary>
    /// The report is being refreshed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPass), nameof(ShowsFailure), nameof(ShowsNotChecked), nameof(ShowsRing), nameof(ShowsQuietProgress), nameof(ShowsFinishedMark), nameof(ShowsResult))]
    [NotifyPropertyChangedFor(nameof(HasFix), nameof(HasLines), nameof(HasActions), nameof(SpokenName))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRing), nameof(ShowsQuietProgress), nameof(ShowsFinishedMark), nameof(ProgressText), nameof(SpokenName))]
    public partial DoctorCheckProgress Progress { get; set; }

    public bool ShowsResult
    {
        get { return !IsBusy; }
    }

    public string ProgressText
    {
        get { return Progress.ToString(); }
    }

    public string Group
    {
        get { return _result.Group; }
    }

    public string Title
    {
        get { return _result.Title; }
    }

    public DoctorOutcome Outcome
    {
        get { return _result.Outcome; }
    }

    public IReadOnlyList<DoctorTextPart> Found
    {
        get { return _result.Found.Parts; }
    }

    public bool HasFix
    {
        get { return ShowsFailure && _result.Fix != null; }
    }

    public IReadOnlyList<DoctorTextPart> FixText
    {
        get { return _result.Fix?.Text.Parts ?? new List<DoctorTextPart>(); }
    }

    /// <summary>
    /// The lines the check is about, the one in question marked.
    /// </summary>
    public IReadOnlyList<FileLineViewModel> Lines { get; }

    public bool HasLines
    {
        get { return HasFix && Lines.Count > 0; }
    }

    public IReadOnlyList<FileLineViewModel> AfterMoveLines { get; }

    public bool HasAfterMove
    {
        get { return AfterMoveLines.Count > 0; }
    }

    public IReadOnlyList<DoctorActionViewModel> Actions { get; }

    public bool HasActions
    {
        get { return HasFix && Actions.Count > 0; }
    }

    public bool ShowsPass
    {
        get { return !IsBusy && Outcome == DoctorOutcome.Passes; }
    }

    public bool ShowsFailure
    {
        get { return !IsBusy && Outcome == DoctorOutcome.Fails; }
    }

    public bool ShowsNotChecked
    {
        get { return !IsBusy && Outcome == DoctorOutcome.NotChecked; }
    }

    public bool ShowsRing
    {
        get { return IsBusy && Progress == DoctorCheckProgress.Checking; }
    }

    public bool ShowsQuietProgress
    {
        get { return IsBusy && Progress == DoctorCheckProgress.Waiting; }
    }

    public bool ShowsFinishedMark
    {
        get { return IsBusy && Progress == DoctorCheckProgress.Finished; }
    }

    /// <summary>
    /// What a screen reader says for the row: the title, how it ended, and what was found.
    /// </summary>
    public string SpokenName
    {
        get
        {
            if(IsBusy)
                return $"{Title}, {ProgressText}.";

            var ended = Outcome switch
            {
                DoctorOutcome.Passes => "passes",
                DoctorOutcome.Fails => "fails",
                DoctorOutcome.NotChecked => "not checked",
                _ => throw new ArgumentOutOfRangeException(nameof(Outcome))
            };
            return $"{Title}, {ended}. {_result.Found}";
        }
    }

    private static List<FileLineViewModel> LinesOf(DoctorExcerpt? excerpt, bool afterMove)
    {
        var lines = new List<FileLineViewModel>();
        if(excerpt == null)
            return lines;

        foreach(var line in excerpt.Lines)
        {
            var marked = excerpt.Marks(line.Number);
            var model = new FileLineViewModel { IsFinding = marked && !afterMove, IsMoved = marked && afterMove };
            model.Take(line, false);

            // a plain run, so that the line is set in ink and not in the faint colour of a line that is not code
            model.Segments = [new CodeSegment(line.Text, null)];
            lines.Add(model);
        }

        return lines;
    }
}
