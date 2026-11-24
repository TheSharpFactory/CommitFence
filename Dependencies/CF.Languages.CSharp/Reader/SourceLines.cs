using CF.Core.Code;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CF.Languages.CSharp.Reader;

/// <summary>
/// Answers questions about where syntax sits in the lines of one file. Lines and columns are counted from 1.
/// </summary>
internal sealed class SourceLines
{
    private readonly SyntaxTree _tree;

    public SourceLines(SyntaxTree tree)
    {
        _tree = tree;
    }

    public SourceSpan SpanOf(SyntaxNode node)
    {
        return SpanBetween(node.Span);
    }

    public SourceSpan SpanOf(SyntaxToken token)
    {
        return SpanBetween(token.Span);
    }

    public SourceSpan SpanBetween(TextSpan span)
    {
        var lineSpan = _tree.GetLineSpan(span);
        return new SourceSpan(
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.StartLinePosition.Character + 1,
            lineSpan.EndLinePosition.Line + 1,
            lineSpan.EndLinePosition.Character + 1);
    }

    /// <summary>
    /// The place directly above a node, as a span with no lines: it starts at the node's first line and ends on the
    /// line before it.
    /// </summary>
    public SourceSpan EmptyZoneAbove(SyntaxNode node)
    {
        var span = SpanOf(node);
        return new SourceSpan(span.StartLine, 1, span.StartLine - 1, 1);
    }

    public int LineCount(SyntaxNode node)
    {
        var span = SpanOf(node);
        return span.EndLine - span.StartLine + 1;
    }

    public bool FitsOnOneLine(SyntaxNode node)
    {
        return LineCount(node) == 1;
    }

    public bool TokensFitOnOneLine(SyntaxToken first, SyntaxToken last)
    {
        var lineSpan = _tree.GetLineSpan(TextSpan.FromBounds(first.SpanStart, last.Span.End));
        return lineSpan.StartLinePosition.Line == lineSpan.EndLinePosition.Line;
    }
}