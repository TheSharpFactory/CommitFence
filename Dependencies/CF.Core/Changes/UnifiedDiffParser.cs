using System.Text;
using System.Text.RegularExpressions;

namespace CF.Core.Changes;

/// <summary>
/// Reads the unified diff git writes (with no context lines and with rename detection) into the files it changes and
/// the lines of each that changed.
/// </summary>
public static class UnifiedDiffParser
{
    private const string _filePrefix = "diff --git ";
    private const string _hunkPrefix = "@@ ";
    private const string _newFilePrefix = "+++ ";
    private const string _oldFilePrefix = "--- ";
    private const string _renameFromPrefix = "rename from ";
    private const string _renameToPrefix = "rename to ";
    private const string _newFileModePrefix = "new file mode";
    private const string _deletedFileModePrefix = "deleted file mode";
    private const string _noFile = "/dev/null";
    private const string _binaryPrefix = "Binary files ";
    private const char _backslash = '\\';

    private static readonly Regex _hunkPattern = new Regex(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@", RegexOptions.CultureInvariant);

    /// <summary>
    /// Reads the diff. The function gives the identifier of the language that owns a path, or nothing.
    /// </summary>
    public static IReadOnlyList<ChangedFile> Parse(string diff, Func<string, string?> languageOf)
    {
        var files = new List<ChangedFile>();
        DiffFileBuilder? current = null;
        foreach(var rawLine in diff.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if(line.StartsWith(_filePrefix, StringComparison.Ordinal))
            {
                AddFinished(files, current, languageOf);
                current = new DiffFileBuilder(ReadPathOfHeader(line));
                continue;
            }

            if(current != null)
                ReadLine(current, line);
        }

        AddFinished(files, current, languageOf);
        return files;
    }

    private static void ReadLine(DiffFileBuilder current, string line)
    {
        if(current.InHunk)
        {
            CountHunkLine(current, line);
            return;
        }

        if(line.StartsWith(_newFileModePrefix, StringComparison.Ordinal))
            current.Status = ChangeStatus.Added;
        else if(line.StartsWith(_deletedFileModePrefix, StringComparison.Ordinal))
            current.Status = ChangeStatus.Deleted;
        else if(line.StartsWith(_renameFromPrefix, StringComparison.Ordinal))
            current.RenamedFrom = UnquotePath(line.Substring(_renameFromPrefix.Length));
        else if(line.StartsWith(_renameToPrefix, StringComparison.Ordinal))
            ReadRename(current, line);
        else if(line.StartsWith(_oldFilePrefix, StringComparison.Ordinal))
            ReadOldFile(current, line);
        else if(line.StartsWith(_newFilePrefix, StringComparison.Ordinal))
            ReadNewFile(current, line);
        else if(line.StartsWith(_hunkPrefix, StringComparison.Ordinal))
            ReadHunk(current, line);
        else if(line.StartsWith(_binaryPrefix, StringComparison.Ordinal))
            current.IsWithoutLines = true;
    }

    private static void CountHunkLine(DiffFileBuilder current, string line)
    {
        if(line.StartsWith('\\'))
            return;

        if(!line.StartsWith('+'))
            current.OldLinesLeft--;

        if(!line.StartsWith('-'))
            current.NewLinesLeft--;
    }

    private static void ReadRename(DiffFileBuilder current, string line)
    {
        current.Status = ChangeStatus.Renamed;
        current.Path = UnquotePath(line.Substring(_renameToPrefix.Length));
    }

    private static void ReadOldFile(DiffFileBuilder current, string line)
    {
        var text = ReadPathText(line.Substring(_oldFilePrefix.Length));
        if(text != _noFile)
            current.OldPath = StripPrefix(UnquotePath(text));
    }

    private static void ReadNewFile(DiffFileBuilder current, string line)
    {
        var text = ReadPathText(line.Substring(_newFilePrefix.Length));
        if(text == _noFile)
        {
            current.Status = ChangeStatus.Deleted;
            current.Path = current.OldPath ?? current.Path;
            return;
        }

        current.Path = StripPrefix(UnquotePath(text));
    }

    private static void ReadHunk(DiffFileBuilder current, string line)
    {
        var match = _hunkPattern.Match(line);
        if(!match.Success)
            return;

        current.OldLinesLeft = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1;
        var newStart = int.Parse(match.Groups[3].Value);
        var newCount = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 1;
        current.NewLinesLeft = newCount;
        if(newCount == 0)
            current.ChangedLines.Add(LineRange.ForRemovalAfter(newStart));
        else
            current.ChangedLines.Add(LineRange.ForLines(newStart, newStart + newCount - 1));
    }

    private static void AddFinished(List<ChangedFile> files, DiffFileBuilder? current, Func<string, string?> languageOf)
    {
        if(current == null)
            return;

        var previousPath = current.Status == ChangeStatus.Renamed ? current.RenamedFrom : null;
        if(current.IsWithoutLines && current.Status != ChangeStatus.Deleted)
            current.ChangedLines.Add(LineRange.ForWholeFile());

        files.Add(new ChangedFile(current.Path, languageOf(current.Path), current.Status, current.ChangedLines, previousPath));
    }

    private static string ReadPathOfHeader(string line)
    {
        var text = line.Substring(_filePrefix.Length);
        var separator = text.LastIndexOf(" b/", StringComparison.Ordinal);
        if(text.StartsWith('"') || separator < 0)
            return text;

        return ReadPathNamedTwice(text) ?? text.Substring(separator + 1).Substring(2);
    }

    /// <summary>
    /// A file that was not renamed is named twice in the header, as <c>a/path b/path</c>, so each half of the text is
    /// its name, also when the name itself holds " b/". Nothing when the two halves differ (a rename, whose lines give
    /// the names).
    /// </summary>
    /// <remarks>
    /// Worked example, for the file <c>src b/Foo.cs</c> (12 characters): the text after "diff --git " is
    /// <c>a/src b/Foo.cs b/src b/Foo.cs</c>, which is 29 characters. Cut at the last " b/" it would give <c>Foo.cs</c>,
    /// which is wrong. Half of 29 is 14, and the character at index 14 is the one space between the two names:
    /// the 14 characters before it are <c>a/src b/Foo.cs</c> and the 14 after it are <c>b/src b/Foo.cs</c>. Without its
    /// "a/" the first half is <c>src b/Foo.cs</c>, and the second half is "b/" and that same name, so the text is
    /// such a header. Two equal names of n characters take 2n + 5 characters ("a/", "b/" and the space), which is
    /// always odd, so a text of even length cannot be such a header and gives nothing.
    /// </remarks>
    private static string? ReadPathNamedTwice(string text)
    {
        var half = text.Length / 2;
        if(text.Length % 2 == 0 || text[half] != ' ' || !text.StartsWith("a/", StringComparison.Ordinal))
            return null;

        var first = text.Substring(2, half - 2);
        return text.Substring(half + 1) == "b/" + first ? first : null;
    }

    private static string ReadPathText(string text)
    {
        var tab = text.IndexOf('\t');
        return tab < 0 ? text : text.Substring(0, tab);
    }

    private static string StripPrefix(string path)
    {
        return path.Length > 2 && path[1] == '/' ? path.Substring(2) : path;
    }

    private static string UnquotePath(string text)
    {
        if(text.Length < 2 || !text.StartsWith('"') || !text.EndsWith('"'))
            return text;

        var bytes = new List<byte>();
        var inner = text.Substring(1, text.Length - 2);
        var index = 0;
        while(index < inner.Length)
            index = AppendUnquoted(bytes, inner, index);

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static int AppendUnquoted(List<byte> bytes, string inner, int index)
    {
        if(inner[index] != _backslash || index + 1 >= inner.Length)
        {
            var runEnd = inner.IndexOf(_backslash, index + 1);
            var length = (runEnd < 0 ? inner.Length : runEnd) - index;
            bytes.AddRange(Encoding.UTF8.GetBytes(inner.Substring(index, length)));
            return index + length;
        }

        var escaped = inner[index + 1];
        if(char.IsAsciiDigit(escaped) && index + 3 < inner.Length)
        {
            bytes.Add(Convert.ToByte(inner.Substring(index + 1, 3), 8));
            return index + 4;
        }

        bytes.Add(UnescapeCharacter(escaped));
        return index + 2;
    }

    private static byte UnescapeCharacter(char escaped)
    {
        switch(escaped)
        {
            case 't':
                return (byte)'\t';
            case 'n':
                return (byte)'\n';
            default:
                return (byte)escaped;
        }
    }
}