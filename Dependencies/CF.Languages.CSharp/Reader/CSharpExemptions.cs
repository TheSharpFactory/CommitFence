using System.Reflection.Metadata;
using CF.Core.Code;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CF.Languages.CSharp.Reader;

/// <summary>Finds exemptions in C# syntax, including compiler suppressions.</summary>
internal static class CSharpExemptions
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> _references = new Lazy<IReadOnlyList<MetadataReference>>(CoreLibrary);
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

        var semanticModel = new Lazy<SemanticModel>(() => CSharpCompilation.Create("Exemptions", [tree], _references.Value).GetSemanticModel(tree));
        foreach(var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            var position = lines.GetLinePosition(attribute.SpanStart);
            AddAttribute(result, attribute, semanticModel, position.Line + lineOffset + 1, position.Character + 1);
        }
    }

    private static unsafe IReadOnlyList<MetadataReference> CoreLibrary()
    {
        // The runtime owns this metadata for the lifetime of the process, including in the single-file app.
        if(!typeof(string).Assembly.TryGetRawMetadata(out var blob, out var length))
            return [];

        var module = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
        return [AssemblyMetadata.Create(module).GetReference()];
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

    private static void AddAttribute(List<SourceExemption> result, AttributeSyntax attribute, Lazy<SemanticModel> semanticModel, int line, int column)
    {
        if(attribute.Name.GetLastToken().ValueText is not ("SuppressMessage" or "SuppressMessageAttribute"))
            return;

        var arguments = attribute.ArgumentList?.Arguments ?? default;
        var checkId = arguments.FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == "checkId");
        if(checkId == null && arguments.Count > 1 && arguments[1].NameEquals == null && arguments[1].NameColon == null)
            checkId = arguments[1];
        var id = StringValue(checkId) ?? ConstantId(checkId, semanticModel);
        var colon = id?.IndexOf(':', StringComparison.Ordinal) ?? -1;
        var rule = colon < 0 ? id : NonBlank(id![..colon]);
        var reason = StringValue(arguments.FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.ValueText == "Justification"));
        result.Add(new SourceExemption(line, column, rule ?? "(not a literal)", ExemptionForm.SuppressMessage, reason));
    }

    /// <summary>
    /// The text of a constant string expression: a concatenation of literals, a constant or <c>nameof</c>. Only this file
    /// is known, so a constant declared in another file gives nothing: it needs the project.
    /// </summary>
    private static string? ConstantId(AttributeArgumentSyntax? argument, Lazy<SemanticModel> semanticModel)
    {
        if(argument == null)
            return null;

        var constant = semanticModel.Value.GetConstantValue(argument.Expression);
        return constant is { HasValue: true, Value: string text } ? NonBlank(text) : null;
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
