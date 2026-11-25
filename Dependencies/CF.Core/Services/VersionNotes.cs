using System.Globalization;

namespace CF.Core.Services;

/// <summary>The release notes of one version: the text of its section of CHANGELOG.md and the date its heading gives.</summary>
/// <param name="Markdown">The section under the version's heading, in Markdown.</param>
/// <param name="Released">The date in the heading, or null when the heading has none.</param>
public sealed record VersionNotes(string Markdown, DateOnly? Released)
{
    /// <summary>
    /// The section of a changelog for the version: the lines after its "## version" heading up to the next "##" heading, with the date
    /// the heading gives after its dash. Null when the changelog has no section for the version.
    /// </summary>
    public static VersionNotes? Find(string changelog, string version)
    {
        var lines = changelog.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => IsHeadingOf(line, version));
        if(start < 0)
            return null;

        var end = Array.FindIndex(lines, start + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
        var body = string.Join('\n', lines.Skip(start + 1).Take((end < 0 ? lines.Length : end) - start - 1)).Trim();
        return body.Length == 0 ? null : new VersionNotes(body, DateOf(lines[start]));
    }

    private static bool IsHeadingOf(string line, string version)
    {
        if(!line.StartsWith("## ", StringComparison.Ordinal))
            return false;

        var words = line.Substring(3).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words[0] == version;
    }

    private static DateOnly? DateOf(string heading)
    {
        var last = heading.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];
        return DateOnly.TryParseExact(last, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }
}
