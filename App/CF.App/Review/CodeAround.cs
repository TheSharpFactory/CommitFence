using CF.App.Rules.Page.Code;

namespace CF.App.Review;

/// <summary>
/// Cuts the lines around a finding out of the text of the file as the reviewed tree held it, coloured as Try it colours C#.
/// </summary>
internal static class CodeAround
{
    private const int _linesAround = 3;
    private const string _tab = "    ";

    public static IReadOnlyList<CodeLineViewModel> Cut(string content, int line)
    {
        var starts = LineStarts(content);
        if(line < 1 || line > starts.Count)
            return new List<CodeLineViewModel>();

        var tokens = CSharpTokens.Scan(content);
        var first = Math.Max(1, line - _linesAround);
        var last = Math.Min(starts.Count, line + _linesAround);
        var lines = new List<CodeLineViewModel>();
        for(var number = first; number <= last; number++)
        {
            var start = starts[number - 1];
            var end = number < starts.Count ? starts[number] : content.Length;
            lines.Add(new CodeLineViewModel(number, Segments(content, start, TrimEnd(content, start, end), tokens), number == line));
        }

        return lines;
    }

    private static List<int> LineStarts(string content)
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

    private static List<CodeSegment> Segments(string content, int start, int end, IReadOnlyList<CodeToken> tokens)
    {
        var segments = new List<CodeSegment>();
        var at = start;
        foreach(var token in tokens)
        {
            if(token.End <= start || token.Start >= end)
                continue;

            var from = Math.Max(token.Start, start);
            var to = Math.Min(token.End, end);
            if(from > at)
                segments.Add(new CodeSegment(content.Substring(at, from - at).Replace("\t", _tab, StringComparison.Ordinal), null));

            segments.Add(new CodeSegment(content.Substring(from, to - from).Replace("\t", _tab, StringComparison.Ordinal), TokenBrushNames.Of(token.Kind)));
            at = to;
        }

        if(at < end)
            segments.Add(new CodeSegment(content.Substring(at, end - at).Replace("\t", _tab, StringComparison.Ordinal), null));

        return segments;
    }
}
