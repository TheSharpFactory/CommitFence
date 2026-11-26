using CF.Core.Code;
using CF.Core.Ports;
using CF.Core.Structure;

namespace CF.Core.Services;

/// <inheritdoc cref="ILanguageService" />
public sealed class LanguageService : ILanguageService
{
    private readonly IGit _git;
    private readonly IReadOnlyList<ILanguage> _languages;

    /// <summary>Creates the service for the installed languages.</summary>
    public LanguageService(IGit git, IReadOnlyList<ILanguage> languages)
    {
        _git = git;
        _languages = languages;
    }

    /// <inheritdoc />
    public IReadOnlyList<LanguageReport> Show(string folder)
    {
        var paths = PathsOfLastCommit(folder);
        return _languages.Select(language => new LanguageReport(language.Name, language.FileEndings, paths?.Count(language.OwnsFile), language.FullReviewNote)).ToList();
    }

    /// <inheritdoc />
    public PathMatches? FindFiles(string folder, string languageId, string pattern)
    {
        var language = _languages.FirstOrDefault(candidate => candidate.Id == languageId);
        var paths = language == null ? null : PathsOfLastCommit(folder);
        if(paths == null)
            return null;

        var patterns = new List<string> { pattern };
        return new PathMatches(paths.Where(path => language!.OwnsFile(path) && PathFilter.Matches(patterns, path)).ToList());
    }

    /// <summary>
    /// The files the last commit holds, or null when the folder is not in a repository, the repository has no commit, or git cannot be run.
    /// </summary>
    private IReadOnlyList<string>? PathsOfLastCommit(string folder)
    {
        try
        {
            var repository = _git.FindRepository(folder);
            var commit = repository == null ? null : _git.ResolveCommit(repository, "HEAD");
            var tree = commit == null ? null : _git.ResolveTree(repository!, commit);
            return tree == null ? null : _git.ListFiles(repository!, tree, string.Empty);
        }
        catch(GitException)
        {
            return null;
        }
    }
}
