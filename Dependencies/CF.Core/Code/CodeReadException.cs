namespace CF.Core.Code;

/// <summary>
/// A source file cannot be read into artifacts, for example because it does not parse. The message is plain; the line
/// and the column, counted from 1, say where, when the problem has a place.
/// </summary>
public sealed class CodeReadException : Exception
{
    /// <summary>
    /// A problem at a line and a column of the file.
    /// </summary>
    public CodeReadException(string message, int? line, int? column)
        : base(message)
    {
        Line = line;
        Column = column;
    }

    /// <summary>
    /// The line, counted from 1, or nothing.
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// The column, counted from 1, or nothing.
    /// </summary>
    public int? Column { get; }
}