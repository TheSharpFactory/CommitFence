using System.Text;
using System.Text.RegularExpressions;

namespace CF.Core.Updates;

/// <summary>
/// The release text (Markdown) as plain text for the notice the program shows before it updates.
/// </summary>
public static class ReleaseNotes
{
    private static readonly Regex _link = new Regex(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled);

    /// <summary>
    /// The release text as plain text: what comes before its Downloads section. The lines a paragraph or a list item
    /// was wrapped over are joined again, list items start with a bullet, headings stand on their own line, and
    /// Markdown marks, links and tables are left out. Cut after the block that passes <paramref name="maxLength" />
    /// characters. Null when there is no text.
    /// </summary>
    public static string? Summarize(string? markdown, int maxLength)
    {
        if(string.IsNullOrWhiteSpace(markdown))
            return null;

        var blocks = ReadBlocks(markdown);
        var text = new StringBuilder();
        for(var i = 0; i < blocks.Count; i++)
        {
            if(i > 0 && text.Length + blocks[i].Text.Length > maxLength)
            {
                text.Append("\n…");
                break;
            }

            if(i > 0)
                text.Append(IsTight(blocks[i - 1], blocks[i]) ? "\n" : "\n\n");

            text.Append(blocks[i].Text);
        }

        var result = text.ToString().Trim();
        return result.Length == 0 ? null : result;
    }

    /// <summary>
    /// The same text as <see cref="Summarize" />, line by line, for a view to lay out: headings, list items (without
    /// their bullet) and paragraphs, cut the same way with a last "…" line.
    /// </summary>
    public static List<ReleaseNoteLine> Lines(string? markdown, int maxLength)
    {
        var result = new List<ReleaseNoteLine>();
        if(string.IsNullOrWhiteSpace(markdown))
            return result;

        var blocks = ReadBlocks(markdown);
        var length = 0;
        for(var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if(i > 0 && length + block.Text.Length > maxLength)
            {
                result.Add(new ReleaseNoteLine { Text = "…" });
                break;
            }

            length += block.Text.Length;
            var text = block.IsListItem ? block.Text.Substring(2) : block.Text;
            result.Add(new ReleaseNoteLine { Text = text, IsHeading = block.IsHeading, IsListItem = block.IsListItem });
        }

        return result;
    }

    /// <summary>
    /// A heading sits right on what it heads, and list items on each other; everything else has a blank line between.
    /// </summary>
    private static bool IsTight(NotesBlock previous, NotesBlock next)
    {
        return previous.IsHeading || (previous.IsListItem && next.IsListItem);
    }

    private static List<NotesBlock> ReadBlocks(string markdown)
    {
        var blocks = new List<NotesBlock>();
        NotesBlock? current = null;
        foreach(var rawLine in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim();
            if(line.StartsWith('#'))
            {
                var heading = CleanInline(line.TrimStart('#'));
                if(string.Equals(heading, "Downloads", StringComparison.OrdinalIgnoreCase))
                    break;

                current = null;
                if(heading.Length > 0)
                    blocks.Add(new NotesBlock { Text = heading, IsHeading = true });

                continue;
            }

            if(line.Length == 0 || line.StartsWith('|') || line == "---")
            {
                current = null;
                continue;
            }

            if(line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                current = new NotesBlock { Text = "• " + CleanInline(line.Substring(2)), IsListItem = true };
                blocks.Add(current);
                continue;
            }

            // a wrapped line: it continues the paragraph or list item above it
            if(current != null)
            {
                current.Text += " " + CleanInline(line);
                continue;
            }

            current = new NotesBlock { Text = CleanInline(line) };
            blocks.Add(current);
        }

        return blocks;
    }

    /// <summary>
    /// Bold and code marks out, a link as its text.
    /// </summary>
    private static string CleanInline(string text)
    {
        var plain = text.Replace("**", string.Empty, StringComparison.Ordinal).Replace("`", string.Empty, StringComparison.Ordinal);
        return _link.Replace(plain, "$1").Trim();
    }

    /// <summary>
    /// A paragraph, a list item or a heading of the release text.
    /// </summary>
    private sealed class NotesBlock
    {
        public string Text { get; set; } = string.Empty;

        public bool IsHeading { get; init; }

        public bool IsListItem { get; init; }
    }
}