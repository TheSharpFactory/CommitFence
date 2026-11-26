using CF.Core.Ports;
using CF.Tests.Toolchain.Support;

namespace CF.Tests.Rules.Support;

/// <summary>
/// The dotnet command of a machine whose only SDK lives in a folder of the test, or of a machine with no dotnet at all.
/// </summary>
internal sealed class SdkFolderRunner : IProcessRunner
{
    public const string Version = "11.0.100";

    private readonly string? _sdkRoot;

    public SdkFolderRunner(string? sdkRoot)
    {
        _sdkRoot = sdkRoot;
    }

    public ProcessResult Run(ProcessRequest request)
    {
        if(_sdkRoot == null)
            return new ProcessResult(-1, string.Empty, "dotnet could not be started.");

        if(request.Arguments.SequenceEqual(new[] { "--version" }))
            return new ProcessResult(0, Version + "\n", string.Empty);

        if(request.Arguments.SequenceEqual(new[] { "--list-sdks" }))
            return new ProcessResult(0, $"{Version} [{_sdkRoot}]\n", string.Empty);

        return new ProcessResult(1, string.Empty, "error: the fake dotnet does not know " + string.Join(" ", request.Arguments));
    }
}

/// <summary>
/// The file system of the machine, except that a file of the given name cannot be read by this user.
/// </summary>
internal sealed class DeniedFileSystem : IFileSystem
{
    private readonly IFileSystem _inner;
    private readonly string _deniedName;

    public DeniedFileSystem(IFileSystem inner, string deniedName)
    {
        _inner = inner;
        _deniedName = deniedName;
    }

    public string ResolveRealPath(string path)
    {
        return _inner.ResolveRealPath(path);
    }

    public bool FileExists(string path)
    {
        return _inner.FileExists(path);
    }

    public bool FolderExists(string path)
    {
        return _inner.FolderExists(path);
    }

    public string ReadAllText(string path)
    {
        if(Path.GetFileName(path) == _deniedName)
            throw new UnauthorizedAccessException($"Access to the path '{path}' is denied.");

        return _inner.ReadAllText(path);
    }

    public Stream OpenRead(string path)
    {
        return _inner.OpenRead(path);
    }

    public void WriteAllText(string path, string content)
    {
        _inner.WriteAllText(path, content);
    }

    public void WriteNewFile(string path, string content)
    {
        _inner.WriteNewFile(path, content);
    }

    public void ReplaceFile(string source, string destination)
    {
        _inner.ReplaceFile(source, destination);
    }

    public void CreateFolder(string path)
    {
        _inner.CreateFolder(path);
    }

    public void DeleteFile(string path)
    {
        _inner.DeleteFile(path);
    }

    public IReadOnlyList<string> FindFiles(string folder, string extension, IReadOnlyList<string> skippedFolderNames)
    {
        return _inner.FindFiles(folder, extension, skippedFolderNames);
    }
}

/// <summary>
/// An SDK folder of the test, with the files from which the titles of the compiler's rules are read.
/// </summary>
internal sealed class FakeSdk : IDisposable
{
    private readonly TestFolder _folder = new TestFolder();

    public FakeSdk()
    {
        var sdk = Path.Combine(Root, SdkFolderRunner.Version, "Sdks", "Microsoft.NET.Sdk");
        TreeFiles.Write(sdk, Path.Combine("analyzers", "build", "config", "analysislevel_9_all.globalconfig"), "# CA1000: An older title of the analyzer\n");
        TreeFiles.Write(sdk, Path.Combine("analyzers", "build", "config", "analysislevel_11_all.globalconfig"), "# CA1000: Do not declare static members on generic types\n# CA1001: Disposable fields\n# CA1002: Do not expose generic lists\n# CA1050: Declare types in namespaces\n# CA1052: Static holder types\n# ZZ1000: Test analyzer\n");
        TreeFiles.Write(sdk, Path.Combine("codestyle", "cs", "build", "config", "analysislevelstyle_all.globalconfig"), "# IDE0005: Remove unnecessary using directives\n");
    }

    public string Root
    {
        get { return Path.Combine(_folder.Path, "sdk"); }
    }

    /// <summary>
    /// The dotnet command of a machine with this SDK, or of one without dotnet.
    /// </summary>
    public IProcessRunner Runner(bool isAvailable)
    {
        return new SdkFolderRunner(isAvailable ? Root : null);
    }

    public void Dispose()
    {
        _folder.Dispose();
    }
}
