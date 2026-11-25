using Avalonia.Controls;

namespace CF.App.Parts;

/// <summary>
/// The summary of a screen: one wrapping row of pieces at the top of its main column, 20 apart and 8 between lines. A piece is a
/// text block (type-lead in ink-2, its numbers set in weight 600 ink by the screen), a fact, a count piece or a small button, and it
/// never breaks. The look of the text is in <c>Styles/Screens.axaml</c>.
/// </summary>
internal sealed class SummaryRow : WrapPanel
{
    public SummaryRow()
    {
        ItemSpacing = 20;
        LineSpacing = 8;
        Orientation = Avalonia.Layout.Orientation.Horizontal;
    }
}
