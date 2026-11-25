using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using CF.App.Review;
using CF.App.Rules.Page.Code;

namespace CF.App.Parts;

/// <summary>
/// One line of code or of a settings file in a monospaced font that wraps when it does not fit: its own indent is kept, and its
/// rest continues four characters right of where the line's text starts. Nothing is cut and nothing scrolls sideways. A line may
/// come in coloured runs (see <see cref="CodeSegment"/>).
/// </summary>
internal sealed class HangingText : TextBlock
{
    private const int _fewestColumns = 12;
    private const int _fewestAfterIndent = 4;

    public static readonly StyledProperty<string> LineProperty = AvaloniaProperty.Register<HangingText, string>(nameof(Line), string.Empty);

    public static readonly StyledProperty<IReadOnlyList<CodeSegment>?> SegmentsProperty = AvaloniaProperty.Register<HangingText, IReadOnlyList<CodeSegment>?>(nameof(Segments));

    public static readonly StyledProperty<int> ContinuationIndentProperty = AvaloniaProperty.Register<HangingText, int>(nameof(ContinuationIndent), 4);

    private string? _builtFor;

    static HangingText()
    {
        LineProperty.Changed.AddClassHandler<HangingText>((text, _) => text.Rebuild());
        SegmentsProperty.Changed.AddClassHandler<HangingText>((text, _) => text.Rebuild());
        ContinuationIndentProperty.Changed.AddClassHandler<HangingText>((text, _) => text.Rebuild());
    }

    public HangingText()
    {
        TextWrapping = TextWrapping.NoWrap;
        ActualThemeVariantChanged += (_, _) => Rebuild();
    }

    /// <summary>
    /// The line, when it has one colour. A line in coloured runs gives <see cref="Segments"/> instead.
    /// </summary>
    public string Line
    {
        get
        {
            return GetValue(LineProperty);
        }
        set
        {
            SetValue(LineProperty, value);
        }
    }

    public IReadOnlyList<CodeSegment>? Segments
    {
        get
        {
            return GetValue(SegmentsProperty);
        }
        set
        {
            SetValue(SegmentsProperty, value);
        }
    }

    /// <summary>
    /// How many characters a continued row is set in from the line's own start: 4.
    /// </summary>
    public int ContinuationIndent
    {
        get
        {
            return GetValue(ContinuationIndentProperty);
        }
        set
        {
            SetValue(ContinuationIndentProperty, value);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width - Padding.Left - Padding.Right;
        var columns = double.IsInfinity(width) ? int.MaxValue : Math.Max(_fewestColumns, (int)Math.Floor(width / CharacterWidth()));
        Fill(columns);
        return base.MeasureOverride(availableSize);
    }

    private void Rebuild()
    {
        _builtFor = null;
        InvalidateMeasure();
    }

    private double CharacterWidth()
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var width = new FormattedText("M", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, null).Width;
        return width > 0 ? width : FontSize * 0.6;
    }

    /// <summary>
    /// Sets the runs of the text with a break before each place where the line wraps, followed by the continuation's indent.
    /// </summary>
    private void Fill(int columns)
    {
        var segments = Segments ?? new List<CodeSegment> { new CodeSegment(Line, null) };
        var text = string.Concat(segments.Select(segment => segment.Text));
        var key = $"{columns}|{ActualThemeVariant}|{text}|{ContinuationIndent}|{string.Join('\u0001', segments.Select(segment => segment.BrushName))}";
        if(key == _builtFor || Inlines == null)
            return;

        _builtFor = key;
        Inlines.Clear();
        var indent = text.Length - text.TrimStart(' ').Length;
        var breaks = WrapPoints(text, columns, indent, ContinuationIndent);
        var prefix = new string(' ', indent + ContinuationIndent);
        var position = 0;
        foreach(var segment in segments)
        {
            var start = 0;
            for(var index = 0; index < segment.Text.Length; index++)
            {
                if(!breaks.Contains(position + index))
                    continue;

                AddRun(segment.Text[start..index], segment.BrushName);
                Inlines.Add(new LineBreak());
                Inlines.Add(new Run(prefix));
                start = index;
            }

            AddRun(segment.Text[start..], segment.BrushName);
            position += segment.Text.Length;
        }
    }

    private void AddRun(string text, string? brushName)
    {
        if(text.Length == 0)
            return;

        var run = new Run(text);
        if(brushName != null)
            run.Foreground = ThemeBrushes.Find(this, brushName);

        Inlines!.Add(run);
    }

    /// <summary>
    /// The places in the line before which a row begins (the first row begins at 0 and is not listed): a row ends after a space
    /// when it has one, and anywhere when it has none.
    /// </summary>
    public static HashSet<int> WrapPoints(string text, int columns, int indent, int continuation)
    {
        var points = new HashSet<int>();
        var start = 0;
        var room = columns;
        var later = Math.Max(_fewestAfterIndent, columns - indent - continuation);
        while(text.Length - start > room)
        {
            var end = start + room;
            var space = text.LastIndexOf(' ', end - 1, end - start);
            var floor = start + (start == 0 ? indent : 0);
            var next = space + 1 > floor ? space + 1 : end;
            points.Add(next);
            start = next;
            room = later;
        }

        return points;
    }
}
