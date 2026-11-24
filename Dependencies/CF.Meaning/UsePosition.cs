using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CF.Meaning;

/// <summary>
/// Where a use is written, as a position in the file.
/// </summary>
/// <remarks>
/// For a use that has no name of its own it is the token a reader would point at (the name of the called method or of
/// the member, the opening bracket of an index, the operator), and the start of the syntax otherwise.
/// </remarks>
internal static class UsePosition
{
    public static int Of(SyntaxNode syntax)
    {
        switch(syntax)
        {
            case InvocationExpressionSyntax invocation:
                return NameOf(invocation.Expression).SpanStart;
            case MemberAccessExpressionSyntax memberAccess:
                return memberAccess.Name.SpanStart;
            case MemberBindingExpressionSyntax binding:
                return binding.Name.SpanStart;
            case ElementAccessExpressionSyntax access:
                return access.ArgumentList.SpanStart;
            case BinaryExpressionSyntax binary:
                return binary.OperatorToken.SpanStart;
            case AssignmentExpressionSyntax assignment:
                return assignment.OperatorToken.SpanStart;
            default:
                return syntax.SpanStart;
        }
    }

    private static SyntaxNode NameOf(ExpressionSyntax expression)
    {
        switch(expression)
        {
            case MemberAccessExpressionSyntax access:
                return access.Name;
            case MemberBindingExpressionSyntax binding:
                return binding.Name;
            default:
                return expression;
        }
    }
}