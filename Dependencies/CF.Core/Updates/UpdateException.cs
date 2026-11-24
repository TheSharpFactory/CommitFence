namespace CF.Core.Updates;

/// <summary>
/// An update step that failed, with a message for the user.
/// </summary>
public sealed class UpdateException : Exception
{
    /// <summary>
    /// Creates the exception with the message the user is shown.
    /// </summary>
    public UpdateException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with the message the user is shown and the failure behind it.
    /// </summary>
    public UpdateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
