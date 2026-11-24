namespace CF.Core.Ports;

/// <summary>
/// Where a git repository is on the machine: the folder the developer works in and git's own folder.
/// </summary>
public sealed class RepositoryLocation
{
    /// <summary>
    /// Creates a location.
    /// </summary>
    public RepositoryLocation(string workFolder, string gitDirectory)
    {
        WorkFolder = workFolder;
        GitDirectory = gitDirectory;
    }

    /// <summary>
    /// The top folder of the developer's working tree.
    /// </summary>
    public string WorkFolder { get; }

    /// <summary>
    /// The folder that holds git's objects, refs and index, as an absolute path.
    /// </summary>
    public string GitDirectory { get; }
}