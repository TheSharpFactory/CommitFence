namespace CF.App.Review;

/// <summary>A line in the patch, including its gutter and the changed span in its code.</summary>
internal sealed class FixDiffLine(int? oldLine, int? newLine, IReadOnlyList<CodeSegment> parts)
{
    public int? OldLine { get; } = oldLine;
    public int? NewLine { get; } = newLine;
    public IReadOnlyList<CodeSegment> Parts { get; } = parts;
    public string Text { get; } = string.Concat(parts.Select(part => part.Text));
    public int MarkStart { get; private set; }
    public int MarkLength { get; private set; }
    public string PlaceLabel { get; set; } = string.Empty;

    public bool HasPlaceLabel
    {
        get { return PlaceLabel.Length > 0; }
    }

    public bool IsAdded
    {
        get { return OldLine == null; }
    }

    public bool IsRemoved
    {
        get { return NewLine == null; }
    }

    public void Mark(int start, int length)
    {
        MarkStart = start;
        MarkLength = length;
    }
}
