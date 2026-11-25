using System.Buffers;

namespace CF.Core.Code;

/// <summary>Reads the shared allow syntax after the language has identified a comment.</summary>
public static class AllowComment
{
    private static readonly SearchValues<char> _spaces = SearchValues.Create(" \t\r\n");
    private static readonly SearchValues<char> _idSeparators = SearchValues.Create(" \t\r\n,;");

    /// <summary>The marker that begins an allow comment.</summary>
    public const string Marker = "commitfence:allow";

    /// <summary>Reads the rule and reason from comment text without its language delimiters.</summary>
    public static SourceExemption? Read(string text, int line, int column, bool commentOnlyLine)
    {
        var content = text.AsSpan().Trim();
        if(!content.StartsWith(Marker, StringComparison.Ordinal) || content.Length <= Marker.Length || !char.IsWhiteSpace(content[Marker.Length]))
            return null;

        var ruleAndReason = content[Marker.Length..].Trim();
        var colon = ruleAndReason.IndexOf(':');
        var space = ruleAndReason.IndexOfAny(_spaces);
        var id = colon < 0 && space >= 0 ? ruleAndReason[..space] : (colon < 0 ? ruleAndReason : ruleAndReason[..colon]).Trim();
        if(id.IsEmpty || id.ContainsAny(_idSeparators))
            return null;

        var reason = colon < 0 ? ReadOnlySpan<char>.Empty : ruleAndReason[(colon + 1)..].Trim();
        return new SourceExemption(line, column, id.ToString(), ExemptionForm.AllowComment, reason.IsEmpty ? null : reason.ToString(), commentOnlyLine);
    }
}