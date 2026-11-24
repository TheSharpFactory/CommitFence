using CF.Core.Code;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CF.Languages.CSharp.Reader;

/// <summary>
/// Works out what each kind of artifact's properties were read from besides the artifact itself, so that a change there
/// counts as touching it.
/// </summary>
/// <remarks>
/// These are the header of the type a method or class is declared in, the documentation comment above it, and the else
/// a nested if follows.
/// </remarks>
internal static class ArtifactReaches
{
    public static ArtifactReach ForMethod(MethodDeclarationSyntax node, SourceLines lines)
    {
        return Create(node, lines, HeaderOf(node.Parent as TypeDeclarationSyntax, lines), true);
    }

    public static ArtifactReach ForClass(ClassDeclarationSyntax node, SourceLines lines)
    {
        return Create(node, lines, HeaderOf(node.Parent as TypeDeclarationSyntax, lines), true);
    }

    public static ArtifactReach ForIfStatement(IfStatementSyntax node, SourceLines lines)
    {
        var elseKeyword = node.Parent is ElseClauseSyntax elseClause ? lines.SpanOf(elseClause.ElseKeyword) : null;
        return Create(node, lines, elseKeyword, false);
    }

    private static ArtifactReach Create(SyntaxNode node, SourceLines lines, SourceSpan? dependsOn, bool hasDocumentation)
    {
        var spans = new List<SourceSpan>();
        if(dependsOn != null)
            spans.Add(dependsOn);

        var documentation = hasDocumentation ? DocumentationZoneOf(node, lines) : null;
        return new ArtifactReach(lines.SpanOf(node), spans, documentation);
    }

    /// <summary>
    /// From the first modifier (or the keyword) to the name of the type: everything the properties about the containing
    /// type are read from. Nothing when the artifact is not declared in a type.
    /// </summary>
    private static SourceSpan? HeaderOf(TypeDeclarationSyntax? type, SourceLines lines)
    {
        if(type == null)
            return null;

        var first = type.Modifiers.Count > 0 ? type.Modifiers[0] : type.Keyword;
        return lines.SpanBetween(TextSpan.FromBounds(first.SpanStart, type.Identifier.Span.End));
    }

    private static SourceSpan DocumentationZoneOf(SyntaxNode node, SourceLines lines)
    {
        var comments = node.GetLeadingTrivia().Where(IsDocumentationComment).ToList();
        if(comments.Count == 0)
            return lines.EmptyZoneAbove(node);

        var start = comments[0].SpanStart;
        var end = Math.Max(start, comments[comments.Count - 1].Span.End - 1);
        return lines.SpanBetween(TextSpan.FromBounds(start, end));
    }

    private static bool IsDocumentationComment(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
    }
}