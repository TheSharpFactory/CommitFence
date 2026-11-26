using System.Text;

namespace CF.Core.Fixes;

/// <summary>The source text and byte conventions of one side of a fix.</summary>
internal sealed record FixSource(string Text, int CodePage, bool HasMark, string Endings, string EndingsInOrder)
{
    private static readonly Encoding[] _markedEncodings =
        [Encoding.UTF32, new UTF32Encoding(true, true), Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode];

    public static FixSource Read(ReadOnlySpan<byte> bytes)
    {
        var encoding = Encoding.UTF8;
        var markLength = 0;
        foreach(var candidate in _markedEncodings)
        {
            if(!bytes.StartsWith(candidate.Preamble))
                continue;

            encoding = candidate;
            markLength = candidate.Preamble.Length;
            break;
        }

        var strict = Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var text = strict.GetString(bytes[markLength..]);
        var endings = new StringBuilder();
        for(var index = 0; index < text.Length; index++)
        {
            if(text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                endings.Append('W');
                index++;
                continue;
            }

            if(text[index] == '\r')
                endings.Append('R');
            else if(text[index] == '\n')
                endings.Append('L');
        }

        var order = endings.ToString();
        var style = DescribeEndings(order);
        return new FixSource(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'),
            encoding.CodePage, markLength != 0, style, order);
    }

    /// <summary>
    /// Gives the lines a formatter added the file's own line ending. A formatter writes its new lines with the
    /// machine's ending, so a file that had one kind would come back mixed; a file that comes back with one kind
    /// (the formatter applying end_of_line) is left as it is.
    /// </summary>
    public static byte[] KeepEndings(byte[] before, byte[] after)
    {
        var original = Read(before);
        var formatted = Read(after);
        var mixedByTheFormatter = original.Endings is "LF" or "CRLF" && formatted.Endings == "mixed"
            && formatted.CodePage == original.CodePage && formatted.HasMark == original.HasMark;
        if(!mixedByTheFormatter)
            return after;

        var encoding = Encoding.GetEncoding(formatted.CodePage);
        var newline = original.Endings == "LF" ? "\n" : "\r\n";
        var body = encoding.GetBytes(formatted.Text.Replace("\n", newline, StringComparison.Ordinal));
        return formatted.HasMark ? [.. encoding.GetPreamble(), .. body] : body;
    }

    public static bool SameLines(FixSource before, FixSource after)
    {
        return string.Equals(WithoutFinalNewline(before.Text), WithoutFinalNewline(after.Text), StringComparison.Ordinal);
    }

    public static string DescribeChanges(FixSource before, FixSource after, bool only)
    {
        var details = new List<string>();
        if(before.Endings != after.Endings)
            details.Add($"the line endings of this file ({before.Endings} to {after.Endings})");
        else if(before.Endings == "mixed" && before.EndingsInOrder != after.EndingsInOrder)
            details.Add("the mixed line endings of this file");
        else if(before.Text.EndsWith('\n') != after.Text.EndsWith('\n'))
            details.Add("the final line ending of this file");

        if(before.HasMark != after.HasMark || (before.HasMark && before.CodePage != after.CodePage))
            details.Add("its byte order mark");

        if(before.CodePage != after.CodePage)
            details.Add("its text encoding");

        return details.Count == 0 ? string.Empty : $"The fix changes {(only ? "only " : string.Empty)}{string.Join(" and ", details)}.";
    }

    private static string WithoutFinalNewline(string text)
    {
        return text.EndsWith('\n') ? text[..^1] : text;
    }

    private static string DescribeEndings(string order)
    {
        return order.Distinct().ToList() switch
        {
            [] => "none",
            ['W'] => "CRLF",
            ['R'] => "CR",
            ['L'] => "LF",
            _ => "mixed"
        };
    }
}
