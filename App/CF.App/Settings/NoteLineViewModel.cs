using CF.Core.Updates;

namespace CF.App.Settings;

/// <summary>
/// A line of the release notes as they are shown: a heading, a paragraph or an item of a list, with its bold and code marks.
/// </summary>
internal sealed class NoteLineViewModel
{
    public NoteLineViewModel(ReleaseNoteLine line)
    {
        Marked = line.Marked.Length > 0 ? line.Marked : line.Text;
        IsHeading = line.IsHeading;
        IsItem = line.IsListItem;
    }

    public string Marked { get; }

    public bool IsHeading { get; }

    public bool IsItem { get; }

    public bool IsParagraph
    {
        get { return !IsHeading && !IsItem; }
    }
}
