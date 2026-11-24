using CF.Languages.CSharp.Catalog;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CF.Languages.CSharp.Reader;

/// <summary>
/// Facts every declaration has in the same way: its modifiers, attributes and documentation comment.
/// </summary>
internal static class DeclarationFacts
{
    public static string KindOfType(SyntaxNode? type)
    {
        switch(type?.Kind())
        {
            case SyntaxKind.ClassDeclaration:
                return "class";
            case SyntaxKind.StructDeclaration:
                return "struct";
            case SyntaxKind.InterfaceDeclaration:
                return "interface";
            case SyntaxKind.RecordDeclaration:
            case SyntaxKind.RecordStructDeclaration:
                return "record";
            default:
                return CSharpNames.None;
        }
    }

    public static string ModifierText(SyntaxTokenList modifiers)
    {
        return string.Join(" ", modifiers.Select(modifier => modifier.Text));
    }

    public static bool IsOverride(SyntaxTokenList modifiers)
    {
        return modifiers.Any(SyntaxKind.OverrideKeyword);
    }

    public static string AttributeNames(SyntaxList<AttributeListSyntax> attributeLists)
    {
        var names = attributeLists.SelectMany(list => list.Attributes).Select(attribute => NameText(attribute.Name));
        return string.Join(", ", names);
    }

    /// <summary>
    /// A name as the compiler understands it: a name written with a leading at sign, or with unicode escapes, is the
    /// plain name. A qualified name keeps its dots.
    /// </summary>
    private static string NameText(NameSyntax name)
    {
        switch(name)
        {
            case IdentifierNameSyntax identifier:
                return identifier.Identifier.ValueText;
            case QualifiedNameSyntax qualified:
                return NameText(qualified.Left) + "." + NameText(qualified.Right);
            case AliasQualifiedNameSyntax aliased:
                return aliased.Alias.Identifier.ValueText + "::" + NameText(aliased.Name);
            case GenericNameSyntax generic:
                return generic.Identifier.ValueText + generic.TypeArgumentList;
            default:
                return name.ToString();
        }
    }

    public static bool HasDocumentation(SyntaxNode node)
    {
        return node.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
    }
}