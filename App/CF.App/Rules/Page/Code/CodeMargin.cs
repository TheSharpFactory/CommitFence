using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace CF.App.Rules.Page.Code;

/// <summary>
/// The column at the left of the code: the line numbers, in <c>ink-3</c>, right-aligned with 14 to their right, and on every line
/// of an artifact that breaks the rule the band of <c>diff-del</c> and a bar 3 wide in <c>block</c> at the left edge.
/// </summary>
internal sealed class CodeMargin : AbstractMargin
{
    private const double _column = 44;
    private const double _numbersRoom = 14;
    private const double _barWidth = 3;

    private readonly VerdictLines _lines;
    private readonly Typeface _typeface;
    private TextView? _watched;

    public CodeMargin(VerdictLines lines, Typeface typeface)
    {
        _lines = lines;
        _typeface = typeface;
        Width = _column;
        PropertyChanged += (_, change) =>
        {
            if(change.Property == TextViewProperty)
                Watch(TextView);
        };
    }

    public override void Render(DrawingContext context)
    {
        var textView = TextView;
        if(textView == null || !textView.VisualLinesValid)
            return;

        var band = ThemeBrushes.Find(this, "diff-del");
        var bar = ThemeBrushes.Find(this, "block");
        var ink = ThemeBrushes.Find(this, "ink-3");
        foreach(var line in textView.VisualLines)
        {
            var number = line.FirstDocumentLine.LineNumber;
            var top = line.VisualTop - textView.VerticalOffset;
            if(_lines.IsBreaking(number))
            {
                context.DrawRectangle(band, null, new Rect(0, top, _column, line.Height));
                context.DrawRectangle(bar, null, new Rect(0, top, _barWidth, line.Height));
            }

            var text = new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _typeface, 13, ink);
            context.DrawText(text, new Point(_column - _numbersRoom - text.Width, top + ((line.Height - text.Height) / 2)));
        }
    }

    private void Watch(TextView? textView)
    {
        if(_watched != null)
            _watched.VisualLinesChanged -= OnVisualLinesChanged;

        _watched = textView;
        if(_watched != null)
            _watched.VisualLinesChanged += OnVisualLinesChanged;

        InvalidateVisual();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }
}
