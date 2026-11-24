using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CF.Meaning;

/// <summary>
/// Writes what a declaration is, as the compiler knows it.
/// </summary>
/// <remarks>
/// The fact is D, file, line, column, kind, name (a type as its full name, a method as the compiler writes it),
/// declared access, and the declared access of the type it is declared in. The line and column are where the
/// declaration starts in the file, counted from 1, so that a reader of the syntax can find it. A partial type has one
/// fact for each part, each with the access of the whole.
/// </remarks>
internal static class DeclarationFactWriter
{
    public static string? Write(SemanticModel model, SyntaxNode node, CancellationToken cancellation)
    {
        var kind = KindOf(node);
        if(kind == null)
            return null;

        var symbol = model.GetDeclaredSymbol(node, cancellation);
        if(symbol == null)
            return null;

        var start = model.SyntaxTree.GetLineSpan(node.Span, cancellation).StartLinePosition;
        return FactText.Join(
            "D",
            model.SyntaxTree.FilePath,
            (start.Line + 1).ToString(CultureInfo.InvariantCulture),
            (start.Character + 1).ToString(CultureInfo.InvariantCulture),
            kind,
            symbol is INamedTypeSymbol type ? SymbolText.TypeName(type) : SymbolText.Signature(symbol),
            SymbolText.Access(symbol.DeclaredAccessibility),
            ContainerAccess(symbol));
    }

    private static string ContainerAccess(ISymbol symbol)
    {
        if(symbol is INamedTypeSymbol || symbol.ContainingType == null)
            return "none";

        return SymbolText.Access(symbol.ContainingType.DeclaredAccessibility);
    }

    private static string? KindOf(SyntaxNode node)
    {
        switch(node.Kind())
        {
            case SyntaxKind.ClassDeclaration:
                return "class";
            case SyntaxKind.StructDeclaration:
                return "struct";
            case SyntaxKind.RecordDeclaration:
            case SyntaxKind.RecordStructDeclaration:
                return "record";
            case SyntaxKind.InterfaceDeclaration:
                return "interface";
            case SyntaxKind.EnumDeclaration:
                return "enum";
            case SyntaxKind.DelegateDeclaration:
                return "delegate";
            case SyntaxKind.MethodDeclaration:
                return "method";
            default:
                return null;
        }
    }
}