namespace CF.App.Review;

/// <summary>
/// How the copy of a file on disk stands to the text the review read: the same, found again on a line of its own, not found, found
/// more than once, gone, or not compared at all because it could not be read.
/// </summary>
internal enum SourceCopyState
{
    Same,
    Found,
    Lost,
    Ambiguous,
    Gone,
    NotCompared
}

/// <summary>
/// What the comparison of the reviewed text with the copy on disk found.
/// </summary>
/// <param name="State">The state of the copy.</param>
/// <param name="Line">The line the code is on now: the finding's own line unless it was found on another. Meaningless when the code is
/// lost: the file then opens at its top.</param>
/// <param name="Disk">The lines of the copy on disk, trimmed, when the code was found on another line of it; nothing otherwise.</param>
/// <param name="Reason">Why the copy was not compared, when it was not; nothing otherwise.</param>
/// <param name="ChangedElsewhere">For a copy that is the same where the finding is: the file differs from the reviewed one, but not in these lines.</param>
internal sealed record SourceCopyAnswer(SourceCopyState State, int Line, IReadOnlyList<string>? Disk = null, string? Reason = null, bool ChangedElsewhere = false);

/// <summary>
/// Compares the text a review read with the file on disk, as the fix does before it writes: when the two differ the finding's
/// line may have moved, so the lines around it are looked for in the copy on disk.
/// </summary>
internal static class SourceCopy
{
    private const int _linesAround = 1;

    public const string ReviewedCopyMissing = "The reviewed copy is not available";

    public static SourceCopyAnswer Compare(string? reviewed, string path, int line)
    {
        string disk;
        try
        {
            disk = File.ReadAllText(path);
        }
        catch(Exception exception) when(exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new SourceCopyAnswer(SourceCopyState.Gone, line);
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException)
        {
            return NotCompared(line, exception.Message);
        }

        if(reviewed == null)
            return NotCompared(line, ReviewedCopyMissing);

        if(string.Equals(disk, reviewed, StringComparison.Ordinal))
            return new SourceCopyAnswer(SourceCopyState.Same, line);

        var onDisk = Lines(disk);
        var found = FindAgain(Lines(reviewed), onDisk, line);
        return found.Kind switch
        {
            MatchKind.None => new SourceCopyAnswer(SourceCopyState.Lost, line),
            MatchKind.Several => new SourceCopyAnswer(SourceCopyState.Ambiguous, line),
            MatchKind.One => AtLine(found.Line, line, onDisk),
            _ => new SourceCopyAnswer(SourceCopyState.Lost, line)
        };
    }

    private static SourceCopyAnswer NotCompared(int line, string reason)
    {
        return new SourceCopyAnswer(SourceCopyState.NotCompared, line, null, reason);
    }

    /// <summary>
    /// A block found on the line the finding is on is the same code, whether or not the file changed elsewhere; on another line it moved.
    /// </summary>
    private static SourceCopyAnswer AtLine(int found, int line, string[] onDisk)
    {
        return found == line ? new SourceCopyAnswer(SourceCopyState.Same, line, ChangedElsewhere: true) : new SourceCopyAnswer(SourceCopyState.Found, found, onDisk);
    }

    private static string[] Lines(string text)
    {
        return text.Split('\n').Select(line => line.Trim()).ToArray();
    }

    private enum MatchKind
    {
        None,
        One,
        Several
    }

    private readonly record struct Match(MatchKind Kind, int Line = 0);

    /// <summary>
    /// The line of the copy on disk where the finding's line and the lines beside it are found together: none, one, or several
    /// (a block that occurs twice does not say which of the two it was).
    /// </summary>
    private static Match FindAgain(string[] reviewed, string[] disk, int line)
    {
        var first = Math.Max(0, line - 1 - _linesAround);
        var last = Math.Min(reviewed.Length - 1, line - 1 + _linesAround);
        var block = reviewed[first..(last + 1)];
        if(block.Length == 0 || block.All(text => text.Length == 0))
            return new Match(MatchKind.None);

        var found = new Match(MatchKind.None);
        for(var start = 0; start + block.Length <= disk.Length; start++)
        {
            if(!block.AsSpan().SequenceEqual(disk.AsSpan(start, block.Length)))
                continue;

            if(found.Kind == MatchKind.One)
                return new Match(MatchKind.Several);

            found = new Match(MatchKind.One, start + (line - 1 - first) + 1);
        }

        return found;
    }
}
