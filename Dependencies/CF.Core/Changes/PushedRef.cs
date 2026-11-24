namespace CF.Core.Changes;

/// <summary>
/// One line git gives a pre-push hook: the local ref and object being pushed, and the remote ref and the object the
/// remote has there.
/// </summary>
public sealed class PushedRef
{
    private const char _zeroDigit = '0';

    /// <summary>
    /// Creates a pushed ref.
    /// </summary>
    public PushedRef(string localRef, string localObject, string remoteRef, string remoteObject)
    {
        LocalRef = localRef;
        LocalObject = localObject;
        RemoteRef = remoteRef;
        RemoteObject = remoteObject;
    }

    /// <summary>
    /// The local ref, or (delete) when the remote ref is being deleted.
    /// </summary>
    public string LocalRef { get; }

    /// <summary>
    /// The object being pushed, all zeros for a deletion.
    /// </summary>
    public string LocalObject { get; }

    /// <summary>
    /// The ref on the remote.
    /// </summary>
    public string RemoteRef { get; }

    /// <summary>
    /// The object the remote has at that ref, all zeros when the ref is new.
    /// </summary>
    public string RemoteObject { get; }

    /// <summary>
    /// Whether the push deletes the remote ref.
    /// </summary>
    public bool IsDeletion
    {
        get { return IsZero(LocalObject); }
    }

    /// <summary>
    /// Whether the remote does not have the ref yet.
    /// </summary>
    public bool IsNewOnRemote
    {
        get { return IsZero(RemoteObject); }
    }

    private static bool IsZero(string objectId)
    {
        return objectId.All(digit => digit == _zeroDigit);
    }
}