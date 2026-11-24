using Avalonia.Controls;
using Avalonia.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// The page of a rule that .editorconfig sets. It is one view, bound to whichever file rule is chosen. It has no control
/// that changes the rule; its one button opens the file.
/// </summary>
internal sealed partial class FileRulePageView : UserControl
{
    private const double _contentMost = 900;

    public FileRulePageView()
    {
        InitializeComponent();
        Sheet.SizeChanged += (_, _) => Fit();
    }

    /// <summary>
    /// Tells the page whether the window shows one pane: the page then has a back button and its sides are 24.
    /// </summary>
    public void SetOnePane(bool onePane)
    {
        Classes.Set("onepane", onePane);
    }

    private void Fit()
    {
        // as wide as the page of a team rule: the whole room up to 900
        var room = Sheet.Bounds.Width - Sheet.Padding.Left - Sheet.Padding.Right;
        if(room > 0)
            SheetContent.Width = Math.Min(_contentMost, room);
    }

    /// <summary>
    /// Puts the keyboard on the first stop of the page: the back button in one pane, "Open at line N" otherwise.
    /// </summary>
    public void FocusEntry()
    {
        var back = this.FindControl<Button>("BackButton");
        (back is { IsEffectivelyVisible: true } ? back : OpenAtLine).Focus(NavigationMethod.Tab);
    }
}
