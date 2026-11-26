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
        : this(message, timedOut: false)
    {
    }

    /// <summary>
    /// Creates the exception with what went wrong and whether the operation ran out of time.
    /// </summary>
    public GitException(string message, bool timedOut)
        : base(message)
    {
        TimedOut = timedOut;
    }

    /// <summary>
    /// Whether git ran out of time.
    /// </summary>
    public bool TimedOut { get; }
}
