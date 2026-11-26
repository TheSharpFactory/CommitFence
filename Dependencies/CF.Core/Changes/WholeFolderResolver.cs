using CF.Core.Code;
using CF.Core.Ports;

namespace CF.Core.Changes;

/// <summary>
/// Turns the target of a whole review into one change set: the last commit's files under the target, read against
/// nothing.
/// </summary>
/// <remarks>
/// The target is a folder, the folder of a project file, or nothing for the whole repository. A project is judged by
/// its folder only: a project that compiles files from outside its own folder is judged for the files inside it, and
/// a second project in the same folder is judged too. The content is the last commit, never the working folder. A
/// target written in other letters than the tracked folder is taken for it when exactly one tracked folder or project
/// file differs from it in case alone.
/// </remarks>
internal sealed class WholeFolderResolver
{
    private readonly IGit _git;
    private readonly RepositoryLocation _repository;
    private readonly ChangeSetBuilder _builder;
    private readonly IReadOnlyList<ILanguage> _languages;

    public WholeFolderResolver(IGit git, RepositoryLocation repository, ChangeSetBuilder builder, IReadOnlyList<ILanguage> languages)
    {
        _languages = languages;
        _git = git;
        _repository = repository;
        _builder = builder;
    }

    /// <summary>
    /// The folder to review and the files the last commit holds under it.
    /// </summary>
    private sealed record Target(string Folder, IReadOnlyList<string> Paths);

    /// <exception cref="ScopeException">The repository has no commit.</exception>
    /// <exception cref="ReviewTargetException">The target cannot be used.</exception>
    public ChangeSet Resolve(string targetPath)
    {
        var relative = targetPath.Length == 0 ? string.Empty : RelativeToRepository(targetPath);
        var commit = _git.ResolveCommit(_repository, "HEAD")
            ?? throw new ScopeException("this repository has no commit yet, so there is nothing to review");
        var tree = _git.ResolveTree(_repository, commit)
            ?? throw new ScopeException("the last commit has no tree");

        var target = FindTarget(relative, tree);
        var label = "the whole of " + (target.Folder.Length == 0 ? "the repository" : target.Folder);
        var changeSet = _builder.BuildWhole(label, commit, tree, target.Paths, target.Folder);

        return NoteUncommittedWork(changeSet, target.Folder).WithCommit(commit);
    }

    /// <summary>
    /// The target as typed, or, when the commit holds nothing under it, the one tracked spelling that differs in letter
    /// case only.
    /// </summary>
    private Target FindTarget(string relative, string tree)
    {
        var folder = FolderOf(relative, tree);
        var paths = _git.ListFiles(_repository, tree, folder);
        if(folder.Length == 0 || paths.Count > 0)
            return new Target(folder, paths);

        var tracked = FolderOf(ChooseTrackedSpelling(relative, tree), tree);
        return new Target(tracked, _git.ListFiles(_repository, tree, tracked));
    }

    private ChangeSet NoteUncommittedWork(ChangeSet changeSet, string folder)
    {
        return _git.HasUncommittedWork(_repository, folder)
            ? changeSet.WithUncommittedWork("The working folder holds changes that are not committed; they are not part of this review.")
            : changeSet;
    }

    /// <summary>
    /// The folder to review: the target itself, or the folder of the project file it names.
    /// </summary>
    private string FolderOf(string relative, string tree)
    {
        if(relative.Length == 0 || _git.ReadFile(_repository, tree, relative) == null)
            return relative;

        if(!_languages.Any(language => language.Toolchain != null && language.Toolchain.IsProjectFile(relative)))
            throw new ReviewTargetException($"'{relative}' is a file but not a project file; name a folder or a project file");

        var bar = relative.LastIndexOf('/');
        return bar < 0 ? string.Empty : relative.Substring(0, bar);
    }

    /// <summary>
    /// The one tracked folder or file that has the target's path apart from case. With none the target has no files;
    /// with several the person is asked to name one exactly.
    /// </summary>
    private string ChooseTrackedSpelling(string relative, string tree)
    {
        var spellings = new HashSet<string>(StringComparer.Ordinal);
        foreach(var path in _git.ListFiles(_repository, tree, string.Empty).Where(candidate => IsAtOrUnder(candidate, relative)))
            spellings.Add(path.Substring(0, relative.Length));

        if(spellings.Count == 0)
            throw new ReviewTargetException($"'{relative}' has no files in the last commit (no tracked folder has this name in another letter case either), so there is nothing to review there");

        if(spellings.Count > 1)
            throw new ReviewTargetException($"'{relative}' matches several tracked paths apart from case: {string.Join(", ", spellings.Order(StringComparer.Ordinal).Select(spelling => $"'{spelling}'"))}; name one of them exactly");

        return spellings.First();
    }

    /// <summary>
    /// Whether the tracked path is the target or lies under it, letter case aside.
    /// </summary>
    private static bool IsAtOrUnder(string path, string relative)
    {
        if(!path.StartsWith(relative, StringComparison.OrdinalIgnoreCase))
            return false;

        return path.Length == relative.Length || path[relative.Length] == '/';
    }

    private string RelativeToRepository(string targetPath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(_repository.WorkFolder), Path.GetFullPath(targetPath));
        var outside = relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative);
        if(outside)
            throw new ReviewTargetException($"'{targetPath}' is outside the repository at {_repository.WorkFolder}");

        return relative == "." ? string.Empty : relative.Replace(Path.DirectorySeparatorChar, '/').Trim('/');
    }
}
