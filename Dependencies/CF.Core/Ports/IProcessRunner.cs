namespace CF.Core.Ports;

/// <summary>
/// Starting other programs, behind a port so that Core starts no process itself.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs the program, waits for it to end and returns what it wrote.
    /// </summary>
    /// <remarks>
    /// A program that cannot be started gives exit code -1; one that is still running when its time limit passes is
    /// stopped with everything it started and gives exit code -2 and TimedOut.
    /// </remarks>
    ProcessResult Run(ProcessRequest request);
}