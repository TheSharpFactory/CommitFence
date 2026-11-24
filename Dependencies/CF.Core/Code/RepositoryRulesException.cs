namespace CF.Core.Code;

/// <summary>
/// A file that sets rules exists and cannot be read. The message is plain.
/// </summary>
public sealed class RepositoryRulesException : Exception
{
    /// <summary>
    /// Creates the exception with the plain reason.
    /// </summary>
    public RepositoryRulesException(string message)
        : base(message)
    {
    }
}
