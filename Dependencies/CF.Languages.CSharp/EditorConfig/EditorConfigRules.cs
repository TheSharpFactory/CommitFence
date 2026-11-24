using CF.Core.Code;
using CF.Core.Ports;
using CF.Languages.CSharp.Toolchain;

namespace CF.Languages.CSharp.EditorConfig;

/// <summary>
/// The rules the .editorconfig in the top folder of the repository sets for the compiler: its dotnet_diagnostic severity
/// lines, applied in the file's order, with titles from the installed SDK.
/// </summary>
/// <remarks>
/// Limits, stated and not silent: .editorconfig files in sub-folders and other ways of configuring diagnostics
/// (.globalconfig files, category settings, the :severity suffix of style options) are not read.
/// </remarks>
internal sealed class EditorConfigRules : IRepositoryRules
{
    private const string _languageId = "csharp";
    private const string _fileName = ".editorconfig";
    private const int _linesAround = 3;

    private static readonly IReadOnlyList<string> _skippedFolders = new List<string> { ".git", "bin", "obj", "node_modules" };

    private readonly IFileSystem _fileSystem;
    private readonly SdkFolder _sdkFolder;

    public EditorConfigRules(IFileSystem fileSystem, DotnetTool dotnet)
    {
        _fileSystem = fileSystem;
        _sdkFolder = new SdkFolder(dotnet);
    }

    public FileRuleListing Read(string repositoryFolder)
    {
        var notes = new List<string>();
        var text = ReadText(Path.Combine(repositoryFolder, _fileName));
        AddSubFolderNote(notes, repositoryFolder);
        if(text == null)
        {
            notes.Insert(0, $"There is no {_fileName} in this repository.");
            return new FileRuleListing(_languageId, null, new List<FileRule>(), notes);
        }

        var settings = AppliedSettings(EditorConfigParser.Parse(text), new CSharpFileSet(_fileSystem, repositoryFolder));
        var titles = settings.Count == 0 ? new DiagnosticTitles() : DiagnosticTitles.Load(_fileSystem, _sdkFolder.Find(repositoryFolder));

        var lines = FileLines(text);

        return new FileRuleListing(_languageId, _fileName, settings.Select(setting => ToRule(setting, titles, lines)).ToList(), notes);
    }

    private string? ReadText(string path)
    {
        try
        {
            return _fileSystem.ReadAllText(path);
        }
        catch(Exception exception) when(exception is FileNotFoundException || exception is DirectoryNotFoundException)
        {
            return null;
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            throw new RepositoryRulesException($"The {_fileName} of this repository cannot be read: {exception.Message}");
        }
    }

    private static List<EditorConfigSetting> AppliedSettings(IReadOnlyList<EditorConfigSetting> settings, CSharpFileSet files)
    {
        var matching = new Dictionary<string, bool>();
        var winners = new Dictionary<string, EditorConfigSetting>(StringComparer.OrdinalIgnoreCase);
        foreach(var setting in settings)
        {
            if(!matching.TryGetValue(setting.Section, out var matches))
                matching[setting.Section] = matches = files.Matches(new SectionPattern(setting.Section));

            if(matches)
                winners[setting.Id] = setting;
        }

        return winners.Values.OrderBy(setting => setting.Id, StringComparer.Ordinal).ToList();
    }

    private static List<string> FileLines(string text)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        if(lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        return lines;
    }

    private static List<FileLine> LinesAround(IReadOnlyList<string> lines, int line)
    {
        var first = Math.Max(1, line - _linesAround);
        var last = Math.Min(lines.Count, line + _linesAround);

        return Enumerable.Range(first, Math.Max(0, last - first + 1)).Select(number => new FileLine(number, lines[number - 1])).ToList();
    }

    private static FileRule ToRule(EditorConfigSetting setting, DiagnosticTitles titles, IReadOnlyList<string> lines)
    {
        var title = titles.Find(setting.Id) ?? CommentTitle.From(setting.TrailingComment) ?? CommentTitle.From(setting.CommentAbove);

        return new FileRule(setting.Id, title, setting.Value, SeverityWords.StateOf(setting.Value), SeverityWords.SeverityOf(setting.Value), _fileName, setting.Line, LinesAround(lines, setting.Line));
    }

    private void AddSubFolderNote(List<string> notes, string repositoryFolder)
    {
        var topFile = Path.GetFullPath(Path.Combine(repositoryFolder, _fileName));
        var count = _fileSystem.FindFiles(repositoryFolder, _fileName, _skippedFolders).Count(path => Path.GetFileName(path) == _fileName && Path.GetFullPath(path) != topFile);
        if(count > 0)
            notes.Add($"{count} {_fileName} {(count == 1 ? "file" : "files")} in sub-folders {(count == 1 ? "is" : "are")} not read.");
    }
}
