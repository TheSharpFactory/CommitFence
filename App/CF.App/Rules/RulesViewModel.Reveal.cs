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

    private static bool IsRule(RuleListItemViewModel item, string ruleId)
    {
        if(item is TeamRuleRowViewModel team)
            return team.Id == ruleId;

        return item is FileRuleRowViewModel file && file.Id == ruleId;
    }
}
