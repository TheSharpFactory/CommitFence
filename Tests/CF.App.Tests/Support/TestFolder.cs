namespace CF.App.Tests.Support;

/// <summary>
/// A temporary folder that is deleted, with everything in it, when the test is done.
/// </summary>
internal sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cf-app-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if(Directory.Exists(Path))
            Directory.Delete(Path, true);
    }
}
