namespace CF.App.Rules.Page;

/// <summary>
/// Where a refusal is said. The head says it in a few words ("Cannot save: the lines requirement cannot be met."); the whole
/// reason stands in red under the clause it is about, or in the picker that is open when the refusal comes from what is typed there.
/// </summary>
internal sealed partial class RulePageViewModel
{
    /// <summary>
    /// The refusal in a few words, for the line under the title. Empty while Save can work.
    /// </summary>
    public string SaveSummary
    {
        get
        {
            if(SaveBlocker == null)
                return string.Empty;

            return $"Cannot save: {SaveSummaryClause()}.";
        }
    }

    /// <summary>
    /// The whole reason of a path that the settings file holds and the picker would refuse: under the within clause.
    /// </summary>
    public string? WithinReason
    {
        get { return ReasonOf("Within"); }
    }

    /// <summary>
    /// The whole reason of a refused condition: under the where clause.
    /// </summary>
    public string? WhereReason
    {
        get { return ReasonOf("Where"); }
    }

    /// <summary>
    /// The whole reason of a refused requirement: under the require clause.
    /// </summary>
    public string? RequireReason
    {
        get { return ReasonOf("Require"); }
    }

    public bool HasWithinReason
    {
        get { return WithinReason != null; }
    }

    public bool HasWhereReason
    {
        get { return WhereReason != null; }
    }

    public bool HasRequireReason
    {
        get { return RequireReason != null; }
    }

    /// <summary>
    /// The whole reason of a clause, in the picker instead of under the clause while an open picker shows that same reason.
    /// </summary>
    private string? ReasonOf(string clause)
    {
        return PickerShowsReasonOf(clause) ? null : ReasonRaw(clause);
    }

    private string? ReasonRaw(string clause)
    {
        return InvalidReason != null && InvalidClauseWord == clause ? InvalidReason : null;
    }

    /// <summary>
    /// What a refused condition says in its own picker.
    /// </summary>
    public string? ReasonForCondition(ConditionViewModel condition)
    {
        return condition.IsConflicted || condition.IsInvalid ? ReasonRaw(condition.IsWhereClause ? "Where" : "Require") : null;
    }

    /// <summary>
    /// Whether an open picker of the clause carries the clause's reason: the path's picker while it holds the pattern the page refuses
    /// (what it shows is the reason of what is typed), or the comparison's picker of a refused condition. The property picker shows no
    /// reason, so opening it leaves the reason under its clause.
    /// </summary>
    private bool PickerShowsReasonOf(string clause)
    {
        if(clause == "Within")
            return Paths.Any(path => path.IsOpen && path.IsInvalid && path.Problem == path.InvalidText);

        var conditions = clause == "Where" ? WhereFlow.OfType<ConditionViewModel>() : RequireFlow;
        return conditions.Any(condition => (condition.IsConflicted || condition.IsInvalid) && condition.ComparisonPicker.IsOpen);
    }

    private void RefreshReasons()
    {
        OnPropertyChanged(nameof(SaveSummary));
        OnPropertyChanged(nameof(WithinReason));
        OnPropertyChanged(nameof(WhereReason));
        OnPropertyChanged(nameof(RequireReason));
        OnPropertyChanged(nameof(HasWithinReason));
        OnPropertyChanged(nameof(HasWhereReason));
        OnPropertyChanged(nameof(HasRequireReason));
        foreach(var condition in WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow))
            condition.ComparisonPicker.ReasonChanged();
    }

    private string SaveSummaryClause()
    {
        if(Paths.Any(candidate => candidate.HasInvalidDraft))
            return "the path in within is not valid";

        var range = PendingRanges().FirstOrDefault(candidate => candidate.ComparisonPicker.PendingProblem != null);
        if(range != null)
            return $"the range of {range.PropertyWords} is not valid";

        if(InvalidReason != null)
            return InvalidSummaryClause();

        if(Title.Trim().Length == 0)
            return "the rule needs a title";

        return IsComplete ? "commitfence.json is being written" : "the sentence is not finished";
    }

    private string InvalidSummaryClause()
    {
        if(Paths.Any(candidate => candidate.IsInvalid))
            return "the path in within is not valid";

        var conditions = WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow).ToList();
        var missing = conditions.FirstOrDefault(candidate => candidate.IsInvalid);
        if(missing != null)
            return $"the property {missing.PropertyWords} is not valid";

        var conflicted = conditions.FirstOrDefault(candidate => candidate.IsConflicted);
        if(conflicted != null)
            return $"the {conflicted.PropertyWords} {(conflicted.IsWhereClause ? "condition" : "requirement")} cannot be met";

        return $"the {(InvalidClauseWord == "Where" ? "where" : "require")} clause cannot be saved";
    }
}
