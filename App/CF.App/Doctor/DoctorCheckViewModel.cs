using CF.App.Parts;
using CF.App.Review;
using CF.Core.Setup;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Doctor;

/// <summary>
/// One check of the list: its mark, its title, what was found, and, when it fails, its fix open under it with the lines of the file
/// it is about and its buttons. While the checks run every row keeps its last result and shows the busy ring in place of its mark.
/// </summary>
internal sealed partial class DoctorCheckViewModel : ObservableObject
{
    private readonly DoctorCheckResult _result;

    public DoctorCheckViewModel(DoctorCheckResult result, Action<DoctorAction> open, Func<ITextClipboard?> clipboard)
    {
        _result = result;
        Lines = LinesOf(result.Fix?.Excerpt);
        Actions = (result.Fix?.Actions ?? new List<DoctorAction>()).Select(action => new DoctorActionViewModel(action, open, clipboard)).ToList();
    }

    /// <summary>
    /// The row shows the busy ring in place of its mark.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPass), nameof(ShowsFailure), nameof(ShowsNotChecked), nameof(ShowsRing))]
    public partial bool IsBusy { get; set; }

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
        get { return Outcome == DoctorOutcome.Fails && _result.Fix != null; }
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
        get { return IsBusy; }
    }

    /// <summary>
    /// What a screen reader says for the row: the title, how it ended, and what was found.
    /// </summary>
    public string SpokenName
    {
        get
        {
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

    /// <summary>
    /// Whether this is the same check as another result of the same list: the same group and the same title.
    /// </summary>
    public bool IsSameCheckAs(DoctorCheckViewModel other)
    {
        return Group == other.Group && Title == other.Title;
    }

    private static List<FileLineViewModel> LinesOf(DoctorExcerpt? excerpt)
    {
        var lines = new List<FileLineViewModel>();
        foreach(var line in excerpt?.Lines ?? new List<CF.Core.Code.FileLine>())
        {
            var model = new FileLineViewModel();
            model.Take(line, line.Number == excerpt?.Line);

            // a plain run, so that the line is set in ink and not in the faint colour of a line that is not code
            model.Segments = [new CodeSegment(line.Text, null)];
            lines.Add(model);
        }

        return lines;
    }
}
