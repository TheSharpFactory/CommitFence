using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace CF.Meaning;

/// <summary>
/// Finds the uses that no name in the source stands for, because the compiler resolved them.
/// </summary>
/// <remarks>
/// They are found in the operation tree of every body, initializer and argument (indexers, operators, conversions,
/// target-typed new, initializers, extension methods, lambdas, inferred type arguments, patterns, the type of every
/// expression), and in what the syntax model alone tells (the methods that foreach, await and deconstruction call, the
/// constructor an attribute calls).
/// </remarks>
internal sealed class ResolvedUses
{
    private readonly SemanticModel _model;
    private readonly UseFactWriter _uses;
    private readonly CancellationToken _cancellation;
    private readonly HashSet<IOperation> _roots = new HashSet<IOperation>();

    public ResolvedUses(SemanticModel model, UseFactWriter uses, CancellationToken cancellation)
    {
        _model = model;
        _uses = uses;
        _cancellation = cancellation;
    }

    public void Add(SyntaxNode node)
    {
        AddOperations(node);
        switch(node)
        {
            case AwaitExpressionSyntax await:
                AddAwait(await);
                break;
            case CommonForEachStatementSyntax loop:
                AddForEach(loop);
                break;
            case AssignmentExpressionSyntax assignment:
                AddDeconstruction(_model.GetDeconstructionInfo(assignment), node);
                break;
            case AttributeSyntax attribute:
                _uses.AddSymbol(_model.GetSymbolInfo(attribute, _cancellation).Symbol, node, node.SpanStart);
                break;
        }
    }

    private void AddOperations(SyntaxNode node)
    {
        // a node that starts an operation tree has no parent operation; every other operation is reached from its root
        var operation = _model.GetOperation(node, _cancellation);
        if(operation == null || operation.Parent != null || !_roots.Add(operation))
            return;

        foreach(var inner in operation.DescendantsAndSelf())
            AddOperation(inner);
    }

    private void AddOperation(IOperation operation)
    {
        var syntax = operation.Syntax;
        if(syntax.SyntaxTree != _model.SyntaxTree || operation is IInstanceReferenceOperation)
            return;

        var position = UsePosition.Of(syntax);
        foreach(var symbol in OperationSymbols.Of(operation))
            _uses.AddSymbol(symbol, syntax, position);
    }

    private void AddAwait(AwaitExpressionSyntax node)
    {
        var info = _model.GetAwaitExpressionInfo(node);
        _uses.AddSymbol(info.GetAwaiterMethod, node, node.SpanStart);
        _uses.AddSymbol(info.IsCompletedProperty, node, node.SpanStart);
        _uses.AddSymbol(info.GetResultMethod, node, node.SpanStart);
    }

    private void AddForEach(CommonForEachStatementSyntax node)
    {
        var info = _model.GetForEachStatementInfo(node);
        var position = node.ForEachKeyword.SpanStart;
        _uses.AddSymbol(info.GetEnumeratorMethod, node, position);
        _uses.AddSymbol(info.MoveNextMethod, node, position);
        _uses.AddSymbol(info.CurrentProperty, node, position);
        _uses.AddSymbol(info.DisposeMethod, node, position);
        _uses.AddSymbol(info.ElementType, node, position);
        _uses.AddSymbol(info.ElementConversion.MethodSymbol, node, position);
        if(node is ForEachVariableStatementSyntax variables)
            AddDeconstruction(_model.GetDeconstructionInfo(variables), node);
    }

    private void AddDeconstruction(DeconstructionInfo info, SyntaxNode node)
    {
        _uses.AddSymbol(info.Method, node, node.SpanStart);
        _uses.AddSymbol(info.Conversion?.MethodSymbol, node, node.SpanStart);
        foreach(var nested in info.Nested)
            AddDeconstruction(nested, node);
    }
}