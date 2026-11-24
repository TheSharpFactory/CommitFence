namespace CF.Tests.Toolchain.Support;

/// <summary>
/// A temporary folder for the real tests that is reached through a symbolic link on Linux and macOS, as the temporary folder
/// of a Mac is (/var is a link to /private/var). The compiler writes the real path into its logs, so a product that compares
/// those paths with the path it was given fails here, without a Mac. On Windows a link needs a privilege, so the folder is plain.
/// </summary>
internal sealed class LinkedFolder : IDisposable
{
    private readonly string _real;

    public LinkedFolder()
    {
        _real = System.IO.Path.Combine(TestPaths.Temp, "cf-linked-real-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_real);
        if(OperatingSystem.IsWindows())
        {
            Path = _real;
            return;
        }

        Path = System.IO.Path.Combine(TestPaths.Temp, "cf-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateSymbolicLink(Path, _real);
    }

    /// <summary>
    /// The folder as the test names it: the link on Linux and macOS.
    /// </summary>
    public string Path { get; }

    public void Dispose()
    {
        if(!string.Equals(Path, _real, StringComparison.Ordinal) && Directory.Exists(Path))
            Directory.Delete(Path);

        if(Directory.Exists(_real))
            Directory.Delete(_real, true);
    }
}
