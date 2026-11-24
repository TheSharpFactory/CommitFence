using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using CF.App.Rules.Page.Code;

namespace CF.App.Review;

/// <summary>
/// Lets a text block take the coloured runs of a line of code: <c>review:CodeText.Segments</c>. The colours are looked up in the
/// theme in force and again when the theme changes.
/// </summary>
internal static class CodeText
{
    public static readonly AttachedProperty<IReadOnlyList<CodeSegment>?> SegmentsProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, IReadOnlyList<CodeSegment>?>("Segments", typeof(CodeText));

    static CodeText()
    {
        SegmentsProperty.Changed.AddClassHandler<TextBlock>((block, _) => Fill(block));
    }

    public static IReadOnlyList<CodeSegment>? GetSegments(TextBlock block)
    {
        return block.GetValue(SegmentsProperty);
    }

    public static void SetSegments(TextBlock block, IReadOnlyList<CodeSegment>? segments)
    {
        block.SetValue(SegmentsProperty, segments);
    }

    private static void Fill(TextBlock block)
    {
        block.ActualThemeVariantChanged -= OnThemeChanged;
        block.ActualThemeVariantChanged += OnThemeChanged;
        block.Inlines?.Clear();
        var segments = GetSegments(block);
        if(segments == null || block.Inlines == null)
            return;

        foreach(var segment in segments)
        {
            var run = new Run(segment.Text);
            if(segment.BrushName != null)
                run.Foreground = ThemeBrushes.Find(block, segment.BrushName);

            block.Inlines.Add(run);
        }
    }

    private static void OnThemeChanged(object? sender, EventArgs e)
    {
        if(sender is TextBlock block)
            Fill(block);
    }
}
