namespace CF.Core.Ports;

/// <summary>Read-only health states of the private checkout.</summary>
public enum PrivateCopyState
{
    /// <summary>No review has made a private checkout yet.</summary>
    Absent,

    /// <summary>The files and index agree.</summary>
    InOrder,

    /// <summary>The files and index are incomplete or disagree.</summary>
    OutOfOrder,

    /// <summary>A review currently holds the checkout.</summary>
    InUse,

    /// <summary>A review stopped and left the private index locked.</summary>
    IndexLocked
}
