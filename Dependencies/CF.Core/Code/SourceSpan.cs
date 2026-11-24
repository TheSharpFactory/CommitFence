namespace CF.Core.Code;

/// <summary>
/// A stretch of a source file, with lines and columns counted from 1.
/// </summary>
public sealed class SourceSpan
{
    /// <summary>
    /// Creates a span.
    /// </summary>
    public SourceSpan(int startLine, int startColumn, int endLine, int endColumn)
    {
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    /// <summary>
    /// The first line.
    /// </summary>
    public int StartLine { get; }

    /// <summary>
    /// The first column.
    /// </summary>
    public int StartColumn { get; }

    /// <summary>
    /// The last line.
    /// </summary>
    public int EndLine { get; }

    /// <summary>
    /// The last column.
    /// </summary>
    public int EndColumn { get; }
}