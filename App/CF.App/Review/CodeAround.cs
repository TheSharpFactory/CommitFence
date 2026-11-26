using CF.App.Rules.Page.Code;
using CF.Core.Code;

namespace CF.App.Review;

/// <summary>
/// Cuts the lines around a finding out of the text of the file as the reviewed tree held it, coloured as Try it colours C#.
/// </summary>
internal static class CodeAround
{
    private const int _linesAround = 3;
    private const string _tab = "    ";

    public static IReadOnlyList<CodeLineViewModel> Cut(string content, int line, bool isBlock)
    {
        var cut = Excerpt.Around(content, line, _linesAround);
        if(cut.Count == 0)
            return new List<CodeLineViewModel>();

        var tokens = CSharpTokens.Scan(content);
        return cut.Select(excerpt => new CodeLineViewModel(excerpt.Number, Segments(content, excerpt.Start, excerpt.End, tokens), excerpt.Number == line, isBlock)).ToList();
    }

    public static List<CodeSegment> Segments(string content, int start, int end, IReadOnlyList<CodeToken> tokens)
    {
        var segments = new List<CodeSegment>();
        var at = start;
        for(var index = FirstTokenAfter(tokens, start); index < tokens.Count && tokens[index].Start < end; index++)
        {
            var token = tokens[index];

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

    private static int FirstTokenAfter(IReadOnlyList<CodeToken> tokens, int start)
    {
        var low = 0;
        var high = tokens.Count;
        while(low < high)
        {
            var middle = low + (high - low) / 2;
            if(tokens[middle].End <= start)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }
}
