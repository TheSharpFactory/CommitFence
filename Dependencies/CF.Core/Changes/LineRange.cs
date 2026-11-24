namespace CF.Core.Changes;

/// <summary>
/// Lines of the new version of a file that a change touched. Lines that were only removed leave a point between two
/// lines instead.
/// </summary>
public sealed class LineRange
{
    private LineRange(int startLine, int endLine, bool isRemovalPoint)
    {
        StartLine = startLine;
        EndLine = endLine;
        IsRemovalPoint = isRemovalPoint;
    }

    /// <summary>
    /// The first line, counted from 1. For a removal point, the line the removed lines followed.
    /// </summary>
    public int StartLine { get; }

    /// <summary>
    /// The last line, counted from 1.
    /// </summary>
    public int EndLine { get; }

    /// <summary>
    /// Whether lines were only removed here: the point lies between line StartLine and the line after it.
    /// </summary>
    public bool IsRemovalPoint { get; }

    /// <summary>
    /// Lines added or changed, first to last, both counted.
    /// </summary>
    public static LineRange ForLines(int startLine, int endLine)
    {
        return new LineRange(startLine, endLine, false);
    }

    /// <summary>
    /// Every line of a file: what a whole review touches, and the fallback for a changed file git gives no line numbers
    /// for (it takes the file for binary, or the repository tells it not to compare it) when a second reading does not
    /// give them either, so that more is reviewed, never less.
    /// </summary>
    public static LineRange ForWholeFile()
    {
        return new LineRange(1, int.MaxValue, false);
    }

    /// <summary>
    /// Whether this range stands for every line of the file, whatever its length.
    /// </summary>
    public bool IsWholeFile
    {
        get { return !IsRemovalPoint && StartLine == 1 && EndLine == int.MaxValue; }
    }

    /// <summary>
    /// Lines removed right after the given line; 0 means the start of the file.
    /// </summary>
    public static LineRange ForRemovalAfter(int line)
    {
        return new LineRange(line, line, true);
    }

    /// <summary>
    /// Whether an artifact that spans these lines was touched. A removal touches an artifact only when the artifact
    /// holds the lines on both sides of it.
    /// </summary>
    public bool Touches(int artifactStartLine, int artifactEndLine)
    {
        if(IsRemovalPoint)
            return artifactStartLine <= StartLine && artifactEndLine >= StartLine + 1;

        return artifactStartLine <= EndLine && artifactEndLine >= StartLine;
    }

    /// <summary>
    /// Whether the documentation zone of an artifact was touched.
    /// </summary>
    /// <remarks>
    /// A removal counts at the edges of the zone as well as inside it: deleting the first or the last line of a
    /// comment, or the whole comment (an empty zone, whose last line is before its first), changes what the artifact
    /// says about its documentation.
    /// </remarks>
    public bool TouchesDocumentation(int zoneStartLine, int zoneEndLine)
    {
        if(IsRemovalPoint)
            return StartLine >= zoneStartLine - 1 && StartLine <= zoneEndLine;

        return zoneEndLine >= zoneStartLine && Touches(zoneStartLine, zoneEndLine);
    }
}