using System.Text.RegularExpressions;
using CF.Core.Code;

namespace CF.App.Doctor;

/// <summary>Continues the generated hook command on display rows without changing its file or clipboard text.</summary>
internal static partial class HookDisplayRows
{
    public static IEnumerable<FileLine> For(FileLine line, string prefix = "")
    {
        var start = prefix.Length;
        var first = true;
        foreach(Match match in Boundaries().Matches(line.Text, start))
        {
            if(match.Value.StartsWith('"'))
                continue;

            var end = match.Value.StartsWith(';') ? match.Index + 1 : match.Index;
            yield return Row(line, prefix, start, end, first);
            start = match.Index + match.Length;
            first = false;
        }

        yield return Row(line, prefix, start, line.Text.Length, first);
    }

    private static FileLine Row(FileLine line, string prefix, int start, int end, bool first)
    {
        if(first)
            return new FileLine(line.Number, line.Text[..end]);

        var body = line.Text[start..end];
        var indent = body.StartsWith("||", StringComparison.Ordinal) ? 2
            : body.StartsWith("exit 1; };", StringComparison.Ordinal) ? 7 : 0;
        return new FileLine(0, new string(' ', prefix.Length + indent) + body);
    }

    // Skip quoted strings and keep the exit and closing brace together on the same display row.
    [GeneratedRegex(""" "[^"]*" | ;\x20(?!\};) | \x20(?=\|\|) """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex Boundaries();
}
