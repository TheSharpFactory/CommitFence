using System.Diagnostics;

namespace CF.Adapters.Storage;

/// <summary>
/// A file held open without sharing. The operating system does not wait for such a lock, so taking it is tried again at
/// a short interval until a bounded time has passed.
/// </summary>
internal static class HeldFile
{
    public static FileStream? TryHold(string path, TimeSpan waitLimit, TimeSpan retryInterval)
    {
        // a stopwatch, not the time of day: the wait must not grow when the machine's clock is set back
        var waited = Stopwatch.StartNew();
        while(true)
        {
            var held = TryOpen(path);
            if(held != null)
                return held;

            if(waited.Elapsed > waitLimit)
                return null;

            Thread.Sleep(retryInterval);
        }
    }

    private static FileStream? TryOpen(string path)
    {
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch(IOException)
        {
            return null;
        }
    }
}