using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// The add button that ends the where clause. It stays on the last condition's line (see TokenFlow);
/// it says "and" after a condition and "add a condition" when there is none.
/// </summary>
internal sealed partial class AddConditionItem : ObservableObject, ITokenAdd
{
    private readonly RulePageViewModel _page;

    [ObservableProperty]
    public partial string Text { get; set; } = "and";

    public AddConditionItem(RulePageViewModel page)
    {
        _page = page;
    }

    [RelayCommand]
    private void Add()
    {
        _page.AddCondition();
    }
}
