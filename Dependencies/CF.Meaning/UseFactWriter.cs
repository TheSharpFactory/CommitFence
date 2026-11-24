using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CF.Meaning;

/// <summary>
/// Collects the uses of one file: every type and every member of a type that the code refers to, by a name in the
/// source or only as the compiler resolved it.
/// </summary>
/// <remarks>
/// A fact is U, file, the member that uses, its namespace, what is used (the member, or the type itself), the type that
/// holds it, that type's namespace and assembly and the file that declares it (empty when it comes from a library), and
/// the places, as line:column separated by commas. The same use in the same member is one fact with all its places. A
/// name in a using directive is not a use. A use in a field's initializer belongs to the variable it initializes.
/// </remarks>
internal sealed class UseFactWriter
{
    private const int _deepestTypeArgument = 8;

    private readonly SemanticModel _model;
    private readonly CancellationToken _cancellation;
    private readonly SortedDictionary<string, UsePlaces> _uses = new SortedDictionary<string, UsePlaces>(StringComparer.Ordinal);
    private readonly Dictionary<ISymbol, UseTarget?> _targets = new Dictionary<ISymbol, UseTarget?>(SymbolEqualityComparer.Default);
    private readonly Dictionary<ISymbol, string> _memberTexts = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);

    public UseFactWriter(SemanticModel model, CancellationToken cancellation)
    {
        _model = model;
        _cancellation = cancellation;
    }

    public void Add(SimpleNameSyntax name)
    {
        if(name.FirstAncestorOrSelf<UsingDirectiveSyntax>() != null)
            return;

        var symbol = _model.GetSymbolInfo(name, _cancellation).Symbol;
        AddTarget(TargetOf(symbol), name, name.SpanStart);
    }

    /// <summary>
    /// A use that the compiler resolved.
    /// </summary>
    /// <remarks>
    /// A type counts with the types it is made of (type arguments, array elements, tuple elements); a method counts
    /// with its type arguments, inferred ones included; a local variable counts as its type; a lambda counts as the
    /// types of its parameters and of what it returns. The types of the language itself (int, string, object and the
    /// like) are left out.
    /// </remarks>
    public void AddSymbol(ISymbol? symbol, SyntaxNode scope, int position)
    {
        switch(symbol)
        {
            case null:
                return;
            case ITypeSymbol type:
                AddType(type, scope, position, 0);
                return;
            case ILocalSymbol local:
                AddType(local.Type, scope, position, 0);
                return;
            case IMethodSymbol method:
                AddMethod(method, scope, position);
                return;
            default:
                AddTarget(TargetOf(symbol), scope, position);
                AddTypeArguments(symbol.ContainingType, scope, position, 0);
                return;
        }
    }

    public IEnumerable<string> ToFacts()
    {
        return _uses.Select(use => use.Key + "|" + use.Value.ToText());
    }

    private void AddMethod(IMethodSymbol method, SyntaxNode scope, int position)
    {
        AddTarget(TargetOf(method), scope, position);
        AddTypeArguments(method.ContainingType, scope, position, 0);
        foreach(var argument in method.TypeArguments)
            AddType(argument, scope, position, 0);

        if(method.MethodKind != MethodKind.AnonymousFunction)
            return;

        AddType(method.ReturnType, scope, position, 0);
        foreach(var parameter in method.Parameters)
            AddType(parameter.Type, scope, position, 0);
    }

    private void AddType(ITypeSymbol? type, SyntaxNode scope, int position, int depth)
    {
        if(type == null || depth > _deepestTypeArgument)
            return;

        switch(type)
        {
            case IArrayTypeSymbol array:
                AddType(array.ElementType, scope, position, depth + 1);
                return;
            case IPointerTypeSymbol pointer:
                AddType(pointer.PointedAtType, scope, position, depth + 1);
                return;
            case INamedTypeSymbol named:
                AddNamedType(named, scope, position, depth);
                return;
        }
    }

    private void AddNamedType(INamedTypeSymbol type, SyntaxNode scope, int position, int depth)
    {
        if(!IsOfTheLanguage(type))
            AddTarget(TargetOf(type), scope, position);

        AddTypeArguments(type, scope, position, depth);
    }

    private void AddTypeArguments(INamedTypeSymbol? type, SyntaxNode scope, int position, int depth)
    {
        if(type == null)
            return;

        foreach(var argument in type.TypeArguments)
            AddType(argument, scope, position, depth + 1);

        AddTypeArguments(type.ContainingType, scope, position, depth);
    }

    private static bool IsOfTheLanguage(INamedTypeSymbol type)
    {
        return type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_DateTime;
    }

    private UseTarget? TargetOf(ISymbol? symbol)
    {
        if(symbol == null)
            return null;

        if(_targets.TryGetValue(symbol, out var known))
            return known;

        var target = UseTarget.From(symbol);
        _targets[symbol] = target;
        return target;
    }

    private void AddTarget(UseTarget? target, SyntaxNode scope, int position)
    {
        if(target == null)
            return;

        var enclosing = FindEnclosingMember(scope);
        var key = FactText.Join(
            "U",
            _model.SyntaxTree.FilePath,
            enclosing == null ? string.Empty : MemberText(enclosing),
            SymbolText.NamespaceName(enclosing?.ContainingNamespace),
            target.Used,
            target.TypeName,
            target.Namespace,
            target.Assembly,
            target.File);
        if(!_uses.TryGetValue(key, out var places))
        {
            places = new UsePlaces();
            _uses[key] = places;
        }

        var start = _model.SyntaxTree.GetLineSpan(new TextSpan(position, 0), _cancellation).StartLinePosition;
        places.Add(start.Line + 1, start.Character + 1);
    }

    private string MemberText(ISymbol member)
    {
        if(_memberTexts.TryGetValue(member, out var text))
            return text;

        text = SymbolText.Signature(member);
        _memberTexts[member] = text;
        return text;
    }

    /// <summary>
    /// The member whose declaration holds the node, a signature or an initializer included, so that the return type of
    /// a method is a use by the method.
    /// </summary>
    /// <remarks>
    /// A node outside any member belongs to what the compiler says encloses it.
    /// </remarks>
    private ISymbol? FindEnclosingMember(SyntaxNode node)
    {
        var declaration = node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        var symbol = declaration == null ? null : DeclaredSymbolOf(declaration, node);

        return symbol ?? _model.GetEnclosingSymbol(node.SpanStart, _cancellation);
    }

    private ISymbol? DeclaredSymbolOf(MemberDeclarationSyntax declaration, SyntaxNode node)
    {
        if(declaration is BaseFieldDeclarationSyntax field)
            return _model.GetDeclaredSymbol(DeclaratorOf(field, node), _cancellation);

        if(declaration is BaseNamespaceDeclarationSyntax)
            return null;

        return _model.GetDeclaredSymbol(declaration, _cancellation);
    }

    /// <summary>
    /// The variable of a field declaration that the node belongs to: the one whose initializer holds it. The type
    /// written once for all the variables belongs to the first.
    /// </summary>
    private static VariableDeclaratorSyntax DeclaratorOf(BaseFieldDeclarationSyntax field, SyntaxNode node)
    {
        foreach(var declarator in node.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>())
        {
            if(declarator.Parent is VariableDeclarationSyntax declaration && declaration.Parent == field)
                return declarator;
        }

        return field.Declaration.Variables[0];
    }
}