namespace CF.App.Rules.Page.Code;

/// <summary>
/// What a piece of C# is, as far as the editor's colours tell it apart.
/// </summary>
internal enum TokenKind
{
    Keyword,
    Type,
    String,
    Comment,
    Number
}

/// <summary>
/// A piece of C# text that has a colour: where it starts, how long it is, and what it is.
/// </summary>
internal readonly record struct CodeToken(int Start, int Length, TokenKind Kind)
{
    public int End
    {
        get
        {
            return Start + Length;
        }
    }
}

/// <summary>
/// Finds the pieces of C# that are coloured: keywords, names of types, strings and characters, comments and numbers. It
/// reads the text once, from the start, so a comment or a string that runs over several lines is one piece. It is a colouring,
/// not a parser: a name that starts with a capital letter and is not a call or a member is taken for a type.
/// </summary>
internal static class CSharpTokens
{
    private static readonly HashSet<string> _keywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
        "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while", "var", "async", "await", "record", "partial", "yield", "nameof", "init", "required", "with"
    };

    public static IReadOnlyList<CodeToken> Scan(string text)
    {
        var tokens = new List<CodeToken>();
        var index = 0;
        while(index < text.Length)
            index = ScanFrom(text, index, tokens);

        return tokens;
    }

    private static int ScanFrom(string text, int index, List<CodeToken> tokens)
    {
        var letter = text[index];
        if(letter == '/' && Next(text, index) == '/')
            return Add(tokens, index, EndOfLine(text, index), TokenKind.Comment);

        if(letter == '/' && Next(text, index) == '*')
            return Add(tokens, index, EndOfBlockComment(text, index), TokenKind.Comment);

        if(letter == '"' || ((letter == '@' || letter == '$') && StartsString(text, index)))
            return Add(tokens, index, EndOfString(text, index), TokenKind.String);

        if(letter == '\'')
            return Add(tokens, index, EndOfCharacter(text, index), TokenKind.String);

        if(char.IsDigit(letter))
            return Add(tokens, index, EndOfNumber(text, index), TokenKind.Number);

        if(char.IsLetter(letter) || letter == '_')
            return ScanWord(text, index, tokens);

        return index + 1;
    }

    private static int ScanWord(string text, int start, List<CodeToken> tokens)
    {
        var end = start;
        while(end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
            end++;

        var word = text.AsSpan(start, end - start);
        if(_keywords.Contains(word.ToString()))
            tokens.Add(new CodeToken(start, end - start, TokenKind.Keyword));
        else if(char.IsUpper(word[0]) && !FollowsDot(text, start) && !IsCalled(text, end))
            tokens.Add(new CodeToken(start, end - start, TokenKind.Type));

        return end;
    }

    private static int Add(List<CodeToken> tokens, int start, int end, TokenKind kind)
    {
        tokens.Add(new CodeToken(start, end - start, kind));
        return end;
    }

    private static char Next(string text, int index)
    {
        return index + 1 < text.Length ? text[index + 1] : '\0';
    }

    private static bool StartsString(string text, int index)
    {
        var next = Next(text, index);
        if(next == '"')
            return true;

        return (next == '@' || next == '$') && index + 2 < text.Length && text[index + 2] == '"';
    }

    private static int EndOfLine(string text, int index)
    {
        var end = index;
        while(end < text.Length && text[end] != '\n' && text[end] != '\r')
            end++;

        return end;
    }

    private static int EndOfBlockComment(string text, int index)
    {
        var close = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
        return close < 0 ? text.Length : close + 2;
    }

    private static int EndOfString(string text, int index)
    {
        var verbatim = text[index] == '@' || (text[index] == '$' && Next(text, index) == '@');
        var end = index;
        while(text[end] != '"')
            end++;

        end++;
        while(end < text.Length)
        {
            var current = text[end];
            if(current == '"' && verbatim && Next(text, end) == '"')
                end += 2;
            else if(current == '"')
                return end + 1;
            else if(current == '\\' && !verbatim)
                end += 2;
            else if((current == '\n' || current == '\r') && !verbatim)
                return end;
            else
                end++;
        }

        return text.Length;
    }

    private static int EndOfCharacter(string text, int index)
    {
        var end = index + 1;
        while(end < text.Length && text[end] != '\'' && text[end] != '\n' && text[end] != '\r')
            end += text[end] == '\\' ? 2 : 1;

        return Math.Min(text.Length, end < text.Length && text[end] == '\'' ? end + 1 : end);
    }

    private static int EndOfNumber(string text, int index)
    {
        var end = index;
        while(end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_' || (text[end] == '.' && end + 1 < text.Length && char.IsDigit(text[end + 1]))))
            end++;

        return end;
    }

    private static bool FollowsDot(string text, int start)
    {
        var before = start - 1;
        while(before >= 0 && (text[before] == ' ' || text[before] == '\t'))
            before--;

        return before >= 0 && text[before] == '.';
    }

    private static bool IsCalled(string text, int end)
    {
        while(end < text.Length && (text[end] == ' ' || text[end] == '\t'))
            end++;

        return end < text.Length && text[end] == '(';
    }
}
