using CF.Core.Code;
using CF.Languages.CSharp.Catalog;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CF.Languages.CSharp.Reader;

/// <summary>
/// The properties of an if statement. The body is the branch taken when the condition holds.
/// </summary>
internal static class IfStatementProperties
{
    public static IReadOnlyList<Property> Create(IfStatementSyntax node, SourceLines lines)
    {
        var body = node.Statement;
        var statements = StatementsOf(body);

        return new PropertyList()
            .Syntax(CSharpNames.StatementsInBody, statements.Count)
            .Syntax(CSharpNames.BodyFitsOnOneLine, BodyFitsOnOneLine(statements, lines))
            .Syntax(CSharpNames.FitsOnOneLine, lines.FitsOnOneLine(node))
            .Syntax(CSharpNames.Braces, body is BlockSyntax)
            .Syntax(CSharpNames.IsElseIf, node.Parent is ElseClauseSyntax)
            .Syntax(CSharpNames.HasElse, node.Else != null)
            .Syntax(CSharpNames.Lines, lines.LineCount(node))
            .ToList();
    }

    private static IReadOnlyList<StatementSyntax> StatementsOf(StatementSyntax body)
    {
        if(body is BlockSyntax block)
            return block.Statements;

        return new[] { body };
    }

    private static bool BodyFitsOnOneLine(IReadOnlyList<StatementSyntax> statements, SourceLines lines)
    {
        if(statements.Count == 0)
            return true;

        var first = statements[0].GetFirstToken();
        var last = statements[statements.Count - 1].GetLastToken();
        return lines.TokensFitOnOneLine(first, last);
    }
}