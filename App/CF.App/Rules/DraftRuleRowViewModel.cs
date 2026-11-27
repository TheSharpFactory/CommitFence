using CF.App.Rules.Page;

namespace CF.App.Rules;

internal sealed class DraftRuleRowViewModel(RulePageViewModel page) : RuleListItemViewModel
{
    public RulePageViewModel Page { get; } = page;
}
