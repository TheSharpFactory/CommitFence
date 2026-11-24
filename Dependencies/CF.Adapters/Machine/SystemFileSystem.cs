using System.Text;
using CF.Core.Ports;

namespace CF.Adapters.Machine;

/// <summary>
/// The files of the machine the program runs on.
/// </summary>
public sealed class SystemFileSystem : IFileSystem
{
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
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            var bytes = _utf8WithoutMark.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
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
}