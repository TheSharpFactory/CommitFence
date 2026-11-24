using System.Buffers;
using System.Text.RegularExpressions;

namespace CF.Languages.CSharp.EditorConfig;

/// <summary>
/// Reads the text of an .editorconfig the way the compiler does and keeps the dotnet_diagnostic severity lines: keys
/// without regard to case, spaces around the separator (an equals sign or a colon), and a comment after the value
/// (# or ;) that is not part of the value.
/// </summary>
internal static partial class EditorConfigParser
{
    private static readonly SearchValues<char> _commentMarks = SearchValues.Create("#;");

    [GeneratedRegex(@"^\s*\[(([^#;]|\\#|\\;)+)\]")]
    private static partial Regex SectionHeader();

    [GeneratedRegex(@"^\s*([\w\.\-]+)\s*[=:]\s*(.*?)\s*$")]
    private static partial Regex Property();

    [GeneratedRegex(@"^dotnet_diagnostic\.(?<id>[^.\s]+)\.severity$", RegexOptions.IgnoreCase)]
    private static partial Regex SeverityKey();

    public static IReadOnlyList<EditorConfigSetting> Parse(string text)
    {
        var settings = new List<EditorConfigSetting>();
        string? section = null;
        string? commentAbove = null;
        var lineNumber = 0;
        foreach(var raw in text.Split('\n'))
        {
            lineNumber++;
            var line = raw.TrimEnd('\r');
            var header = SectionHeader().Match(line);
            if(header.Success)
                section = header.Groups[1].Value.Trim();

            var setting = header.Success ? null : ReadSetting(section, line, commentAbove, lineNumber);
            if(setting != null)
                settings.Add(setting);

            commentAbove = IsComment(line) ? line.Trim().Substring(1).Trim() : null;
        }

        return settings;
    }

    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith('#') || trimmed.StartsWith(';');
    }

    private static EditorConfigSetting? ReadSetting(string? section, string line, string? commentAbove, int lineNumber)
    {
        if(section == null || IsComment(line))
            return null;

        var property = Property().Match(line);
        var key = SeverityKey().Match(property.Success ? property.Groups[1].Value : string.Empty);
        if(!key.Success)
            return null;

        var id = key.Groups["id"].Value;
        var value = property.Groups[2].Value;
        var commentStart = value.AsSpan().IndexOfAny(_commentMarks);
        if(commentStart < 0)
            return new EditorConfigSetting(section, id, value, null, commentAbove, lineNumber);

        return new EditorConfigSetting(section, id, value.Substring(0, commentStart).TrimEnd(), value.Substring(commentStart + 1).Trim(), commentAbove, lineNumber);
    }
}
