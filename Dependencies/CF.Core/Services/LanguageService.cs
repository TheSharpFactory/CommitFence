using CF.Core.Code;
using CF.Core.Ports;

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
