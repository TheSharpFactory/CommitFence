namespace CF.Core.Rules.Settings;

/// <summary>
/// A place in a text, with the line and the column counted from 1.
/// </summary>
internal sealed class TextPosition
{
    public TextPosition(int line, int column)
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }

    public int Column { get; }
}