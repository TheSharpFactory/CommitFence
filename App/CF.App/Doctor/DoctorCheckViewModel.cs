using CF.App.Parts;
using CF.App.Review;
using CF.Core.Setup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    private readonly Func<DoctorCheckViewModel, HookAddition, Task>? _addHookLine;

    public DoctorCheckViewModel(DoctorCheckResult result, Action<DoctorAction> open, Func<ITextClipboard?> clipboard,
        Func<DoctorCheckViewModel, HookAddition, Task>? addHookLine = null, string? hookAddedAt = null, SetupNextStep? commitHook = null)
    {
        _result = result;
        _addHookLine = addHookLine;
        var fix = result.Fix;
        Addition = fix?.Addition;
        Lines = LinesOf(fix?.Excerpt, false);
        AfterMoveLines = LinesOf(Addition?.After ?? fix?.AfterMove, true, Addition != null);
        EntryHeading = fix?.EntryHeading?.Parts;
        EntryHint = fix?.EntryHint?.Parts;
        EntryLines = EntryHeading == null ? [] : LinesOfEntry(fix!.Actions[0].Value);
        var actions = fix?.Actions ?? [];
        if(Addition != null)
            actions = actions.OrderBy(action => action.Kind != DoctorActionKind.CopyText).ToList();

        Actions = actions.Select((action, index) =>
        {
            var copiesEntry = HasEntry && action.Kind == DoctorActionKind.CopyText;
            var isPrimary = copiesEntry || (index == 0 && Addition == null && action.Kind != DoctorActionKind.CopyText
                && result.Title != "The pre-push hook runs CommitFence first");
            return new DoctorActionViewModel(action, isPrimary, open, clipboard,
                copyName: copiesEntry ? $"{action.Label}, {EntryLines.Count(line => line.ShowsPlus)} lines for lefthook.yml" : null,
                isQuiet: HasEntry && !copiesEntry || Addition != null && action.Kind == DoctorActionKind.OpenFile);
        }).ToList();
        var justAdded = hookAddedAt != null && result.Outcome == DoctorOutcome.Passes;
        Found = justAdded ? [.. result.Found.Parts, $" CommitFence added it at {hookAddedAt}."] : result.Found.Parts;
        CommitHook = justAdded ? commitHook : null;
    }

    private HookAddition? Addition { get; }

    public bool CanAdd { get { return Addition != null && _addHookLine != null; } }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddLabel), nameof(AddName), nameof(ShowsAddIcon), nameof(ShowsRetryIcon))]
    public partial bool IsAdding { get; set; }

    partial void OnIsAddingChanged(bool value)
    {
        foreach(var action in Actions)
        {
            if(action.Copy is { } copy)
                copy.IsAvailable = !value;
            else if(action.Kind == DoctorActionKind.OpenFile)
                action.IsAvailable = !value;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddLabel), nameof(AddName), nameof(ShowsAddIcon), nameof(ShowsRetryIcon), nameof(AddErrorParts))]
    public partial string? AddError { get; set; }

    [ObservableProperty]
    public partial string? AddErrorDetail { get; set; }

    public IReadOnlyList<DoctorTextPart>? AddErrorParts
    {
        get { return AddError == null || Addition == null ? null : SetupMessage.WithFileName(AddError, Addition.ShownPath); }
    }

    public string AddLabel { get { return IsAdding ? "Adding" : AddError != null ? "Try again" : "Add the line"; } }

    public bool ShowsAddIcon { get { return !IsAdding && AddError == null; } }

    public bool ShowsRetryIcon { get { return !IsAdding && AddError != null; } }

    public string? AddName
    {
        get
        {
            if(IsAdding)
                return "Adding, busy";

            return Addition == null ? null : $"{AddLabel} to {Addition.ShownPath}, as line {Addition.After.Line}";
        }
    }

    public double SignWidth { get { return Addition != null ? 22 : 0; } }

    public double SignGap { get { return Addition != null ? 0 : 14; } }

    public double NumberWidth { get { return Addition != null ? 44 : 30; } }

    public string NowName { get { return $"Now, {Lines.Count} lines"; } }

    public string PreviewName { get { return Addition == null ? PreviewHeading : $"After adding, {Addition.After.Lines.Count} lines, line {Addition.After.Line} added"; } }

    public string PreviewHeading { get { return Addition == null ? DoctorFix.AfterMoveHeading : DoctorFix.AfterAddingHeading; } }

    public IReadOnlyList<DoctorTextPart>? EntryHeading { get; }

    public IReadOnlyList<DoctorTextPart>? EntryHint { get; }

    public IReadOnlyList<DoctorTextPart> EntryLineNote { get; } = ["The ", DoctorTextPart.CodeText("run:"),
        " line is one line in the file, shown broken at ", DoctorTextPart.CodeText(";"), " and ", DoctorTextPart.CodeText("||"),
        " to read. Copy the lefthook entry gives it as one line."];

    public IReadOnlyList<FileLineViewModel> EntryLines { get; }

    public bool HasEntry { get { return EntryHeading != null; } }

    public IReadOnlyList<DoctorTextPart>? HookLineNote
    {
        get
        {
            return Addition == null ? null : [$"Line {Addition.After.Line} is one line in the file, shown broken at ",
                DoctorTextPart.CodeText(";"), " and ", DoctorTextPart.CodeText("||"), " to read. Copy the hook line gives it as one line."];
        }
    }

    public SetupNextStep? CommitHook { get; }

    public bool ShowsCommitHook { get { return ShowsPass && CommitHook != null; } }

    public bool ShowsManualMark { get { return ShowsFailure && HasEntry; } }

    public bool ShowsFailureMark { get { return ShowsFailure && !HasEntry; } }

    public bool IsHookSetup { get { return _result.Title == "The pre-push hook runs CommitFence first" && _result.Fix is { Excerpt.Line: null } or { Excerpt: null }; } }

    public bool ShowsNow { get { return HasAfterMove || IsHookSetup && HasLines; } }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AddAsync()
    {
        if(CanAdd && !IsBusy && !IsAdding)
            await _addHookLine!(this, Addition!);
    }

    /// <summary>
    /// The report is being refreshed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPass), nameof(ShowsFailure), nameof(ShowsNotChecked), nameof(ShowsRing), nameof(ShowsQuietProgress), nameof(ShowsFinishedMark), nameof(ShowsResult))]
    [NotifyPropertyChangedFor(nameof(HasFix), nameof(HasLines), nameof(HasActions), nameof(SpokenName))]
    [NotifyPropertyChangedFor(nameof(ShowsManualMark), nameof(ShowsFailureMark), nameof(ShowsCommitHook))]
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

    public IReadOnlyList<DoctorTextPart> Found { get; }

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

    private static List<FileLineViewModel> LinesOf(DoctorExcerpt? excerpt, bool preview, bool adding = false)
    {
        var lines = new List<FileLineViewModel>();
        if(excerpt == null)
            return lines;

        foreach(var line in excerpt.Lines)
        {
            var marked = excerpt.Marks(line.Number);
            if(marked && adding)
            {
                lines.AddRange(HookDisplayRows.For(line).Select(row => AddedLine(row)));
                continue;
            }

            var model = new FileLineViewModel { IsFinding = marked && !preview, IsMoved = marked && preview };
            model.Take(line, false);

            if(!model.IsComment || line.Text.StartsWith("#!", StringComparison.Ordinal))
                model.Segments = [new CodeSegment(line.Text, null)];
            lines.Add(model);
        }

        return lines;
    }

    private static List<FileLineViewModel> LinesOfEntry(string entry)
    {
        var lines = new List<FileLineViewModel>();
        const string run = "run: '";
        foreach(var text in DoctorExcerpt.ReadLines(entry))
        {
            var line = new CF.Core.Code.FileLine(1, text);
            var trimmed = text.AsSpan().TrimStart(' ');
            if(trimmed.StartsWith(run, StringComparison.Ordinal))
            {
                var prefixLength = text.Length - trimmed.Length + run.Length;
                lines.AddRange(HookDisplayRows.For(line, text[..prefixLength]).Select(row => AddedLine(row, fragment: true)));
            }
            else
            {
                lines.Add(AddedLine(line, fragment: true));
            }
        }

        return lines;
    }

    private static FileLineViewModel AddedLine(CF.Core.Code.FileLine line, bool fragment = false)
    {
        var model = new FileLineViewModel { IsAdded = true, IsContinuation = line.Number == 0 };
        model.Take(fragment ? new CF.Core.Code.FileLine(0, line.Text) : line, false);
        model.Segments = HookSegments(line.Text);
        return model;
    }

    private static List<CodeSegment> HookSegments(string text)
    {
        var segments = new List<CodeSegment>();
        var start = 0;
        while(start < text.Length)
        {
            var quoted = text.IndexOf('"', start);
            var end = quoted < 0 ? -1 : text.IndexOf('"', quoted + 1);
            if(end < 0)
            {
                segments.Add(new CodeSegment(text[start..], null));
                break;
            }

            if(quoted > start)
                segments.Add(new CodeSegment(text[start..quoted], null));

            segments.Add(new CodeSegment(text[quoted..(end + 1)], "code-string"));
            start = end + 1;
        }

        return segments;
    }
}
