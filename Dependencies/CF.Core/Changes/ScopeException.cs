namespace CF.Core.Changes;

/// <summary>
/// The scope of a review could not be established, or the tree under review could not be prepared. The review is then
/// incomplete, never a pass.
/// </summary>
public sealed class ScopeException : Exception
{
    /// <summary>
    /// Creates the exception with the reason.
    /// </summary>
    public ScopeException(string message)
        : base(message)
    {
    }
}