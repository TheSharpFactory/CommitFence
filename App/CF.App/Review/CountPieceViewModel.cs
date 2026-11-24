namespace CF.App.Review;

/// <summary>
/// One piece of the verdict's counts: a filled piece for blocking findings or for warnings, a plain sentence, or the filter piece of a rule chosen.
/// </summary>
internal sealed class CountPieceViewModel
{
    public CountPieceViewModel(string text, CountPieceKind kind)
    {
        Text = text;
        Kind = kind;
    }

    public string Text { get; }

    /// <summary>
    /// The filter piece's clear button: what it does and what it is called ("Show all 184 findings").
    /// </summary>
    public System.Windows.Input.ICommand? Clear { get; init; }

    public string ClearName { get; init; } = string.Empty;

    public CountPieceKind Kind { get; }

    public bool IsBlock
    {
        get
        {
            return Kind == CountPieceKind.Block;
        }
    }

    public bool IsWarn
    {
        get
        {
            return Kind == CountPieceKind.Warn;
        }
    }

    public bool IsAllow
    {
        get
        {
            return Kind == CountPieceKind.Allow;
        }
    }

    public bool IsFilter
    {
        get
        {
            return Kind == CountPieceKind.Filter;
        }
    }

    public bool IsPlain
    {
        get
        {
            return Kind == CountPieceKind.Plain;
        }
    }
}

/// <summary>
/// The look of a count piece.
/// </summary>
internal enum CountPieceKind
{
    Block,

    Warn,

    Plain,

    Allow,

    /// <summary>
    /// The piece that says one rule is chosen: "Only", its id and a button that shows all again.
    /// </summary>
    Filter
}
