namespace CF.App.Rules;

/// <summary>
/// A line of words under a group heading where its rows would be: no rule yet, or none that the filter lets through.
/// </summary>
internal sealed class GroupNoteViewModel : RuleListItemViewModel
{
    public GroupNoteViewModel(string text, bool followsFilter)
    {
        Text = text;
        FollowsFilter = followsFilter;
    }

    public string Text { get; }

    /// <summary>
    /// Whether the line answers the filter (drawn small) rather than says there are no rules at all.
    /// </summary>
    public bool FollowsFilter { get; }

    public bool IsPlain
    {
        get
        {
            return !FollowsFilter;
        }
    }
}
