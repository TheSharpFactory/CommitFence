namespace CF.Core.Ports;

/// <summary>
/// The lock that keeps two reviews of one repository from using its private copy at the same time.
/// </summary>
public interface IReviewLock
{
    /// <summary>
    /// Waits for the lock of this repository and takes it. Disposing the result gives it back. Throws
    /// <see cref="TimeoutException" /> when it cannot be had within a bounded time.
    /// </summary>
    IDisposable Acquire(RepositoryLocation repository);
}