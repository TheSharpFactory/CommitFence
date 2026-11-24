using Microsoft.CodeAnalysis;

namespace CF.Meaning;

/// <summary>
/// Writes symbols the one way CommitFence reads them: full names without the language's keywords for types
/// (System.Int32, never int), and the words rules use for access.
/// </summary>
internal static class SymbolText
{
    private static readonly SymbolDisplayFormat _typeFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    private static readonly SymbolDisplayFormat _memberFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

    public static string TypeName(INamedTypeSymbol type)
    {
        return type.ToDisplayString(_typeFormat);
    }

    public static string MemberName(ISymbol member)
    {
        return member.ToDisplayString(_memberFormat);
    }

    public static string Signature(ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    public static string NamespaceName(INamespaceSymbol? container)
    {
        if(container == null || container.IsGlobalNamespace)
            return string.Empty;

        return container.ToDisplayString();
    }

    public static string Access(Accessibility accessibility)
    {
        switch(accessibility)
        {
            case Accessibility.Public:
                return "public";
            case Accessibility.Internal:
                return "internal";
            case Accessibility.Protected:
                return "protected";
            case Accessibility.ProtectedOrInternal:
                return "protected internal";
            case Accessibility.ProtectedAndInternal:
                return "private protected";
            case Accessibility.Private:
                return "private";
            default:
                return "none";
        }
    }
}