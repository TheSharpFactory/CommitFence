namespace CF.Core.Changes;

/// <summary>
/// The folder or project file named for a whole review cannot be used: it is outside the repository, it is not in the
/// last commit, or it is neither a folder nor a project file. This is wrong use of the command, not an incomplete
/// review.
/// </summary>
public sealed class ReviewTargetException : Exception
{
    /// <summary>
    /// Creates the exception with the reason.
    /// </summary>
    public ReviewTargetException(string message)
        : base(message)
    {
    }
}