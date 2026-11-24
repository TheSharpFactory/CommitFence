namespace CF.Core.Ports;

/// <summary>
/// The lock that makes two commands that change the same settings file take turns, so that neither loses the other's
/// edit.
/// </summary>
public interface ISettingsLock
{
    /// <summary>
    /// Waits for the lock of this settings file and takes it. Disposing the result gives it back. Throws
    /// <see cref="TimeoutException" /> when it cannot be had within a bounded time.
    /// </summary>
    IDisposable Take(string settingsPath);
}