namespace CF.Core.Ports;

/// <summary>
/// The lock that keeps a review and an update of the program from overlapping: a review holds it shared for as long as
/// it runs, and an update takes it alone before it replaces the files.
/// </summary>
public interface IProgramLock
{
    /// <summary>
    /// Takes the shared lock for a review; disposing the result gives it back. Waits while an update replaces the files.
    /// Throws <see cref="TimeoutException" /> when it cannot be had within a bounded time.
    /// </summary>
    IDisposable HoldForReview();
}