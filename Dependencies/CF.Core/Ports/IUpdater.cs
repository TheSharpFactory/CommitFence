using System.Diagnostics;
using CF.Core.Updates;

namespace CF.Core.Ports;

/// <summary>
/// Updating the program in place: asking for the latest release, downloading and checking it, and replacing the
/// program's files with it.
/// </summary>
public interface IUpdater
{
    /// <summary>
    /// The version of the running program.
    /// </summary>
    Version CurrentVersion { get; }

    /// <summary>
    /// Asks for the latest release and compares its version with this one. A failure is a result, never an exception.
    /// </summary>
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The reason the program cannot install this release here, for the user (a copy that is not a release download,
    /// a release without a download for this system); null when it can.
    /// </summary>
    string? FindProblem(ReleaseInfo release);

    /// <summary>
    /// Downloads the release, checks it, and replaces the program's files with it. <paramref name="progress" /> and
    /// <paramref name="cancellation" /> may be null. Throws <see cref="UpdateException" /> with a message for the user
    /// when a step fails, in which case the program is as it was.
    /// </summary>
    /// <exception cref="OperationCanceledException">The update was cancelled.</exception>
    Task InstallAsync(ReleaseInfo release, IProgress<UpdateProgress>? progress, UpdateCancellation? cancellation);

    /// <summary>
    /// Deletes what an earlier update left in the program's folder. A file of the old program that is still running
    /// cannot be deleted on Windows, so this tries <paramref name="attempts" /> times, a second apart.
    /// </summary>
    Task CleanUpAsync(int attempts);

    /// <summary>
    /// How to start this program again from where it is, with the argument if there is one (the folder it works on).
    /// </summary>
    ProcessStartInfo CreateStartInfo(string? argument);
}