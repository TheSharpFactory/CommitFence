using System.Security.Cryptography;
using CF.Core.Ports;
using CF.Languages.CSharp.Toolchain;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// The analyzer of ours that the team's compiler loads during a review's build.
/// </summary>
/// <remarks>
/// It ships beside the programs, in CommitFence's own folder; it is handed to the build by its path and is never copied
/// into a reviewed repository.
/// </remarks>
internal sealed class MeaningAnalyzerFile
{
    private readonly IFileSystem _fileSystem;
    private string? _hash;

    public MeaningAnalyzerFile(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string Path { get; } = PathBeside(AppContext.BaseDirectory);

    public static string PathBeside(string programFolder)
    {
        return ToolchainPaths.Combine(programFolder, "analyzers/CF.Meaning.dll");
    }

    public bool Exists()
    {
        return _fileSystem.FileExists(Path);
    }

    /// <summary>
    /// The SHA-256 of the file: the version of the analyzer, for the key of kept answers.
    /// </summary>
    public string Hash()
    {
        if(_hash != null)
            return _hash;

        using(var stream = _fileSystem.OpenRead(Path))
        {
            _hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        return _hash;
    }

    public string DescribeMissing()
    {
        return $"the analyzer of CommitFence that reads what the code refers to is missing from {Path}, so the build cannot be checked: reinstall CommitFence";
    }
}