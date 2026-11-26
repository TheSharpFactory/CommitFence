using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using CF.App.Rules.Page.Code;

namespace CF.App.Review;

/// <summary>The horizontally scrolling, selectable code column of the diff.</summary>
internal sealed class DiffCode : SelectableTextBlock
{
    public static readonly StyledProperty<IReadOnlyList<FixDiffLine>?> LinesProperty =
        AvaloniaProperty.Register<DiffCode, IReadOnlyList<FixDiffLine>?>(nameof(Lines));

    public static readonly StyledProperty<double> PlaceLabelWidthProperty =
        AvaloniaProperty.Register<DiffCode, double>(nameof(PlaceLabelWidth));

    static DiffCode()
    {
        LinesProperty.Changed.AddClassHandler<DiffCode>((code, _) => code.Fill());
        AffectsMeasure<DiffCode>(PlaceLabelWidthProperty);
    }

    public DiffCode()
    {
        ActualThemeVariantChanged += (_, _) => Fill();
    }

    public IReadOnlyList<FixDiffLine>? Lines
    {
        get { return GetValue(LinesProperty); }
        set { SetValue(LinesProperty, value); }
    }

    public double PlaceLabelWidth
    {
        get { return GetValue(PlaceLabelWidthProperty); }
        set { SetValue(PlaceLabelWidthProperty, value); }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        var lines = Lines;
        var labelWidth = PlaceLabelWidth;
        if(lines == null || labelWidth <= 0)
            return size;

        // A labelled line needs enough extent to scroll its last character clear of the pinned label.
        var width = size.Width;
        var textLines = TextLayout.TextLines;
        for(var index = 0; index < Math.Min(lines.Count, textLines.Count); index++)
        {
            if(lines[index].HasPlaceLabel)
                width = Math.Max(width, textLines[index].WidthIncludingTrailingWhitespace + labelWidth);
        }

        return new Size(width, size.Height);
    }

    private void Fill()
    {
        if(Inlines == null)
            return;

        Inlines.Clear();
        var lines = Lines ?? [];
        for(var index = 0; index < lines.Count; index++)
        {
            if(index > 0)
                Inlines.Add(new LineBreak());

            var line = lines[index];
            var at = 0;
            foreach(var segment in line.Parts)
            {
                var text = segment.Text.AsSpan();
                var markStart = Math.Clamp(line.MarkStart - at, 0, text.Length);
                var markEnd = Math.Clamp(line.MarkStart + line.MarkLength - at, markStart, text.Length);
                AddRun(text[..markStart], segment.BrushName);
                AddRun(text[markStart..markEnd], "ink");
                AddRun(text[markEnd..], segment.BrushName);
                at += text.Length;
            }
        }
    }

    private void AddRun(ReadOnlySpan<char> text, string? brush)
    {
        if(text.IsEmpty)
            return;

        var run = new Run(text.ToString());
        if(brush != null)
            run.Foreground = ThemeBrushes.Find(this, brush);

        Inlines!.Add(run);
    }
}
