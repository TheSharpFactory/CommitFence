namespace CF.Core.Updates;

/// <summary>
/// One line of the release text as the notice lays it out.
/// </summary>
public sealed class ReleaseNoteLine
{
    /// <summary>
    /// The text of the line.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The text of the line with its bold (**) and code (backtick) marks kept, for a view that draws them. A heading has none.
    /// </summary>
    public string Marked { get; init; } = string.Empty;

    /// <summary>
    /// Whether the line is a heading.
    /// </summary>
    public bool IsHeading { get; init; }

    /// <summary>
    /// Whether the line is a list item (its text has no bullet).
    /// </summary>
    public bool IsListItem { get; init; }

    /// <summary>
    /// Whether the line is a paragraph: neither a heading nor a list item.
    /// </summary>
    public bool IsParagraph
    {
        get { return !IsHeading && !IsListItem; }
    }
}