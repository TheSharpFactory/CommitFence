using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// The add button that ends the within clause. It says "or" after a path and "add a path" while the rule applies to every file.
/// </summary>
internal sealed partial class AddPathItem : ObservableObject, ITokenAdd
{
    private readonly RulePageViewModel _page;

    [ObservableProperty]
    public partial string Text { get; set; } = "add a path";

    [ObservableProperty]
    public partial string SpokenName { get; set; } = "Files: every file. Add a path";

    public AddPathItem(RulePageViewModel page)
    {
        _page = page;
    }

    [RelayCommand]
    private void Add()
    {
        _page.AddPath();
    }
}
