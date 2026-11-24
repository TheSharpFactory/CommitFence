using Avalonia.Controls;

namespace CF.App.Rules.Page;

/// <summary>
/// One condition of the sentence as the page draws it. It has no code of its own: the keys of its tokens are the page's.
/// </summary>
internal sealed partial class ConditionPiece : UserControl
{
    public ConditionPiece()
    {
        InitializeComponent();
    }
}
