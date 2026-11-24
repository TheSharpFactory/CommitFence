using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace CF.App.Rules.Page.Code;

/// <summary>
/// Colours the pieces of C# that <see cref="CSharpTokens" /> found, with the <c>code-</c> tokens of the theme in force.
/// It is public because AvaloniaEdit makes its colouring step a protected method, and the rules of the house allow no protected
/// method in an internal class.
/// </summary>
public sealed class TokenColorizer : DocumentColorizingTransformer
{
    private readonly Control _owner;
    private IReadOnlyList<CodeToken> _tokens = new List<CodeToken>();
    private readonly Dictionary<TokenKind, IBrush> _brushes = new Dictionary<TokenKind, IBrush>();

    internal TokenColorizer(Control owner)
    {
        _owner = owner;
    }

    internal void Scan(string text)
    {
        _tokens = CSharpTokens.Scan(text);
    }

    internal void ThemeChanged()
    {
        _brushes.Clear();
    }

    /// <inheritdoc />
    protected override void ColorizeLine(DocumentLine line)
    {
        var first = FirstEndingAfter(line.Offset);
        for(var index = first; index < _tokens.Count && _tokens[index].Start < line.EndOffset; index++)
        {
            var token = _tokens[index];
            var brush = BrushOf(token.Kind);
            ChangeLinePart(Math.Max(token.Start, line.Offset), Math.Min(token.End, line.EndOffset), element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    private int FirstEndingAfter(int offset)
    {
        var low = 0;
        var high = _tokens.Count;
        while(low < high)
        {
            var middle = (low + high) / 2;
            if(_tokens[middle].End > offset)
                high = middle;
            else
                low = middle + 1;
        }

        return low;
    }

    private IBrush BrushOf(TokenKind kind)
    {
        if(!_brushes.TryGetValue(kind, out var brush))
        {
            brush = ThemeBrushes.Find(_owner, TokenBrushNames.Of(kind));
            _brushes[kind] = brush;
        }

        return brush;
    }
}
