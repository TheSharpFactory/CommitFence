namespace CF.Core.Code;

/// <summary>
/// Cuts the lines around one line out of a text: the way both faces show the code of a finding.
/// </summary>
public static class Excerpt
{
    /// <summary>
    /// The line and up to this many lines before it and after it, in order. Nothing when the text has no such line.
    /// </summary>
    public static IReadOnlyList<ExcerptLine> Around(string content, int line, int linesAround)
    {
        return Around(content, LineStarts(content), line, linesAround);
    }

    /// <summary>
    /// The same cut, from the starts of the lines the text was split into once: many cuts of one text share one split.
    /// </summary>
    public static IReadOnlyList<ExcerptLine> Around(string content, IReadOnlyList<int> starts, int line, int linesAround)
    {
        if(line < 1 || line > starts.Count)
            return new List<ExcerptLine>();

        var first = Math.Max(1, line - linesAround);
        var last = Math.Min(starts.Count, line + linesAround);
        var lines = new List<ExcerptLine>();
        for(var number = first; number <= last; number++)
        {
            var start = starts[number - 1];
            var end = number < starts.Count ? starts[number] : content.Length;
            lines.Add(new ExcerptLine(number, start, TrimEnd(content, start, end)));
        }

        return lines;
    }

    /// <summary>
    /// Where each line of the text starts, in order; a text without a line break has one line.
    /// </summary>
    public static IReadOnlyList<int> LineStarts(string content)
    {
        var starts = new List<int> { 0 };
        for(var index = 0; index < content.Length; index++)
        {
            if(content[index] == '\n')
                starts.Add(index + 1);
        }

        return starts;
    }

    private static int TrimEnd(string content, int start, int end)
    {
        while(end > start && (content[end - 1] == '\n' || content[end - 1] == '\r'))
            end--;

        return end;
    }
}