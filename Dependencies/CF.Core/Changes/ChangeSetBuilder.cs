using CF.Core.Code;
using CF.Core.Ports;

namespace CF.Core.Changes;

/// <summary>
/// Turns the difference between two trees, or the files of one tree, into a change set: gives each file its language
/// and notes the rule files.
/// </summary>
internal sealed class ChangeSetBuilder
{
    private static readonly string[] _ruleFileNames = { ".editorconfig", "commitfence.json" };

    private readonly IGit _git;
    private readonly RepositoryLocation _repository;
    private readonly IReadOnlyList<ILanguage> _languages;

    public ChangeSetBuilder(IGit git, RepositoryLocation repository, IReadOnlyList<ILanguage> languages)
    {
        _git = git;
        _repository = repository;
        _languages = languages;
    }

    public ChangeSet Build(string label, string before, string afterTree)
    {
        var diff = _git.ReadDiff(_repository, before, afterTree);
        var files = ReadAsTextWhatGitGaveNoLinesFor(UnifiedDiffParser.Parse(diff, FindLanguage), before, afterTree);
        var noun = files.Count == 1 ? "file" : "files";
        var beforeTree = _git.ResolveTree(_repository, before) ?? _git.EmptyTree(_repository);

        return ChangeSet.ForChanges(label, beforeTree, afterTree, files, FindRuleFiles(files), $"{label}: {files.Count} changed {noun}.");
    }

    /// <summary>
    /// The files of the tree that are given, each an added file with every line touched. There is no diff to read: a
    /// whole review has no tree before.
    /// </summary>
    public ChangeSet BuildWhole(string label, string commit, string afterTree, IReadOnlyList<string> paths, string folder)
    {
        var files = paths.Select(path => new ChangedFile(path, FindLanguage(path), ChangeStatus.Added, new List<LineRange> { LineRange.ForWholeFile() })).ToList();
        var noun = files.Count == 1 ? "file" : "files";
        var note = $"{label} at commit {commit[..Math.Min(10, commit.Length)]}: {files.Count} {noun}.";

        return ChangeSet.ForWholeReview(label, _git.EmptyTree(_repository), afterTree, files, note, folder);
    }

    /// <summary>
    /// A file a language owns that git wrote without lines (the repository says not to compare it, or git takes it for
    /// binary) is read once more as text, so that it behaves as any other file: what the change touched is judged, an
    /// old breach is let go. Files no language owns are never read as text. A file the second reading does not give stays
    /// touched on every line: more is reviewed, never less.
    /// </summary>
    private IReadOnlyList<ChangedFile> ReadAsTextWhatGitGaveNoLinesFor(IReadOnlyList<ChangedFile> files, string before, string after)
    {
        var withoutLines = files.Where(file => file.Language != null && file.Status != ChangeStatus.Deleted && file.ChangedLines.Any(range => range.IsWholeFile)).ToList();
        if(withoutLines.Count == 0)
            return files;

        // a renamed file is asked for with the name it left too, so that git still sees a rename and not a new file
        var namesOfEachFile = withoutLines.Select(file => (IReadOnlyList<string>)new[] { file.Path, file.PreviousPath }.OfType<string>().ToList()).ToList();
        var diff = _git.ReadDiffAsText(_repository, before, after, namesOfEachFile);
        var read = UnifiedDiffParser.Parse(diff, FindLanguage);

        return files.Select(file => withoutLines.Contains(file) ? WithLinesOf(file, read) : file).ToList();
    }

    private static ChangedFile WithLinesOf(ChangedFile file, IReadOnlyList<ChangedFile> read)
    {
        var found = read.FirstOrDefault(candidate => string.Equals(candidate.Path, file.Path, StringComparison.Ordinal));
        if(found == null || found.ChangedLines.Count == 0 || found.ChangedLines.Any(range => range.IsWholeFile))
            return file;

        return new ChangedFile(file.Path, file.Language, file.Status, found.ChangedLines, file.PreviousPath);
    }

    private static List<string> FindRuleFiles(IReadOnlyList<ChangedFile> files)
    {
        return files.Where(file => IsRuleFile(file.Path)).Select(file => file.Path).ToList();
    }

    private string? FindLanguage(string path)
    {
        return _languages.FirstOrDefault(language => language.OwnsFile(path))?.Id;
    }

    private static bool IsRuleFile(string path)
    {
        var name = path.Substring(path.LastIndexOf('/') + 1);
        return _ruleFileNames.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}
