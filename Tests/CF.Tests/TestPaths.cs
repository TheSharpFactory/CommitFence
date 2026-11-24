using CF.Adapters.Machine;

namespace CF.Tests;

/// <summary>
/// Where the tests put their temporary folders.
/// </summary>
internal static class TestPaths
{
    /// <summary>
    /// The system's temporary folder by its real path. On macOS the temporary folder sits behind a symbolic link, and a
    /// program started inside it reports the real path: a test that built its paths from the link would disagree with
    /// what the product resolves. The tests that want a link make one themselves.
    /// </summary>
    public static string Temp { get; } = new SystemFileSystem().ResolveRealPath(Path.GetTempPath());
}
