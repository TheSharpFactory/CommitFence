namespace CF.Core.Ports;

/// <summary>
/// What a program did: how it ended and what it wrote.
/// </summary>
public sealed class ProcessResult
{
    /// <summary>
    /// Creates a result.
    /// </summary>
    public ProcessResult(int exitCode, string output, string error, bool timedOut = false)
    {
        ExitCode = exitCode;
        Output = output;
        Error = error;
        TimedOut = timedOut;
    }

    /// <summary>
    /// The exit code; -1 when the program could not be started, -2 when it did not end within its time limit.
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Everything written to standard output (what was written until the program was stopped, when it timed out).
    /// </summary>
    public string Output { get; }

    /// <summary>
    /// Everything written to standard error, or the reason the program could not be started or was stopped.
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// True when the program did not end within its time limit and was stopped, together with every process it started.
    /// </summary>
    /// <remarks>
    /// Its output and exit code then say nothing about the work: a caller must never take this for success.
    /// </remarks>
    public bool TimedOut { get; }
}