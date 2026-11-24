using System.Diagnostics;

namespace CF.Adapters.Updates;

/// <summary>
/// The file in the program's folder that a review and an update lock: a review opens it shared and keeps it open while
/// it runs, an update opens it alone before it replaces the files. The operating system does not wait for such a lock,
/// so each side tries again at a short interval until a bounded time has passed.
/// </summary>
internal static class ProgramLockFile
{
    public const string FileName = ".program-lock";

    private static readonly TimeSpan _retryInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The path of the lock file in the program's folder.
    /// </summary>
    public static string PathIn(string folder)
    {
        return Path.Combine(folder, FileName);
    }

    /// <summary>
    /// Takes the lock shared, as a review does; null when an update held it alone for the whole of
    /// <paramref name="waitLimit" />. A folder that cannot be written to has no lock file and nothing can update it
    /// from here, so there it is given at once, as a lock that holds nothing.
    /// </summary>
    public static IDisposable? TryHoldShared(string folder, TimeSpan waitLimit)
    {
        // a stopwatch, not the time of day: the wait must not grow when the machine's clock is set back
        var waited = Stopwatch.StartNew();
        while(true)
        {
            try
            {
                return new FileStream(PathIn(folder), FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
            }
            catch(Exception exception) when(exception is UnauthorizedAccessException || exception is DirectoryNotFoundException)
            {
                return new NothingHeld();
            }
            catch(IOException)
            {
                // an update holds the file alone
            }

            if(waited.Elapsed > waitLimit)
                return null;

            Thread.Sleep(_retryInterval);
        }
    }

    /// <summary>
    /// Takes the lock alone, as an update does, once every running review has ended; null when one still ran after
    /// <paramref name="waitLimit" />. Cancel ends the wait with an <see cref="OperationCanceledException" />.
    /// </summary>
    public static async Task<FileStream?> TryHoldAloneAsync(string folder, TimeSpan waitLimit, CancellationToken cancellationToken)
    {
        var waited = Stopwatch.StartNew();
        while(true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(PathIn(folder), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch(IOException)
            {
                // a review, or another update, holds the file
            }

            if(waited.Elapsed > waitLimit)
                return null;

            await Task.Delay(_retryInterval, cancellationToken);
        }
    }

    /// <summary>
    /// A lock that holds nothing, for a folder that has no lock file.
    /// </summary>
    public sealed class NothingHeld : IDisposable
    {
        public void Dispose()
        {
        }
    }
}