using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CF.App.Rules.Page.Code;

namespace CF.App.Review;

/// <summary>Draws the rounded marks behind the selectable code without intercepting selection.</summary>
internal sealed class DiffMarks : Control
{
    public static readonly StyledProperty<DiffCode?> CodeProperty = AvaloniaProperty.Register<DiffMarks, DiffCode?>(nameof(Code));
    public static readonly StyledProperty<IReadOnlyList<FixDiffLine>?> LinesProperty =
        AvaloniaProperty.Register<DiffMarks, IReadOnlyList<FixDiffLine>?>(nameof(Lines));

    static DiffMarks()
    {
        AffectsRender<DiffMarks>(CodeProperty, LinesProperty);
    }

    public DiffCode? Code
    {
        get { return GetValue(CodeProperty); }
        set { SetValue(CodeProperty, value); }
    }

    public IReadOnlyList<FixDiffLine>? Lines
    {
        get { return GetValue(LinesProperty); }
        set { SetValue(LinesProperty, value); }
    }

    public override void Render(DrawingContext context)
    {
        if(Code == null || Lines == null)
            return;

        var layout = Code.TextLayout;
        for(var index = 0; index < Math.Min(Lines.Count, layout.TextLines.Count); index++)
        {
            var line = Lines[index];
            if(line.MarkLength == 0)
                continue;

            var start = layout.TextLines[index].FirstTextSourceIndex + line.MarkStart;
            var mark = ThemeBrushes.Find(this, line.IsAdded ? "diff-add-mark" : "diff-del-mark");
            Rect? markBounds = null;
            foreach(var bounds in layout.HitTestTextRange(start, line.MarkLength))
                markBounds = markBounds?.Union(bounds) ?? bounds;

            if(markBounds is { } rectangle)
                context.DrawRectangle(mark, null, rectangle.Deflate(new Thickness(0, 1)), 4, 4);
        }
    }
}
