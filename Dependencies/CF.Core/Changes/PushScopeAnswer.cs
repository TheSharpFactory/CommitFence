namespace CF.Core.Changes;

/// <summary>
/// What a push of the current branch would send: its scope, or the plain reason there is none.
/// </summary>
public sealed class PushScopeAnswer
{
    private PushScopeAnswer(ReviewScope? scope, string branch, string remoteName, string reason)
    {
        Scope = scope;
        Branch = branch;
        RemoteName = remoteName;
        Reason = reason;
    }

    /// <summary>
    /// The scope the hook would be given, or <c>null</c> when there is no push to review.
    /// </summary>
    public ReviewScope? Scope { get; }

    /// <summary>
    /// The branch that would be pushed, empty when there is none.
    /// </summary>
    public string Branch { get; }

    /// <summary>
    /// The remote the branch would go to, empty when there is none.
    /// </summary>
    public string RemoteName { get; }

    /// <summary>
    /// Why there is no push to review, empty when there is one.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Whether there is a push to review.
    /// </summary>
    public bool HasPush
    {
        get { return Scope != null; }
    }

    /// <summary>
    /// A push that can be reviewed.
    /// </summary>
    public static PushScopeAnswer Found(ReviewScope scope, string branch, string remoteName)
    {
        return new PushScopeAnswer(scope, branch, remoteName, string.Empty);
    }

    /// <summary>
    /// No push to review, and why.
    /// </summary>
    public static PushScopeAnswer None(string reason, string branch)
    {
        return new PushScopeAnswer(null, branch, string.Empty, reason);
    }
}
