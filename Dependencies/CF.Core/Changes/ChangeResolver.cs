using CF.Core.Code;
using CF.Core.Ports;

namespace CF.Core.Changes;

/// <summary>
/// Establishes what a scope changes, from git's objects alone. When it cannot, it
/// says why and never guesses.
/// </summary>
public sealed class ChangeResolver
{
    private readonly IGit _git;
    private readonly IReadOnlyList<ILanguage> _languages;

    /// <summary>
    /// Creates the resolver.
    /// </summary>
    public ChangeResolver(IGit git, IReadOnlyList<ILanguage> languages)
    {
        _git = git;
        _languages = languages;
    }

    /// <summary>
    /// One change set for each pushed ref, or one for the staged content or the range. There is always at least one; a
    /// change set that has nothing to review says why.
    /// </summary>
    /// <exception cref="ScopeException">The scope cannot be established.</exception>
    /// <exception cref="ReviewTargetException">The folder or project file of a whole review cannot be used.</exception>
    public IReadOnlyList<ChangeSet> Resolve(RepositoryLocation repository, ReviewScope scope)
    {
        try
        {
            return ResolveScope(repository, scope);
        }
        catch(GitException exception)
        {
            throw new ScopeException(exception.Message);
        }
    }

    private IReadOnlyList<ChangeSet> ResolveScope(RepositoryLocation repository, ReviewScope scope)
    {
        var builder = new ChangeSetBuilder(_git, repository, _languages);
        switch(scope.Kind)
        {
            case ScopeKind.Push:
                return new PushChangeResolver(_git, repository, scope.RemoteName, builder).Resolve(scope.PushedRefs);
            case ScopeKind.Staged:
                return new List<ChangeSet> { ResolveStaged(repository, builder) };
            case ScopeKind.Range:
                return new List<ChangeSet> { ResolveRange(repository, builder, scope) };
            case ScopeKind.WholeFolder:
                return new List<ChangeSet> { new WholeFolderResolver(_git, repository, builder, _languages).Resolve(scope.TargetPath) };
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope.Kind, "Unknown scope.");
        }
    }

    private ChangeSet ResolveStaged(RepositoryLocation repository, ChangeSetBuilder builder)
    {
        var staged = _git.WriteStagedTree(repository);
        var before = _git.ResolveTree(repository, "HEAD") ?? _git.EmptyTree(repository);

        return builder.Build("staged changes", before, staged);
    }

    private ChangeSet ResolveRange(RepositoryLocation repository, ChangeSetBuilder builder, ReviewScope scope)
    {
        var baseCommit = _git.ResolveCommit(repository, scope.BaseRevision)
            ?? throw new ScopeException($"'{scope.BaseRevision}' is not a revision of this repository");
        var headCommit = _git.ResolveCommit(repository, scope.HeadRevision)
            ?? throw new ScopeException($"'{scope.HeadRevision}' is not a revision of this repository");
        var tree = _git.ResolveTree(repository, headCommit)
            ?? throw new ScopeException($"'{scope.HeadRevision}' has no tree");

        return builder.Build($"{scope.BaseRevision}..{scope.HeadRevision}", baseCommit, tree);
    }
}