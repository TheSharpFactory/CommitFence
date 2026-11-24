namespace CF.Core.Code;

/// <summary>
/// One line of a repository's own file, with its number (the first line is 1) and its text without the line break.
/// </summary>
public sealed class FileLine
{
    /// <summary>
    /// Creates a line.
    /// </summary>
    public FileLine(int number, string text)
    {
        Number = number;
        Text = text;
    }

    /// <summary>
    /// The number of the line in its file, from 1.
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// The text of the line.
    /// </summary>
    public string Text { get; }
}
