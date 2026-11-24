namespace CF.Core.Code;

/// <summary>
/// One line of a text, as the place where it stands: its number (the first line is 1) and where it starts and ends in the
/// text, without its line break.
/// </summary>
public sealed class ExcerptLine
{
    /// <summary>
    /// Creates a line.
    /// </summary>
    public ExcerptLine(int number, int start, int end)
    {
        Number = number;
        Start = start;
        End = end;
    }

    /// <summary>
    /// The number of the line, from 1.
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Where the line starts in the text.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Where the line ends in the text, before its line break.
    /// </summary>
    public int End { get; }
}