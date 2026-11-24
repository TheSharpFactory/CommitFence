namespace CF.App.Rules.Page.Code;

/// <summary>
/// The name of the colour token each kind of C# piece is drawn in, said once for Try it and for the code of a finding.
/// </summary>
internal static class TokenBrushNames
{
    public static string Of(TokenKind kind)
    {
        switch(kind)
        {
            case TokenKind.Keyword:
                return "code-keyword";
            case TokenKind.Type:
                return "code-type";
            case TokenKind.String:
                return "code-string";
            case TokenKind.Comment:
                return "code-comment";
            default:
                return "code-number";
        }
    }
}
