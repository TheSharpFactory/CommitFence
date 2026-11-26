using Avalonia.Controls;

namespace CF.App.Rules.Page;

/// <summary>
/// One condition of the sentence as the page draws it. Its code offers the whole value in a tooltip when the token shows less of it;
/// the keys of its tokens are the page's.
/// </summary>
internal sealed partial class ConditionPiece : UserControl
{
    private const string _plainTip = "Comparison and value";

    public ConditionPiece()
    {
        InitializeComponent();
        Value.ShortenedChanged += ShowWholeValue;
        DataContextChanged += (_, _) => ShowWholeValue();
    }

    /// <summary>
    /// The tooltip of the comparison is the comparison and the whole value when the token is showing less, so that the whole value is one
    /// step away.
    /// </summary>
    private void ShowWholeValue()
    {
        var whole = DataContext is ConditionViewModel condition && Value.IsShortened ? $"{condition.ComparisonWord} {Value.WholeText}" : _plainTip;
        ToolTip.SetTip(ComparisonButton, whole);
    }
}
