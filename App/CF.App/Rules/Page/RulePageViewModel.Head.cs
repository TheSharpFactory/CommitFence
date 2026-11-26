using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// What the head of the page says in each of its three states. The head is one row in all of them: the state word, Discard, Save
/// rule and the menu are always there, and the line under the title is never empty.
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

            return SaveBlocker == null ? SaveStateKind.Unsaved : SaveStateKind.CannotSave;
        }
    }

    /// <summary>
    /// What prevents saving now, in the words the line under the title shows: the field and the reason. Nothing exactly when Save can
    /// work. The state word, the reason and Save all read it, so they never disagree.
    /// </summary>
    public string? SaveBlocker
    {
        get
        {
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
            return condition ?? conditions.FirstOrDefault(candidate => !candidate.IsComplete);
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
        get { return IsUnsavedState ? "Unsaved changes" : SaveStateWord; }
    }

    /// <summary>
    /// The line under the title: when the rule was saved, what changed, or why it cannot be saved. It is one line and never empty.
    /// </summary>
    public string HeadLine
    {
        get
        {
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

    public bool HeadLineIsBad
    {
        get { return IsCannotSaveState; }
    }

    /// <summary>
    /// The whole reason, for Save's description and its tip; empty while Save works.
    /// </summary>
    public string SaveBlockerText
    {
        get { return IsUnsaved ? SaveBlocker ?? string.Empty : string.Empty; }
    }

    private string UnsavedLine(string? saved)
    {
        if(IsDraft)
            return "Not saved yet. Save writes it to commitfence.json.";

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
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void RefreshHead()
    {
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
        OnPropertyChanged(nameof(SaveBlocker));
        RefreshReasons();
        OnPropertyChanged(nameof(SaveBlockerText));
        OnPropertyChanged(nameof(SaveTip));
        OnPropertyChanged(nameof(CanSave));
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
        SaveCommand.NotifyCanExecuteChanged();
    }
}
