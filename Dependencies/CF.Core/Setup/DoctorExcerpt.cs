using CF.Core.Code;

namespace CF.Core.Setup;

/// <summary>The part of a file that helps explain a check.</summary>
/// <param name="Path">The full file path.</param>
/// <param name="Line">The 1-based line in question, when known.</param>
/// <param name="Lines">The numbered lines to show.</param>
/// <param name="RelatedLine">A second line in question, when the finding names two.</param>
public sealed record DoctorExcerpt(string Path, int? Line, IReadOnlyList<FileLine> Lines, int? RelatedLine = null)
{
    /// <summary>Whether the finding points to this line.</summary>
    public bool Marks(int number)
    {
        return number == Line || number == RelatedLine;
    }

    /// <summary>Shows the marked lines with three lines of context, or the start of the file.</summary>
    public static DoctorExcerpt Around(string path, IReadOnlyList<string> lines, int? line, int? relatedLine = null)
    {
        var first = 1;
        var last = 5;
        if((line ?? relatedLine) is int anchor)
        {
            var other = relatedLine ?? anchor;
            first = Math.Max(1, Math.Min(anchor, other) - 3);
            last = Math.Max(anchor, other) + 3;
        }

        return new DoctorExcerpt(path, line, NumberedLines(lines, first, Math.Min(lines.Count, last)), relatedLine);
    }

    /// <summary>Shows the opening of a hook as it is and with its review command moved.</summary>
    internal static (DoctorExcerpt Now, DoctorExcerpt? AfterMove) HookMove(string path, IReadOnlyList<string> lines, int cause, int hook)
    {
        var last = Math.Min(lines.Count, hook + 3);
        var current = new DoctorExcerpt(path, hook, NumberedLines(lines, 1, last), cause);
        // Moving under line one would leave the command that caused the finding ahead of the hook.
        if(cause == 1)
            return (current, null);

        var reordered = lines.Take(last).ToList();
        reordered.RemoveAt(hook - 1);
        reordered.Insert(1, lines[hook - 1]);

        return (current, new DoctorExcerpt(path, 2, NumberedLines(reordered, 1, last)));
    }

    private static List<FileLine> NumberedLines(IReadOnlyList<string> lines, int first, int last)
    {
        var excerpt = new List<FileLine>();
        for(var number = first; number <= last; number++)
            excerpt.Add(new FileLine(number, lines[number - 1]));

        return excerpt;
    }

    /// <summary>Splits text into file lines.</summary>
    public static IReadOnlyList<string> ReadLines(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }
}
