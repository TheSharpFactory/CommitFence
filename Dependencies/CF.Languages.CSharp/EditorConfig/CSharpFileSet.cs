using CF.Core.Code;
using CF.Core.Ports;

namespace CF.Languages.CSharp.EditorConfig;

/// <summary>
/// The C# files of a repository folder, asked for only when a section's pattern does not match a file in the folder's
/// own top, as the paths relative to it with forward slashes.
/// </summary>
internal sealed class CSharpFileSet
{
    private const string _probe = "File.cs";

    private static readonly IReadOnlyList<string> _skippedFolders = new List<string> { ".git", "bin", "obj", "node_modules" };

    private readonly Lazy<List<string>> _paths;

    public CSharpFileSet(IFileSystem fileSystem, string repositoryFolder)
    {
        _paths = new Lazy<List<string>>(() => Find(fileSystem, repositoryFolder));
    }

    /// <summary>
    /// Whether the pattern matches a C# file of the folder: a file of any name in its top, or one the folder holds.
    /// </summary>
    public bool Matches(SectionPattern pattern)
    {
        return NamesAll(pattern) || _paths.Value.Any(pattern.Matches);
    }

    public static bool NamesAll(SectionPattern pattern)
    {
        return pattern.Matches(_probe) && pattern.Matches("a/b/File.cs");
    }

    public bool Overlaps(SectionPattern pattern, IEnumerable<FileRulePlace> earlier)
    {
        return _paths.Value.Any(path => pattern.Matches(path) && earlier.Any(place => place.Matches(path)));
    }

    private static List<string> Find(IFileSystem fileSystem, string repositoryFolder)
    {
        return fileSystem.FindFiles(repositoryFolder, ".cs", _skippedFolders).Select(path => Path.GetRelativePath(repositoryFolder, path).Replace('\\', '/')).ToList();
    }
}