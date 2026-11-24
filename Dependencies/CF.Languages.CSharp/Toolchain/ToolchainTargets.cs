using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The two things of ours the build needs, kept in the product's data folder and never in a reviewed repository.
/// </summary>
/// <remarks>
/// They are a small MSBuild file handed to the build from outside (CustomAfterDirectoryBuildTargets), and an empty
/// folder used as the only package source, so that a restore cannot reach the network. The file is rewritten only when
/// its text differs, because a changed file makes every project compile again.
/// </remarks>
internal sealed class ToolchainTargets
{
    /// <summary>
    /// The text of the file.
    /// </summary>
    /// <remarks>
    /// It is made of the file the repository names in the same slot, the log path of each compile, the rules a second
    /// build lets through, and the meaning analyzer, each named by a property the review passes with each project.
    /// </remarks>
    internal const string TargetsText = """
        <Project>
          <!-- Handed to the build of CommitFence's private copy from outside. Nothing in the reviewed repository is edited. -->
          <!-- The file the repository itself names in this slot is imported first, under the condition the SDK itself uses, so that taking the slot does not change what is compiled. -->
          <Import Project="$(CommitFenceRepositoryTargets)" Condition="'$(CommitFenceRepositoryTargets)' != ''" />
          <PropertyGroup Condition="'$(CommitFenceLog)' != ''">
            <ErrorLog>$(CommitFenceLog),version=2.1</ErrorLog>
          </PropertyGroup>
          <!-- The rules a second build lets through are appended to the project's own NoWarn, never put in its place. -->
          <PropertyGroup Condition="'$(CommitFenceLetThrough)' != ''">
            <NoWarn>$(NoWarn);$(CommitFenceLetThrough)</NoWarn>
          </PropertyGroup>
          <ItemGroup Condition="'$(CommitFenceMeaning)' != ''">
            <Analyzer Include="$(CommitFenceMeaning)" />
          </ItemGroup>
        </Project>

        """;

    private readonly IFileSystem _fileSystem;
    private readonly IDataFolder _dataFolder;

    public ToolchainTargets(IFileSystem fileSystem, IDataFolder dataFolder)
    {
        _fileSystem = fileSystem;
        _dataFolder = dataFolder;
    }

    /// <summary>
    /// Makes sure the file exists with the right text and returns its path.
    /// </summary>
    public string EnsureTargetsFile()
    {
        var folder = Path.Combine(_dataFolder.RootFolder, "csharp");
        _fileSystem.CreateFolder(folder);
        var path = ToolchainPaths.Combine(folder, "commitfence.targets");
        if(!_fileSystem.FileExists(path) || _fileSystem.ReadAllText(path) != TargetsText)
            _fileSystem.WriteAllText(path, TargetsText);

        return path;
    }

    /// <summary>
    /// Makes sure the empty package source exists and returns its path.
    /// </summary>
    public string EnsureEmptyPackageSource()
    {
        var path = ToolchainPaths.Combine(Path.Combine(_dataFolder.RootFolder, "csharp"), "no-network-packages");
        _fileSystem.CreateFolder(path);

        return path;
    }
}