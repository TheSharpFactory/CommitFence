using System.Text.RegularExpressions;

namespace CF.Languages.CSharp.EditorConfig;

/// <summary>
/// Turns the words of a comment into a rule's title: without a leading rule id and without a closing full stop.
/// </summary>
internal static partial class CommentTitle
{
    [GeneratedRegex(@"^[A-Za-z]+\d+:\s*")]
    private static partial Regex LeadingId();

    /// <summary>
    /// The title in the comment, or nothing when the comment is missing or says nothing.
    /// </summary>
    public static string? From(string? comment)
    {
        if(comment == null)
            return null;

        var title = LeadingId().Replace(comment.Trim(), string.Empty).TrimEnd('.', ' ');
        return title.Length == 0 ? null : title;
    }
}
