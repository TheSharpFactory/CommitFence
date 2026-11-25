using CF.Core.Code;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CF.Languages.CSharp.Reader;

/// <summary>Finds exemptions in C# syntax, including compiler suppressions.</summary>
internal static class CSharpExemptions
{
    public static IReadOnlyList<SourceExemption> Read(string content)
    {
        var result = new List<SourceExemption>();
        ReadSyntax(content, 0, result);
        return result.OrderBy(exemption => exemption.Line).ThenBy(exemption => exemption.Column).ToList();
    }

    private static void ReadSyntax(string content, int lineOffset, List<SourceExemption> result)
    {
        var tree = CSharpSyntaxTree.ParseText(content);
        var lines = tree.GetText().Lines;
        var root = tree.GetRoot();
        foreach(var trivia in root.DescendantTrivia())
        {
            var line = lines.GetLineFromPosition(trivia.SpanStart);
            var column = trivia.SpanStart - line.Start + 1;
            var number = line.LineNumber + lineOffset + 1;
            if(trivia.IsKind(SyntaxKind.DisabledTextTrivia))
                ReadSyntax(trivia.ToString(), number - 1, result);
            else if(trivia.GetStructure() is PragmaWarningDirectiveTriviaSyntax pragma && pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
                AddPragma(result, pragma, number, column);
            else if(IsSingleLineComment(trivia, line))
                AddAllow(result, trivia, line, number, column);
        }

        foreach(var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            var position = lines.GetLinePosition(attribute.SpanStart);
            AddAttribute(result, attribute, position.Line + lineOffset + 1, position.Character + 1);
        }
    }

    private static bool IsSingleLineComment(SyntaxTrivia trivia, TextLine line)
    {
        return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
            || (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && trivia.Span.End <= line.End);
    }

    private static void AddAllow(List<SourceExemption> result, SyntaxTrivia trivia, TextLine line, int number, int column)
    {
        var comment = trivia.ToString();
        var block = trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);
        if(block && (comment.Length < 4 || !comment.EndsWith("*/", StringComparison.Ordinal)))
            return;

        var text = line.ToString().AsSpan();
        var only = text[..(column - 1)].Trim().IsEmpty && text[(trivia.Span.End - line.Start)..].Trim().IsEmpty;
        var exemption = AllowComment.Read(block ? comment[2..^2] : comment[2..], number, column, only);
        if(exemption != null)
            result.Add(exemption);
    }

    private static void AddPragma(List<SourceExemption> result, PragmaWarningDirectiveTriviaSyntax pragma, int line, int column)
    {
        var comment = pragma.DescendantTrivia().FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia));
        var reason = comment.RawKind == 0 ? null : NonBlank(comment.ToString()[2..]);
        if(pragma.ErrorCodes.Count == 0)
            result.Add(new SourceExemption(line, column, null, ExemptionForm.Pragma, reason));

        foreach(var code in pragma.ErrorCodes)
        {
            if(!code.IsMissing)
                result.Add(new SourceExemption(line, column, code.ToString(), ExemptionForm.Pragma, reason));
        }
    }

    private static void AddAttribute(List<SourceExemption> result, AttributeSyntax attribute, int line, int column)
    {
        if(attribute.Name.GetLastToken().ValueText is not ("SuppressMessage" or "SuppressMessageAttribute") || attribute.ArgumentList == null)
            return;

        var arguments = attribute.ArgumentList.Arguments;
        var checkId = arguments.FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == "checkId");
        if(checkId == null && arguments.Count > 1 && arguments[1].NameEquals == null && arguments[1].NameColon == null)
            checkId = arguments[1];
        var id = StringValue(checkId);
        if(id == null)
            return;

        var colon = id.IndexOf(':', StringComparison.Ordinal);
        var rule = NonBlank(colon < 0 ? id : id[..colon]);
        var reason = StringValue(arguments.FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.ValueText == "Justification"));
        if(rule != null)
            result.Add(new SourceExemption(line, column, rule, ExemptionForm.SuppressMessage, reason));
    }

    private static string? StringValue(AttributeArgumentSyntax? argument)
    {
        return argument?.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? NonBlank(literal.Token.ValueText) : null;
    }

    private static string? NonBlank(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
