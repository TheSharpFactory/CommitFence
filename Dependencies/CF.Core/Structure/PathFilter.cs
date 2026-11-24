using System.Text;
using System.Text.RegularExpressions;

namespace CF.Core.Structure;

/// <summary>
/// Matches a file path against the path patterns of a rule: * is any run of characters inside a folder, ** any run
/// across folders, ? one character.
/// </summary>
internal static class PathFilter
{
    /// <summary>
    /// True when there are no patterns, or when any pattern matches the whole path.
    /// </summary>
    public static bool Matches(IReadOnlyList<string> patterns, string path)
    {
        if(patterns.Count == 0)
            return true;

        var normalized = path.Replace('\\', '/');
        foreach(var pattern in patterns)
        {
            if(Regex.IsMatch(normalized, ToRegex(pattern), RegexOptions.CultureInvariant))
                return true;
        }

        return false;
    }

    private static string ToRegex(string pattern)
    {
        var text = pattern.Replace('\\', '/');
        var builder = new StringBuilder("\\A");
        var index = 0;
        while(index < text.Length)
            index += AppendToken(builder, text, index);

        builder.Append("\\z");
        return builder.ToString();
    }

    private static int AppendToken(StringBuilder builder, string text, int index)
    {
        if(string.CompareOrdinal(text, index, "**/", 0, 3) == 0)
        {
            builder.Append("(?:[\\s\\S]*/)?");
            return 3;
        }

        if(string.CompareOrdinal(text, index, "**", 0, 2) == 0)
        {
            builder.Append("[\\s\\S]*");
            return 2;
        }

        if(text[index] == '*')
        {
            builder.Append("[^/]*");
            return 1;
        }

        if(text[index] == '?')
        {
            builder.Append("[^/]");
            return 1;
        }

        builder.Append(Regex.Escape(text[index].ToString()));
        return 1;
    }
}