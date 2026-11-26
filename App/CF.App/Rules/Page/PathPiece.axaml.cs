using Avalonia.Controls;

namespace CF.App.Rules.Page;

/// <summary>
/// One path of the within clause as the page draws it. Its code offers the whole pattern in a tooltip when the token shows less of it.
/// </summary>
internal sealed partial class PathPiece : UserControl
{
    private const string _plainTip = "Files the rule applies to";

    public PathPiece()
    {
        InitializeComponent();
        Value.ShortenedChanged += ShowWholePattern;
        DataContextChanged += (_, _) => ShowWholePattern();
    }

    private void ShowWholePattern()
    {
        ToolTip.SetTip(PathButton, Value.IsShortened ? Value.WholeText : _plainTip);
    }
}
