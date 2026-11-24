using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace CF.App.Rules.Page.Code;

/// <summary>
/// Draws the band of <c>diff-del</c> behind every line of an artifact that breaks the rule, across the whole width of the code.
/// </summary>
internal sealed class BreakBandRenderer : IBackgroundRenderer
{
    private readonly VerdictLines _lines;

    public BreakBandRenderer(VerdictLines lines)
    {
        _lines = lines;
    }

    public KnownLayer Layer
    {
        get
        {
            return KnownLayer.Background;
        }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if(!textView.VisualLinesValid)
            return;

        var brush = ThemeBrushes.Find(textView, "diff-del");
        foreach(var line in textView.VisualLines)
        {
            if(!_lines.IsBreaking(line.FirstDocumentLine.LineNumber))
                continue;

            var top = line.VisualTop - textView.VerticalOffset;
            drawingContext.DrawRectangle(brush, null, new Rect(0, top, textView.Bounds.Width, line.Height));
        }
    }
}
