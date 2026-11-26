namespace CF.Core.Ports;

/// <summary>
/// Which files the working folder has changed, and which of those changes are staged. It reads git's status without taking a lock
/// or touching the index.
/// </summary>
public interface IWorkingStatus
{
    /// <summary>
    /// The status of the whole repository. Throws <see cref="GitException" /> when git cannot be run.
    /// </summary>
    WorkingStatus Read(RepositoryLocation repository);
}

/// <summary>
/// The files whose changes are staged, the files that have changes that are not staged (a new file git does not track counts), and
/// the files that are in both lists.
/// </summary>
/// <param name="Staged">Paths from the top of the repository, written with forward slashes.</param>
/// <param name="Unstaged">Paths with changes that are not staged.</param>
public sealed record WorkingStatus(IReadOnlyList<string> Staged, IReadOnlyList<string> Unstaged)
{
    /// <summary>
    /// Whether a staged file also has changes that are not staged.
    /// </summary>
    public bool StagedFileHasUnstagedChanges
    {
        get { return Staged.Any(path => Unstaged.Contains(path, StringComparer.Ordinal)); }
    }
}
