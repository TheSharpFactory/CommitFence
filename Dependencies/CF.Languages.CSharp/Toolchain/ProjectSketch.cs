using System.Xml;
using System.Xml.Linq;
using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Reads paths that can make a project take part in a review, without asking MSBuild to evaluate anything.
/// </summary>
internal sealed class ProjectSketch
{
    private static readonly IReadOnlyList<string> _itemPathAttributes = new List<string> { "Include", "Exclude", "Remove", "Update" };
    private static readonly HashSet<string> _implicitInputElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Sdk", "PackageReference",
        "DirectoryBuildPropsPath", "DirectoryBuildTargetsPath", "ImportDirectoryBuildProps", "ImportDirectoryBuildTargets",
        "MSBuildProjectExtensionsPath", "BaseIntermediateOutputPath",
        "CustomBeforeMicrosoftCommonProps", "CustomAfterMicrosoftCommonProps", "CustomBeforeMicrosoftCommonTargets", "CustomAfterMicrosoftCommonTargets",
        "CustomBeforeMicrosoftCSharpTargets", "CustomAfterMicrosoftCSharpTargets",
        "CustomBeforeDirectoryBuildProps", "CustomAfterDirectoryBuildProps", "CustomBeforeDirectoryBuildTargets", "CustomAfterDirectoryBuildTargets"
    };

    private readonly IFileSystem _files;
    private readonly string _treeFolder;
    private readonly Dictionary<string, ProjectText> _texts = new Dictionary<string, ProjectText>(ToolchainPaths.Comparer);

    public ProjectSketch(IFileSystem files, string treeFolder)
    {
        _files = files;
        _treeFolder = ToolchainPaths.Normalize(treeFolder);
    }

    public Sketch Read(ProjectFile project, IReadOnlySet<string> changes)
    {
        var folder = ToolchainPaths.FolderOf(project.Path);
        var pending = new Stack<string>();
        pending.Push(project.Path);
        AddDirectoryBuildFiles(folder, pending);
        var seen = new HashSet<string>(ToolchainPaths.Comparer);
        var references = new HashSet<string>(ToolchainPaths.Comparer);
        var reachable = changes.Any(path => TouchesFolder(folder, path));

        while(pending.TryPop(out var path))
        {
            if(!seen.Add(path))
                continue;

            var text = ReadText(path);
            if(changes.Contains(path) || text.Uncertain || (string.Equals(path, project.Path, ToolchainPaths.Comparison) && text.Document == null))
                reachable = true;

            if(text.Document != null && ReadPaths(text.Document, path, folder, pending, references))
                reachable = true;
        }

        return new Sketch(project, reachable, references);
    }

    private static bool TouchesFolder(string folder, string path)
    {
        return ToolchainPaths.IsUnder(folder, path)
            || (ChangeSelector.IsBuildInput(path) && ToolchainPaths.IsUnder(ToolchainPaths.FolderOf(path), folder));
    }

    private void AddDirectoryBuildFiles(string folder, Stack<string> pending)
    {
        while(ToolchainPaths.IsUnder(_treeFolder, folder) || string.Equals(folder, _treeFolder, ToolchainPaths.Comparison))
        {
            pending.Push(ToolchainPaths.Combine(folder, "Directory.Build.props"));
            pending.Push(ToolchainPaths.Combine(folder, "Directory.Build.targets"));
            var parent = Path.GetDirectoryName(folder);
            if(parent == null)
                break;

            folder = ToolchainPaths.Normalize(parent);
        }
    }

    private ProjectText ReadText(string path)
    {
        if(_texts.TryGetValue(path, out var known))
            return known;

        var text = LoadText(path);
        _texts.Add(path, text);
        return text;
    }

    private ProjectText LoadText(string path)
    {
        try
        {
            using var stream = _files.OpenRead(path);
            using var reader = XmlReader.Create(stream);
            var document = XDocument.Load(reader);
            return new ProjectText(document, !string.Equals(document.Root?.Name.LocalName, "Project", StringComparison.OrdinalIgnoreCase));
        }
        catch(Exception exception) when(exception is FileNotFoundException || exception is DirectoryNotFoundException)
        {
            // A literal import with no file cannot supply any items in this tree.
            return new ProjectText(null, false);
        }
        catch(Exception exception) when(exception is XmlException || exception is IOException || exception is UnauthorizedAccessException)
        {
            return new ProjectText(null, true);
        }
    }

    private bool ReadPaths(XDocument document, string path, string folder, Stack<string> pending, HashSet<string> references)
    {
        var sdk = (string?)document.Root?.Attribute("Sdk");
        var uncertain = sdk != null && !string.Equals(sdk, "Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase);
        foreach(var element in document.Descendants())
        {
            var name = element.Name.LocalName;
            if(string.Equals(name, "Import", StringComparison.OrdinalIgnoreCase))
                uncertain |= !AddImport(element, path, pending);
            else if(IsTrackedItem(name))
                uncertain |= ReadItemPaths(element, folder, references);
            else if(string.Equals(name, "Output", StringComparison.OrdinalIgnoreCase) && IsTrackedItem((string?)element.Attribute("ItemName")))
                uncertain = true;
            else if(_implicitInputElements.Contains(name) || name.StartsWith("_DirectoryBuild", StringComparison.OrdinalIgnoreCase))
                uncertain = true;
        }

        return uncertain;
    }

    private static bool IsTrackedItem(string? name)
    {
        return string.Equals(name, "Compile", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "ProjectReference", StringComparison.OrdinalIgnoreCase);
    }

    private bool AddImport(XElement element, string path, Stack<string> pending)
    {
        var import = ((string?)element.Attribute("Project"))?.Trim();
        if(string.IsNullOrEmpty(import) || element.Attribute("Sdk") != null || HasExpression(import) || import.AsSpan().IndexOfAny(';', '*', '?') >= 0)
            return false;

        var fullPath = Resolve(ToolchainPaths.FolderOf(path), import);
        if(fullPath == null || !ToolchainPaths.IsUnder(_treeFolder, fullPath))
            return false;

        // A linked import can contain the same changed text under a different path.
        var realPath = ToolchainPaths.Normalize(_files.ResolveRealPath(fullPath));
        if(!string.Equals(realPath, fullPath, ToolchainPaths.Comparison))
            return false;

        pending.Push(fullPath);
        return true;
    }

    private bool ReadItemPaths(XElement item, string folder, HashSet<string> references)
    {
        var isReference = string.Equals(item.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase);
        var uncertain = false;
        foreach(var attribute in item.Attributes().Where(attribute => _itemPathAttributes.Contains(attribute.Name.LocalName, StringComparer.OrdinalIgnoreCase)))
        {
            var isInclude = string.Equals(attribute.Name.LocalName, "Include", StringComparison.OrdinalIgnoreCase);
            foreach(var path in attribute.Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if(isReference)
                    uncertain |= !ReadReferencePath(path, folder, isInclude, references);
                else
                    uncertain |= !IsLocalCompilePath(path, folder);
            }
        }

        return uncertain;
    }

    private bool ReadReferencePath(string path, string folder, bool isInclude, HashSet<string> references)
    {
        // A glob does not give the sketch a complete list of prerequisites.
        if(HasExpression(path) || path.AsSpan().IndexOfAny('*', '?') >= 0)
            return false;

        var fullPath = Resolve(folder, path);
        if(fullPath == null || !ToolchainPaths.IsUnder(_treeFolder, fullPath))
            return false;

        // A project the C# graph does not load can hide a dependency on a changed C# project.
        if(!fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return false;

        if(isInclude)
            references.Add(fullPath);

        return true;
    }

    private static bool IsLocalCompilePath(string itemPath, string folder)
    {
        if(HasExpression(itemPath) || itemPath.Contains(':'))
            return false;

        var path = itemPath.Replace('\\', '/');
        var wildcard = path.IndexOfAny('*', '?');
        if(wildcard >= 0)
        {
            if(path.Substring(wildcard).Split('/').Contains("..", StringComparer.Ordinal))
                return false;

            var slash = path.LastIndexOf('/', wildcard);
            var prefix = slash < 0 ? folder : Resolve(folder, path.Substring(0, slash + 1));
            return prefix != null && (string.Equals(prefix.TrimEnd('/'), folder.TrimEnd('/'), ToolchainPaths.Comparison) || ToolchainPaths.IsUnder(folder, prefix));
        }

        var fullPath = Resolve(folder, path);
        return fullPath != null && ToolchainPaths.IsUnder(folder, fullPath);
    }

    private static bool HasExpression(string path)
    {
        return path.Contains("$(", StringComparison.Ordinal) || path.Contains("@(", StringComparison.Ordinal) || path.Contains('%');
    }

    private static string? Resolve(string folder, string path)
    {
        var localPath = path.Replace('\\', '/');
        if((Path.IsPathRooted(localPath) && !Path.IsPathFullyQualified(localPath)) || (!OperatingSystem.IsWindows() && localPath.Contains(':')))
            return null;

        try
        {
            return ToolchainPaths.Combine(folder, localPath);
        }
        catch(Exception exception) when(exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
        {
            return null;
        }
    }

    public sealed record Sketch(ProjectFile Project, bool Reachable, IReadOnlySet<string> References);

    private sealed record ProjectText(XDocument? Document, bool Uncertain);
}
