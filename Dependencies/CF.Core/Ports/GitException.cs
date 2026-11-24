namespace CF.Core.Ports;

/// <summary>
/// Git could not be run, or refused what it was asked. The message says what git said.
/// </summary>
public sealed class GitException : Exception
{
    /// <summary>
    /// Creates the exception with what went wrong.
    /// </summary>
    public GitException(string message)
        : base(message)
    {
    }
}