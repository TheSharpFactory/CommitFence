using CF.Core.Code;

namespace CF.Core.Setup;

/// <summary>The part of a file that helps explain a check.</summary>
/// <param name="Path">The full file path.</param>
/// <param name="Line">The 1-based line in question, when known.</param>
/// <param name="Lines">The numbered lines to show.</param>
public sealed record DoctorExcerpt(string Path, int? Line, IReadOnlyList<FileLine> Lines)
{
    /// <summary>Shows two lines above and three below a location, or the start of the file.</summary>
    public static DoctorExcerpt Around(string path, IReadOnlyList<string> lines, int? line)
    {
        var first = Math.Max(1, line.GetValueOrDefault(1) - 3);
        var last = Math.Min(lines.Count, line.HasValue ? line.Value + 3 : 5);
        var excerpt = new List<FileLine>();
        for(var number = first; number <= last; number++)
            excerpt.Add(new FileLine(number, lines[number - 1]));

        return new DoctorExcerpt(path, line, excerpt);
    }

    /// <summary>Splits text into file lines.</summary>
    public static IReadOnlyList<string> ReadLines(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }
}
