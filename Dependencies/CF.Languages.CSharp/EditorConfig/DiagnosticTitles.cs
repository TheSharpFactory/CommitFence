using System.Text.RegularExpressions;
using CF.Core.Ports;

namespace CF.Languages.CSharp.EditorConfig;

/// <summary>
/// The titles of the analyzer and style rules, read from the two files the installed SDK carries: a comment above each
/// rule, such as "# CA1000: Do not declare static members on generic types". Nothing is kept on disk.
/// </summary>
internal sealed partial class DiagnosticTitles
{
    private readonly Dictionary<string, string> _titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^analysislevel_(?<level>\d+)_all\.globalconfig$", RegexOptions.IgnoreCase)]
    private static partial Regex AnalysisLevelFile();

    [GeneratedRegex(@"^#\s*(?<id>[A-Za-z]+\d+):\s*(?<title>.+?)\s*$")]
    private static partial Regex TitleComment();

    /// <summary>
    /// The titles of an SDK folder; none when the folder is missing or lacks the files.
    /// </summary>
    public static DiagnosticTitles Load(IFileSystem fileSystem, string? sdkFolder)
    {
        var titles = new DiagnosticTitles();
        if(sdkFolder == null)
            return titles;

        var sdk = Path.Combine(sdkFolder, "Sdks", "Microsoft.NET.Sdk");
        titles.AddFrom(ReadText(fileSystem, NewestAnalysisLevelFile(fileSystem, Path.Combine(sdk, "analyzers", "build", "config"))));
        titles.AddFrom(ReadText(fileSystem, Path.Combine(sdk, "codestyle", "cs", "build", "config", "analysislevelstyle_all.globalconfig")));

        return titles;
    }

    /// <summary>
    /// The title of the rule, or nothing when the SDK does not give one.
    /// </summary>
    public string? Find(string id)
    {
        return _titles.TryGetValue(id, out var title) ? title : null;
    }

    private static string? NewestAnalysisLevelFile(IFileSystem fileSystem, string configFolder)
    {
        try
        {
            return fileSystem.FindFiles(configFolder, ".globalconfig", new List<string>())
                .Select(path => new { File = path, Match = AnalysisLevelFile().Match(Path.GetFileName(path)) })
                .Where(file => file.Match.Success)
                .OrderByDescending(file => int.Parse(file.Match.Groups["level"].Value, System.Globalization.CultureInfo.InvariantCulture))
                .Select(file => file.File)
                .FirstOrDefault();
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadText(IFileSystem fileSystem, string? path)
    {
        if(path == null)
            return null;

        try
        {
            return fileSystem.ReadAllText(path);
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void AddFrom(string? text)
    {
        if(text == null)
            return;

        foreach(var line in text.Split('\n'))
        {
            var match = TitleComment().Match(line.Trim());
            var title = match.Success ? CommentTitle.From(match.Groups["title"].Value) : null;
            if(title != null)
                _titles[match.Groups["id"].Value] = title;
        }
    }
}
