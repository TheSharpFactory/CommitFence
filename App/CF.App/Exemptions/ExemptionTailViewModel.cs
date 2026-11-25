using CommunityToolkit.Mvvm.Input;

namespace CF.App.Exemptions;

/// <summary>
/// The end of the list: the line that closes the files and, while files are cut by "Show", "And 9 more files with one exemption each."
/// with the small button that shows them.
/// </summary>
internal sealed partial class ExemptionTailViewModel : ExemptionListItem
{
    private readonly Action _show;

    public ExemptionTailViewModel(string text, string buttonText, Action show)
    {
        Text = text;
        ButtonText = buttonText;
        _show = show;
    }

    public string Text { get; }

    public bool HasText
    {
        get { return Text.Length > 0; }
    }

    public string ButtonText { get; }

    [RelayCommand]
    private void Show()
    {
        _show();
    }
}
