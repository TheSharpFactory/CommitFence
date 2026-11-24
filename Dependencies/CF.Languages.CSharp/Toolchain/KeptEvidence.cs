using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The compiler log of the last successful compile of each project and framework, kept in the product's data folder, one
/// folder for each repository, with the key of what that compile read (see <see cref="EvidenceKey" />).
/// </summary>
/// <remarks>
/// Each entry is two files: the log, and a small file with the key that is written last, so that an entry without it is
/// not one. A project and framework has one entry: the newest replaces the older. An entry that cannot be read is absent.
/// </remarks>
internal sealed class KeptEvidence
{
    private const string _logExtension = ".sarif";
    private const string _keyExtension = ".key.json";

    private readonly IFileSystem _fileSystem;
    private readonly string _folder;

    public KeptEvidence(IFileSystem fileSystem, IDataFolder dataFolder, string treeFolder)
    {
        _fileSystem = fileSystem;
        _folder = ToolchainPaths.Combine(Path.Combine(dataFolder.RootFolder, "csharp", "evidence"), NameOf(ToolchainPaths.Normalize(treeFolder)));
    }

    /// <summary>
    /// Whether something is kept for the part, whatever its key.
    /// </summary>
    public bool Has(string partKey)
    {
        return _fileSystem.FileExists(PathOf(partKey, _keyExtension)) && _fileSystem.FileExists(PathOf(partKey, _logExtension));
    }

    /// <summary>
    /// The path of the kept log of the part, when its key is exactly this one and the log is the very one that was kept.
    /// </summary>
    public string? Find(string partKey, string key)
    {
        var keyPath = PathOf(partKey, _keyExtension);
        var logPath = PathOf(partKey, _logExtension);
        if(!_fileSystem.FileExists(keyPath) || !_fileSystem.FileExists(logPath))
            return null;

        try
        {
            var kept = JsonSerializer.Deserialize(_fileSystem.ReadAllText(keyPath), KeptEvidenceJsonContext.Default.KeptEvidenceKeyDto);
            return kept?.Key == key && kept.LogHash == HashOf(logPath) ? logPath : null;
        }
        catch(Exception exception) when(exception is JsonException || exception is IOException || exception is UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Keeps the log of a compile that succeeded under the key, in place of the one kept before.
    /// </summary>
    public void Save(string partKey, string key, string logPath)
    {
        _fileSystem.CreateFolder(_folder);
        _fileSystem.DeleteFile(PathOf(partKey, _keyExtension));
        _fileSystem.WriteAllText(PathOf(partKey, _logExtension), _fileSystem.ReadAllText(logPath));
        var entry = new KeptEvidenceKeyDto { Key = key, LogHash = HashOf(PathOf(partKey, _logExtension)) };
        _fileSystem.WriteAllText(PathOf(partKey, _keyExtension), JsonSerializer.Serialize(entry, KeptEvidenceJsonContext.Default.KeptEvidenceKeyDto));
    }

    private string HashOf(string path)
    {
        using(var stream = _fileSystem.OpenRead(path))
        {
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Forgets what was kept for the part: it was compiled again, so the kept log no longer tells what its output holds.
    /// </summary>
    public void Remove(string partKey)
    {
        _fileSystem.DeleteFile(PathOf(partKey, _keyExtension));
        _fileSystem.DeleteFile(PathOf(partKey, _logExtension));
    }

    /// <summary>
    /// Removes the entries of every part that is not among these: its project is gone.
    /// </summary>
    public void RemoveExcept(IEnumerable<string> partKeys)
    {
        var wanted = partKeys.Select(NameOf).ToHashSet(StringComparer.Ordinal);
        foreach(var path in FindEntryFiles())
        {
            if(!wanted.Contains(Path.GetFileName(path).Split('.')[0]))
                _fileSystem.DeleteFile(path);
        }
    }

    private List<string> FindEntryFiles()
    {
        try
        {
            var none = new List<string>();
            return _fileSystem.FindFiles(_folder, _logExtension, none).Concat(_fileSystem.FindFiles(_folder, _keyExtension, none)).ToList();
        }
        catch(DirectoryNotFoundException)
        {
            return new List<string>();
        }
    }

    private string PathOf(string partKey, string extension)
    {
        return ToolchainPaths.Combine(_folder, NameOf(partKey) + extension);
    }

    private static string NameOf(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant().Substring(0, 32);
    }
}