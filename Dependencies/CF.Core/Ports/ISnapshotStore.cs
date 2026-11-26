namespace CF.Core.Ports;

/// <summary>
/// The private copy of a repository's files, kept in the product's own data folder.
/// It is never the developer's working folder.
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    /// Moves the private copy to the tree and returns its folder. Only what differs from the tree it held before is
    /// changed. Throws <see cref="GitException" /> when it cannot be done.
    /// </summary>
    string MoveTo(RepositoryLocation repository, string tree);

    /// <summary>Lists the tracked changes in the private copy.</summary>
    IReadOnlyList<string> ReadChangedFiles(RepositoryLocation repository);

    /// <summary>Builds a unified diff from two texts without using the repository's index or filters.</summary>
    string MakePatch(RepositoryLocation repository, string file, string before, string after);

    /// <summary>Lists files outside the private copy's index, including build outputs.</summary>
    IReadOnlyList<string> ReadUntrackedFiles(RepositoryLocation repository);
}
