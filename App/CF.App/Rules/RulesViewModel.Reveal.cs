namespace CF.App.Rules;

internal sealed partial class RulesViewModel
{
    /// <summary>
    /// Shows the rule with this id: every filter and search is cleared, both groups are opened, the rule is chosen and, in one
    /// pane, its page opens. A rule of .editorconfig that is not read yet is not found, and nothing changes beyond the clearing.
    /// </summary>
    public void Reveal(string ruleId)
    {
        SearchText = string.Empty;
        SelectedFilter = Filters[0];
        _customHeading.IsExpanded = true;
        _fileHeading.IsExpanded = true;
        var row = Items.FirstOrDefault(item => IsRule(item, ruleId));
        if(row == null)
            return;

        SelectedItem = row;
        IsPageOpen = true;
    }

    /// <summary>
    /// Opens a team rule that another screen sends here to be narrowed: it is shown as <see cref="Reveal"/> shows it, the strip says where
    /// it came from, and one empty condition is added with its property picker open. Nothing is saved.
    /// </summary>
    public void Narrow(string ruleId, NarrowOrigin origin)
    {
        EndNarrowing();
        Reveal(ruleId);
        if(!_pages.TryGetValue(ruleId, out var page) || !ReferenceEquals(CurrentPage, page))
            return;

        _narrowed = page;
        page.BeginNarrowing(origin);
    }

    private void EndNarrowing()
    {
        if(_narrowed != null)
            _narrowed.Origin = null;

        _narrowed = null;
    }

    private static bool IsRule(RuleListItemViewModel item, string ruleId)
    {
        if(item is TeamRuleRowViewModel team)
            return team.Id == ruleId;

        return item is FileRuleRowViewModel file && file.Id == ruleId;
    }
}
