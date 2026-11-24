using CF.Languages.CSharp.Catalog;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CF.Languages.CSharp.Reader;

/// <summary>
/// Works out the access a declaration is written with. The access it really has, with the language's defaults applied,
/// is meaning: the compiler says.
/// </summary>
internal static class AccessRules
{
    public static string WrittenAccess(SyntaxTokenList modifiers)
    {
        var isPublic = modifiers.Any(SyntaxKind.PublicKeyword);
        var isPrivate = modifiers.Any(SyntaxKind.PrivateKeyword);
        var isProtected = modifiers.Any(SyntaxKind.ProtectedKeyword);
        var isInternal = modifiers.Any(SyntaxKind.InternalKeyword);

        if(isProtected && isInternal)
            return "protected internal";

        if(isPrivate && isProtected)
            return "private protected";

        if(isPublic)
            return "public";

        if(isPrivate)
            return "private";

        if(isProtected)
            return "protected";

        return isInternal ? "internal" : CSharpNames.None;
    }
}