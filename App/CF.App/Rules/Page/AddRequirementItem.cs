using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// The add button that ends the require clause. It stays on the last requirement's line (see TokenFlow).
/// </summary>
internal sealed partial class AddRequirementItem : ITokenAdd
{
    private readonly RulePageViewModel _page;

    public AddRequirementItem(RulePageViewModel page)
    {
        _page = page;
    }

    [RelayCommand]
    private void Add()
    {
        _page.AddRequirement();
    }
}
