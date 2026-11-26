using CF.Core.Code;
using CF.Languages.CSharp.Catalog;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CF.Languages.CSharp.Reader;

/// <summary>
/// The properties of a method.
/// </summary>
internal static class MethodProperties
{
    public static IReadOnlyList<Property> Create(MethodDeclarationSyntax node, SourceLines lines)
    {
        var containingType = node.Parent as TypeDeclarationSyntax;

        return new PropertyList()
            .Syntax(CSharpNames.Name, node.Identifier.ValueText)
            .Syntax(CSharpNames.WrittenAccess, AccessRules.WrittenAccess(node.Modifiers))
            .Syntax(CSharpNames.Modifiers, DeclarationFacts.ModifierText(node.Modifiers))
            .Syntax(CSharpNames.IsOverride, DeclarationFacts.IsOverride(node.Modifiers))
            .Syntax(CSharpNames.ContainingType, DeclarationFacts.KindOfType(containingType))
            .Syntax(CSharpNames.ContainingTypeName, containingType?.Identifier.ValueText ?? string.Empty)
            .Syntax(CSharpNames.ContainingTypeWrittenAccess, ContainingTypeWrittenAccess(containingType))
            .Syntax(CSharpNames.ParameterCount, node.ParameterList.Parameters.Count)
            .Syntax(CSharpNames.NestingDepth, node.Body == null ? 0 : NestingDepth(node.Body, 0))
            .Syntax(CSharpNames.Statements, CountStatements(node))
            .Syntax(CSharpNames.Lines, lines.LineCount(node))
            .Syntax(CSharpNames.FitsOnOneLine, lines.FitsOnOneLine(node))
            .Syntax(CSharpNames.DocumentationPresent, DeclarationFacts.HasDocumentation(node))
            .Syntax(CSharpNames.Attributes, DeclarationFacts.AttributeNames(node.AttributeLists))
            .ToList();
    }

    private static int NestingDepth(SyntaxNode node, int depth)
    {
        if(node is LocalFunctionStatementSyntax)
            return depth;

        if(HoldsNestedStatements(node))
            depth++;

        var deepest = depth;
        foreach(var child in node.ChildNodes())
        {
            if(child is StatementSyntax or ElseClauseSyntax or SwitchSectionSyntax or CatchClauseSyntax or FinallyClauseSyntax)
                deepest = Math.Max(deepest, NestingDepth(child, depth));
        }

        return deepest;
    }

    private static bool HoldsNestedStatements(SyntaxNode node)
    {
        return node is IfStatementSyntax { Parent: not ElseClauseSyntax }
            or ForStatementSyntax or CommonForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax
            or SwitchStatementSyntax or TryStatementSyntax or UsingStatementSyntax or LockStatementSyntax
            or FixedStatementSyntax or CheckedStatementSyntax or UnsafeStatementSyntax;
    }

    private static string ContainingTypeWrittenAccess(TypeDeclarationSyntax? containingType)
    {
        if(containingType == null)
            return CSharpNames.None;

        return AccessRules.WrittenAccess(containingType.Modifiers);
    }

    private static int CountStatements(MethodDeclarationSyntax node)
    {
        if(node.Body != null)
            return node.Body.Statements.Count;

        return node.ExpressionBody != null ? 1 : 0;
    }
}