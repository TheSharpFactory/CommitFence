namespace CF.Core.Code;

/// <summary>
/// One place in a source file, with the line and the column counted from 1.
/// </summary>
public sealed class SourcePoint
{
    /// <summary>
    /// Creates a point.
    /// </summary>
    public SourcePoint(int line, int column)
    {
        Line = line;
        Column = column;
    }

    /// <summary>
    /// The line.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// The column.
    /// </summary>
    public int Column { get; }
}