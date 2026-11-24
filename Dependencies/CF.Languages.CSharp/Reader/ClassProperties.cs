using CF.Core.Code;
using CF.Languages.CSharp.Catalog;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CF.Languages.CSharp.Reader;

/// <summary>
/// The properties of a class.
/// </summary>
internal static class ClassProperties
{
    public static IReadOnlyList<Property> Create(ClassDeclarationSyntax node, SourceLines lines)
    {
        return new PropertyList()
            .Syntax(CSharpNames.Name, node.Identifier.ValueText)
            .Syntax(CSharpNames.WrittenAccess, AccessRules.WrittenAccess(node.Modifiers))
            .Syntax(CSharpNames.Modifiers, DeclarationFacts.ModifierText(node.Modifiers))
            .Syntax(CSharpNames.ContainingType, DeclarationFacts.KindOfType(node.Parent as TypeDeclarationSyntax))
            .Syntax(CSharpNames.IsPartial, node.Modifiers.Any(SyntaxKind.PartialKeyword))
            .Syntax(CSharpNames.Lines, lines.LineCount(node))
            .Syntax(CSharpNames.FitsOnOneLine, lines.FitsOnOneLine(node))
            .Syntax(CSharpNames.DocumentationPresent, DeclarationFacts.HasDocumentation(node))
            .Syntax(CSharpNames.Attributes, DeclarationFacts.AttributeNames(node.AttributeLists))
            .ToList();
    }
}