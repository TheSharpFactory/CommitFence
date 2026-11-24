namespace CF.Core.Ports;

/// <summary>
/// The remote and the branch of that remote that a local branch is set to follow.
/// </summary>
public sealed class BranchUpstream
{
    /// <summary>
    /// Creates the answer.
    /// </summary>
    public BranchUpstream(string remoteName, string remoteRef)
    {
        RemoteName = remoteName;
        RemoteRef = remoteRef;
    }

    /// <summary>
    /// The name of the remote, such as <c>origin</c>.
    /// </summary>
    public string RemoteName { get; }

    /// <summary>
    /// The full name of the branch on that remote, such as <c>refs/heads/main</c>.
    /// </summary>
    public string RemoteRef { get; }
}
