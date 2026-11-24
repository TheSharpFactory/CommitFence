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
}