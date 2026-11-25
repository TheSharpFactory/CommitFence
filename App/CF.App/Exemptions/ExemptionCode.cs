using CF.App.Parts;
using CF.App.Review;
using CF.App.Rules.Page.Code;
using CF.Core.Services;

namespace CF.App.Exemptions;

/// <summary>
/// The lines around an exemption as the box of lines shows them: coloured as Try it colours C#, a tab drawn as four spaces, the
/// exemption's own line marked as the line in force.
/// </summary>
internal static class ExemptionCode
{
    private const string _tab = "    ";

    public static IReadOnlyList<FileLineViewModel> Lines(ExemptionEntry entry)
    {
        var text = string.Join('\n', entry.Lines.Select(line => line.Text));
        var tokens = CSharpTokens.Scan(text);
        var lines = new List<FileLineViewModel>();
        var start = 0;
        foreach(var line in entry.Lines)
        {
            var end = start + line.Text.Length;
            lines.Add(new FileLineViewModel
            {
                Number = line.Number,
                Text = line.Text.Replace("\t", _tab, StringComparison.Ordinal),
                Segments = Segments(text, start, end, tokens),
                IsCurrent = line.Number == entry.Line,
                IsPresent = true
            });
            start = end + 1;
        }

        return lines;
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
                segments.Add(Piece(content, at, from, null));

            segments.Add(Piece(content, from, to, TokenBrushNames.Of(token.Kind)));
            at = to;
        }

        if(at < end)
            segments.Add(Piece(content, at, end, null));

        return segments;
    }

    private static CodeSegment Piece(string content, int from, int to, string? brush)
    {
        return new CodeSegment(content[from..to].Replace("\t", _tab, StringComparison.Ordinal), brush);
    }
}
