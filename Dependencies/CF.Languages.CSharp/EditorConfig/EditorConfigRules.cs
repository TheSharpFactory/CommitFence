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

        var files = new CSharpFileSet(_fileSystem, repositoryFolder);
        var patterns = new Dictionary<string, SectionPattern>(StringComparer.Ordinal);
        var settings = EditorConfigParser.Parse(text);
        foreach(var section in settings.Select(setting => setting.Section).Distinct(StringComparer.Ordinal))
        {
            var pattern = new SectionPattern(section);
            if(files.Matches(pattern))
                patterns.Add(section, pattern);
        }

        var titles = patterns.Count == 0 ? new DiagnosticTitles() : DiagnosticTitles.Load(_fileSystem, _sdkFolder.Find(repositoryFolder));
        var lines = FileLines(text);
        var rules = settings.Where(setting => patterns.ContainsKey(setting.Section)).GroupBy(setting => setting.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => ToRule(group, patterns, files, titles, lines)).OrderBy(rule => rule.Id, StringComparer.Ordinal).ToList();
        return new FileRuleListing(_languageId, _fileName, rules, notes);
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

    private static List<string> FileLines(string text)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        if(lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        return lines;
    }

    private static FileRule ToRule(IEnumerable<EditorConfigSetting> settings, Dictionary<string, SectionPattern> patterns, CSharpFileSet files, DiagnosticTitles titles, IReadOnlyList<string> lines)
    {
        FileRulePlace? main = null;
        var exceptions = new List<FileRulePlace>();
        var primary = settings.LastOrDefault(setting => CSharpFileSet.NamesAll(patterns[setting.Section]));
        foreach(var setting in settings.Where(setting => primary == null || setting.Line >= primary.Line))
        {
            var pattern = patterns[setting.Section];
            var place = new FileRulePlace(setting.Section, setting.Value, FileRulePlace.StateOf(setting.Value), null, setting.Line,
                Excerpt(lines, setting.Header, setting.Line), pattern.Matches);
            if(setting == primary)
                main = place;
            else if(main == null || IsDifferentSetting(main, place) || (exceptions.Count > 0 && files.Overlaps(pattern, exceptions)))
                exceptions.Add(place);
        }

        var named = primary ?? settings.First();
        var analyzerTitle = titles.Find(named.Id);
        var title = analyzerTitle ?? CommentTitle.From(named.TrailingComment) ?? CommentTitle.From(named.CommentAbove);

        // A missing title is not evidence that nothing checks the ID: the compiler's own IDs are never titled, and an
        // analyzer package of the build may report others.
        var undescribed = analyzerTitle == null && !IsCompilerId(named.Id);
        return new FileRule(named.Id, title, _fileName, "C#", main, exceptions) { IsUndescribed = undescribed };
    }

    private static bool IsCompilerId(string id)
    {
        return id.Length > 2 && id.StartsWith("CS", StringComparison.OrdinalIgnoreCase) && id.Skip(2).All(char.IsAsciiDigit);
    }

    private static bool IsDifferentSetting(FileRulePlace main, FileRulePlace place)
    {
        if(main.State == FileRuleState.On || place.State == FileRuleState.On)
            return !string.Equals(main.Setting, place.Setting, StringComparison.OrdinalIgnoreCase);

        return main.State != place.State;
    }

    private static List<FileLine> Excerpt(IReadOnlyList<string> lines, int header, int setting)
    {
        var numbers = setting - header > 3 ? [header, setting] : Enumerable.Range(header, setting - header + 1);
        return numbers.Select(number => new FileLine(number, lines[number - 1])).ToList();
    }

    private void AddSubFolderNote(List<string> notes, string repositoryFolder)
    {
        var topFile = Path.GetFullPath(Path.Combine(repositoryFolder, _fileName));
        var count = _fileSystem.FindFiles(repositoryFolder, _fileName, _skippedFolders).Count(path => Path.GetFileName(path) == _fileName && Path.GetFullPath(path) != topFile);
        if(count > 0)
            notes.Add($"{count} {_fileName} {(count == 1 ? "file" : "files")} in sub-folders {(count == 1 ? "is" : "are")} not read.");
    }
}