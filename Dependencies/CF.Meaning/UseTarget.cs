using Microsoft.CodeAnalysis;

namespace CF.Meaning;

/// <summary>
/// What a name refers to, reduced to what CommitFence asks: the thing used (a member written as Type.Member, or a
/// type), the type that holds it and where that type lives.
/// </summary>
internal sealed class UseTarget
{
    private UseTarget(string used, INamedTypeSymbol type)
    {
        Used = used;
        TypeName = SymbolText.TypeName(type);
        Namespace = SymbolText.NamespaceName(type.ContainingNamespace);
        Assembly = type.ContainingAssembly == null ? string.Empty : type.ContainingAssembly.Name;
        File = FindFile(type);
    }

    public string Used { get; }

    public string TypeName { get; }

    public string Namespace { get; }

    public string Assembly { get; }

    public string File { get; }

    /// <summary>
    /// The target of a symbol, or nothing when the symbol is not a type or a member of a type that can be named.
    /// </summary>
    /// <remarks>
    /// These are a namespace, a local, a parameter, a type parameter, a lambda, a local function, an unresolved name.
    /// </remarks>
    public static UseTarget? From(ISymbol? symbol)
    {
        if(symbol is IAliasSymbol alias)
            symbol = alias.Target;

        if(symbol is INamedTypeSymbol type)
            return FromType(type);

        if(symbol is IMethodSymbol method)
            return FromMethod(method);

        if(symbol is IPropertySymbol || symbol is IFieldSymbol || symbol is IEventSymbol)
            return FromMember(symbol);

        return null;
    }

    private static UseTarget? FromType(INamedTypeSymbol type)
    {
        if(type.TypeKind == TypeKind.Error || type.IsAnonymousType)
            return null;

        var definition = type.OriginalDefinition;
        return new UseTarget(SymbolText.TypeName(definition), definition);
    }

    private static UseTarget? FromMethod(IMethodSymbol method)
    {
        if(method.MethodKind == MethodKind.LocalFunction || method.MethodKind == MethodKind.AnonymousFunction || method.ContainingType == null)
            return null;

        if(method.MethodKind == MethodKind.Constructor)
            return FromType(method.ContainingType);

        return FromMember(method);
    }

    private static UseTarget? FromMember(ISymbol member)
    {
        if(member.ContainingType == null || member.ContainingType.TypeKind == TypeKind.Error || member.ContainingType.IsAnonymousType)
            return null;

        return new UseTarget(SymbolText.MemberName(member.OriginalDefinition), member.ContainingType.OriginalDefinition);
    }

    private static string FindFile(INamedTypeSymbol type)
    {
        foreach(var location in type.Locations)
        {
            if(location.IsInSource && location.SourceTree != null)
                return location.SourceTree.FilePath;
        }

        return string.Empty;
    }
}