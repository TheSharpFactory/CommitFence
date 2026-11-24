using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Finds every C# project file of the tree. Folders that hold build outputs or other people's code are not entered.
/// </summary>
internal sealed class ProjectFinder
{
    private static readonly IReadOnlyList<string> _skippedFolders = new List<string> { "bin", "obj", "node_modules", ".git" };

    private readonly IFileSystem _fileSystem;

    public ProjectFinder(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public IReadOnlyList<ProjectFile> Find(string treeFolder)
    {
        var paths = _fileSystem.FindFiles(treeFolder, ".csproj", _skippedFolders).Select(ToolchainPaths.Normalize).ToList();
        var nameCounts = paths.GroupBy(path => Path.GetFileNameWithoutExtension(path), ToolchainPaths.Comparer).ToDictionary(group => group.Key, group => group.Count(), ToolchainPaths.Comparer);

        return paths.Select(path => new ProjectFile(path, DisplayNameOf(treeFolder, path, nameCounts))).ToList();
    }

    private static string DisplayNameOf(string treeFolder, string path, Dictionary<string, int> nameCounts)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return nameCounts[name] == 1 ? name : ToolchainPaths.RelativeTo(treeFolder, path);
    }
}