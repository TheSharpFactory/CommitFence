using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace CF.Meaning;

/// <summary>
/// The symbols the compiler resolved for one operation.
/// </summary>
/// <remarks>
/// They are the member it calls or reads, the operator or conversion method it uses, the type it was given, the type
/// that a pattern, a cast or typeof names. What an operation holds inside it is another operation, reached on its own.
/// </remarks>
internal static class OperationSymbols
{
    public static IEnumerable<ISymbol?> Of(IOperation operation)
    {
        yield return operation.Type;
        foreach(var symbol in Resolved(operation))
            yield return symbol;
    }

    private static IEnumerable<ISymbol?> Resolved(IOperation operation)
    {
        switch(operation)
        {
            case IInvocationOperation invocation:
                return new ISymbol?[] { invocation.TargetMethod };
            case IObjectCreationOperation creation:
                return new ISymbol?[] { creation.Constructor };
            case IImplicitIndexerReferenceOperation implicitIndexer:
                return new ISymbol?[] { implicitIndexer.IndexerSymbol, implicitIndexer.LengthSymbol };
            case IMemberReferenceOperation reference:
                return new ISymbol?[] { reference.Member };
            case IBinaryOperation binary:
                return new ISymbol?[] { binary.OperatorMethod };
            case IUnaryOperation unary:
                return new ISymbol?[] { unary.OperatorMethod };
            case ICompoundAssignmentOperation compound:
                return new ISymbol?[] { compound.OperatorMethod };
            case IIncrementOrDecrementOperation step:
                return new ISymbol?[] { step.OperatorMethod };
            case IConversionOperation conversion:
                return new ISymbol?[] { conversion.OperatorMethod };
            case IAnonymousFunctionOperation function:
                return new ISymbol?[] { function.Symbol };
            case IVariableDeclaratorOperation declarator:
                return new ISymbol?[] { declarator.Symbol };
            case ITypeOfOperation typeOf:
                return new ISymbol?[] { typeOf.TypeOperand };
            case ISizeOfOperation sizeOf:
                return new ISymbol?[] { sizeOf.TypeOperand };
            case IIsTypeOperation isType:
                return new ISymbol?[] { isType.TypeOperand };
            case IDeclarationPatternOperation declaration:
                return new ISymbol?[] { declaration.MatchedType };
            case ITypePatternOperation typePattern:
                return new ISymbol?[] { typePattern.MatchedType };
            case IRecursivePatternOperation recursive:
                return new ISymbol?[] { recursive.MatchedType };
            case ICatchClauseOperation clause:
                return new ISymbol?[] { clause.ExceptionType };
            default:
                return Array.Empty<ISymbol?>();
        }
    }
}