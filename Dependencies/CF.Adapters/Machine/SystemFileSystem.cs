using System.Text;
using CF.Core.Ports;

namespace CF.Adapters.Machine;

/// <summary>
/// The files of the machine the program runs on.
/// </summary>
public sealed class SystemFileSystem : IFileSystem
{
    private const int _maxLinkDepth = 16;

    private static readonly UTF8Encoding _utf8WithoutMark = new UTF8Encoding(false);

    /// <inheritdoc />
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    /// <inheritdoc />
    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    /// <inheritdoc />
    public Stream OpenRead(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <inheritdoc />
    public void WriteAllText(string path, string content)
    {
        File.WriteAllText(path, content, _utf8WithoutMark);
    }

    /// <inheritdoc />
    public void WriteNewFile(string path, string content)
    {
        WriteNewFile(path, _utf8WithoutMark.GetBytes(content));
    }

    /// <inheritdoc />
    public void WriteNewFile(string path, ReadOnlySpan<byte> content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            stream.Write(content);
            stream.Flush(true);
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            stream.Dispose();
            File.Delete(path);
            throw;
        }
    }

    /// <inheritdoc />
    public void ReplaceFile(string source, string destination)
    {
        File.Move(source, destination, true);
    }

    /// <inheritdoc />
    public void CreateFolder(string path)
    {
        Directory.CreateDirectory(path);
    }

    /// <inheritdoc />
    public void DeleteFile(string path)
    {
        if(File.Exists(path))
            File.Delete(path);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> FindFiles(string folder, string extension, IReadOnlyList<string> skippedFolderNames)
    {
        var found = new List<string>();
        Collect(folder, extension, skippedFolderNames, found);
        found.Sort(StringComparer.Ordinal);

        return found;
    }

    private static void Collect(string folder, string extension, IReadOnlyList<string> skippedFolderNames, List<string> found)
    {
        found.AddRange(Directory.EnumerateFiles(folder).Where(file => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)));
        foreach(var child in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(child);
            if(!skippedFolderNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                Collect(child, extension, skippedFolderNames, found);
        }
    }

    /// <inheritdoc />
    public string ResolveRealPath(string path)
    {
        return ResolveRealPath(Path.GetFullPath(path), 0);
    }

    private static string ResolveRealPath(string fullPath, int depth)
    {
        // a link that points back at itself ends here, with the path as it is
        if(OperatingSystem.IsWindows() || depth > _maxLinkDepth)
            return fullPath;

        var current = Path.GetPathRoot(fullPath) ?? string.Empty;
        var parts = fullPath.Substring(current.Length).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        for(var index = 0; index < parts.Length; index++)
        {
            var next = Path.Combine(current, parts[index]);
            var target = FindLinkTarget(next);
            if(target == null)
            {
                current = next;
                continue;
            }

            var rest = string.Join(Path.DirectorySeparatorChar, parts.Skip(index + 1));
            return ResolveRealPath(Path.GetFullPath(Path.Combine(target, rest)), depth + 1);
        }

        return current;
    }

    private static string? FindLinkTarget(string path)
    {
        try
        {
            FileSystemInfo entry = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return entry.LinkTarget == null ? null : entry.ResolveLinkTarget(true)?.FullName;
        }
        catch(IOException)
        {
            return null;
        }
    }
}
