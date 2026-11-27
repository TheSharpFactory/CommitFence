using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// What the head of the page says about saving the rule.
/// </summary>
internal enum SaveStateKind
{
    Saved,
    Unsaved,
    CannotSave
}

internal sealed partial class RulePageViewModel
{
    /// <summary>
    /// Whether a path is being edited in its picker and the page does not hold what is typed yet.
    /// </summary>
    public bool HasOpenDraft
    {
        get { return Paths.Any(path => path.HasOpenDraft); }
    }

    /// <summary>
    /// The conditions whose range picker is open holding ends that the condition does not hold yet.
    /// </summary>
    private List<ConditionViewModel> PendingRanges()
    {
        return WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow).Where(condition => condition.ComparisonPicker.HasPendingChange).ToList();
    }

    /// <summary>
    /// Whether a picker holds an edit the page does not: a path being typed or the ends of a range.
    /// </summary>
    public bool HasPendingEdit
    {
        get { return HasOpenDraft || PendingRanges().Count > 0; }
    }

    /// <summary>
    /// Whether there is anything the file does not hold: edits of the sentence or the title, a rule that is new, or an edit that a
    /// picker still holds.
    /// </summary>
    public bool IsUnsaved
    {
        get { return IsDirty || HasPendingEdit; }
    }

    /// <summary>
    /// Saved when nothing is open; Unsaved while there is a change and Save works; Cannot save whenever anything prevents saving, with
    /// Save off: a path being typed that is refused, a rule with no title, a sentence that is not finished or cannot be met.
    /// </summary>
    public SaveStateKind SaveState
    {
        get
        {
            if(!IsUnsaved)
                return SaveStateKind.Saved;

            return HasLocalRefusal || SaveBlocker == null ? SaveStateKind.Unsaved : SaveStateKind.CannotSave;
        }
    }

    private bool HasLocalRefusal
    {
        get
        {
            return HasIdProblem || Paths.Any(path => path.HasInvalidDraft || path.IsInvalid)
                || WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow).Any(condition => condition.IsConflicted)
                || PendingRanges().Any(range => range.ComparisonPicker.PendingProblem != null);
        }
    }

    public string? IdProblem
    {
        get { return IsDraft ? _changes.NewRuleIdProblem(Id) : null; }
    }

    public bool HasIdProblem
    {
        get { return IdProblem != null; }
    }

    public string IdStatus { get { return HasIdProblem ? "invalid" : string.Empty; } }

    public string IdLine
    {
        get
        {
            return IdProblem ?? "The ID can be changed until the rule is saved.";
        }
    }

    public string? SuggestedId { get { return IsDraft ? _changes.SuggestedRuleId(Id) : null; } }
    public string? IdFixLabel { get { return SuggestedId is { } id ? $"Use {id}" : null; } }
    public string? IdFixDescription { get { return IsDraft ? _changes.TakenRule(Id)?.Title : null; } }
    public string? SaveButtonHelp { get { return SaveBlocker is { } blocker ? $"{blocker} Press Save rule to go there." : null; } }

    public event Action<string>? IdFixed;

    [RelayCommand]
    private void UseSuggestedId()
    {
        if(SuggestedId is not { } id)
            return;

        Id = id;
        IdFixed?.Invoke(id);
    }

    /// <summary>
    /// Why Save is unavailable, or null when the draft can be saved.
    /// </summary>
    public string? SaveBlocker
    {
        get
        {
            if(IdProblem is { } idProblem)
                return idProblem;

            var draft = Paths.FirstOrDefault(candidate => candidate.HasInvalidDraft);
            if(draft != null)
                return $"Path {draft.FieldText.Trim()}: {draft.Problem}";

            var range = PendingRanges().FirstOrDefault(candidate => candidate.ComparisonPicker.PendingProblem != null);
            if(range != null)
                return $"Range of {range.PropertyWords}: {range.ComparisonPicker.PendingProblem}";

            if(InvalidReason != null)
                return InvalidSpokenName;

            if(Title.Trim().Length == 0)
                return "Name the rule to save it.";

            if(!IsComplete)
                return "Finish the sentence to save it.";

            return IsSetupWriting ? "commitfence.json is being written. Save works again in a moment." : null;
        }
    }

    /// <summary>
    /// The part of the page the reason is about, so that the keyboard can go there: the path or the condition at fault, or nothing
    /// for the title and for a sentence that is not finished.
    /// </summary>
    public object? SaveBlockerTarget
    {
        get
        {
            if(HasIdProblem)
                return this;

            var draft = Paths.FirstOrDefault(candidate => candidate.HasInvalidDraft);
            if(draft != null)
                return draft;

            var range = PendingRanges().FirstOrDefault(candidate => candidate.ComparisonPicker.PendingProblem != null);
            if(range != null)
                return range;

            var path = Paths.FirstOrDefault(candidate => candidate.IsInvalid);
            if(path != null)
                return path;

            var conditions = WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow).ToList();
            var condition = conditions.FirstOrDefault(candidate => candidate.IsInvalid) ?? conditions.FirstOrDefault(candidate => candidate.IsConflicted);
            return condition ?? (HasTitle ? conditions.FirstOrDefault(candidate => !candidate.IsComplete) : null);
        }
    }

    /// <summary>
    /// Raised when the reason in the line under the title is followed: the keyboard goes to the part of the page it is about.
    /// </summary>
    public event Action<object?>? BlockerFocusRequested;

    [RelayCommand]
    private void GoToBlocker()
    {
        BlockerFocusRequested?.Invoke(SaveBlockerTarget);
    }

    public bool IsSavedState
    {
        get { return SaveState == SaveStateKind.Saved; }
    }

    public bool IsUnsavedState
    {
        get { return SaveState == SaveStateKind.Unsaved; }
    }

    public bool IsCannotSaveState
    {
        get { return SaveState == SaveStateKind.CannotSave; }
    }

    public string SaveStateWord
    {
        get
        {
            return SaveState switch
            {
                SaveStateKind.Saved => "Saved",
                SaveStateKind.Unsaved => "Unsaved",
                SaveStateKind.CannotSave => "Cannot save",
                _ => throw new InvalidOperationException("Unknown save state.")
            };
        }
    }

    public string SaveStateSpoken
    {
        get { return IsUnsaved ? "Unsaved changes" : "Saved"; }
    }

    /// <summary>
    /// The line under the title: when the rule was saved, what changed, or why it cannot be saved. It is one line and never empty.
    /// </summary>
    public string HeadLine
    {
        get
        {
            if(IsDraft)
                return "Save adds it to commitfence.json. Until then it checks nothing.";

            var saved = _changes.SavedWhen(Id);
            switch(SaveState)
            {
                case SaveStateKind.Saved:
                    return saved == null ? "Kept in commitfence.json." : $"Kept in commitfence.json, saved {saved}.";
                case SaveStateKind.CannotSave:
                    return SaveSummary;
                default:
                    return UnsavedLine(saved);
            }
        }
    }

    public bool HeadLineShowsFile { get { return IsDraft || IsSavedState; } }

    public bool HeadLineShowsPen
    {
        get { return IsUnsavedState && !IsDraft; }
    }

    public bool HeadLineIsBad
    {
        get { return IsCannotSaveState && !IsDraft; }
    }

    /// <summary>
    /// The whole reason, for Save's description and its tip; empty while Save works.
    /// </summary>
    public string SaveBlockerText
    {
        get { return IsUnsaved && !HasLocalRefusal ? SaveBlocker ?? string.Empty : string.Empty; }
    }

    private static string UnsavedLine(string? saved)
    {
        return saved == null
            ? "Changed since it was saved. Save writes it to commitfence.json."
            : $"Changed since it was saved {saved}. Save writes it to commitfence.json.";
    }

    /// <summary>
    /// A picker of a path was opened, typed in or closed: the head says "Unsaved" as soon as there is a draft.
    /// </summary>
    public void DraftChanged()
    {
        RefreshHead();
    }

    private void RefreshHead()
    {
        OnPropertyChanged(nameof(IdProblem));
        OnPropertyChanged(nameof(HasIdProblem));
        OnPropertyChanged(nameof(IdLine));
        OnPropertyChanged(nameof(IdFixLabel));
        OnPropertyChanged(nameof(IdFixDescription));
        OnPropertyChanged(nameof(SaveButtonHelp));
        OnPropertyChanged(nameof(IdStatus));
        OnPropertyChanged(nameof(HasOpenDraft));
        OnPropertyChanged(nameof(HasPendingEdit));
        OnPropertyChanged(nameof(IsUnsaved));
        OnPropertyChanged(nameof(SaveState));
        OnPropertyChanged(nameof(IsSavedState));
        OnPropertyChanged(nameof(IsUnsavedState));
        OnPropertyChanged(nameof(IsCannotSaveState));
        OnPropertyChanged(nameof(SaveStateWord));
        OnPropertyChanged(nameof(SaveStateSpoken));
        OnPropertyChanged(nameof(HeadLine));
        OnPropertyChanged(nameof(HeadLineIsBad));
        OnPropertyChanged(nameof(HeadLineShowsPen));
        OnPropertyChanged(nameof(HeadLineShowsFile));
        OnPropertyChanged(nameof(SaveBlocker));
        RefreshReasons();
        OnPropertyChanged(nameof(SaveBlockerText));
        OnPropertyChanged(nameof(SaveTip));
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Keeps the path drafts and the range edits the pickers hold. False when one is refused: its picker stays open with its reason.
    /// </summary>
    private bool CommitOpenDrafts()
    {
        foreach(var path in Paths.Where(candidate => candidate.HasOpenDraft).ToList())
        {
            if(!path.Commit())
                return false;
        }

        foreach(var range in PendingRanges())
        {
            if(!range.ComparisonPicker.CommitRange())
                return false;
        }

        return true;
    }

    private void CancelOpenPickers()
    {
        foreach(var path in Paths.Where(candidate => candidate.IsOpen).ToList())
            path.Cancel();

        foreach(var range in PendingRanges())
            range.ComparisonPicker.Escape();
    }

    partial void OnIsDirtyChanged(bool value)
    {
        RefreshHead();
    }

    partial void OnIsSetupWritingChanged(bool value)
    {
        RefreshHead();
    }
}
