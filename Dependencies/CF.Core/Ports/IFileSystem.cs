namespace CF.Core.Ports;

/// <summary>
/// Reading and writing files, behind a port so that Core touches no file itself.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Whether a file exists at this path.
    /// </summary>
    bool FileExists(string path);

    /// <summary>Whether a folder exists at this path.</summary>
    bool FolderExists(string path);

    /// <summary>Whether the file exists and the platform treats it as executable.</summary>
    bool IsExecutable(string path)
    {
        return FileExists(path);
    }

    /// <summary>
    /// The whole text of a file.
    /// </summary>
    string ReadAllText(string path);

    /// <summary>
    /// Opens a file for reading. The caller disposes the stream.
    /// </summary>
    Stream OpenRead(string path);

    /// <summary>
    /// Writes the text to a file, in UTF-8 without a byte order mark, replacing what was there. The folder must exist.
    /// </summary>
    void WriteAllText(string path, string content);

    /// <summary>
    /// Creates a file that must not exist yet and writes the text to it, in UTF-8 without a byte order mark.
    /// </summary>
    /// <remarks>
    /// An existing file is never touched: the call fails with an IOException. When writing fails after the file was
    /// created, the file is removed again.
    /// </remarks>
    void WriteNewFile(string path, string content);

    /// <summary>Creates a new file with exactly these bytes, under the same rules as the text overload.</summary>
    void WriteNewFile(string path, ReadOnlySpan<byte> content)
    {
        throw new NotSupportedException();
    }

    /// <summary>Whether the file is marked read-only.</summary>
    bool IsReadOnly(string path)
    {
        return false;
    }

    /// <summary>Whether the path is a symbolic link or other reparse point.</summary>
    bool IsLink(string path)
    {
        return false;
    }

    /// <summary>Writes an executable shell hook, checking the original contents and preserving its other mode bits.</summary>
    void WriteHook(string path, byte[]? original, byte[] content)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Moves the file to the destination, replacing the file that is there.
    /// </summary>
    /// <remarks>
    /// Both are on the same drive, so the replacement happens in one step: a reader sees the old file or the new one,
    /// never half of one.
    /// </remarks>
    void ReplaceFile(string source, string destination);

    /// <summary>
    /// Makes sure the folder exists, with the folders above it.
    /// </summary>
    void CreateFolder(string path);

    /// <summary>
    /// Deletes the file; nothing happens when it does not exist.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// The full paths of every file below the folder whose name ends with the extension (such as ".csproj"), found
    /// without entering the folders whose names are given.
    /// </summary>
    IReadOnlyList<string> FindFiles(string folder, string extension, IReadOnlyList<string> skippedFolderNames);

    /// <summary>
    /// The path with every symbolic link in it replaced by what it points to, so that the same folder has one name
    /// however it is reached. A part of the path that does not exist is left as it is.
    /// </summary>
    /// <remarks>
    /// A program started in a folder sees the folder under its real name, so the paths it writes into its logs use that
    /// name. A path of ours that goes through a link (on macOS the temporary folder, /var, is one) must be turned into
    /// the real one before it is compared with theirs.
    /// </remarks>
    string ResolveRealPath(string path);
}
