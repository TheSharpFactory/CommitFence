using System.Security.Cryptography;
using System.Text;
using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The key of what a compile read: the text that tells whether a compile kept earlier would give the same answer today.
/// </summary>
/// <remarks>
/// It is made of the target framework, the compiler (the SDK version), the version of the meaning analyzer, the tree, the
/// settings of ours, and the manifest the build wrote after the compile (see <see cref="ToolchainTargets" />): every
/// property line, and for every file the compiler was given, its path and the hash of its content, in the order the
/// compiler is given them. Nothing is guessed from the project. A file that is missing or cannot be read, no manifest, a
/// manifest that does not end with its closing line or lists no source, a manifest that says the compiler's log was not
/// the one asked for, or one that marks that no key can be made gives no key, and with no key nothing kept is used or written. A file outside the tree (the SDK, the
/// packages) is hashed once for the review, a file inside it every time.
/// </remarks>
internal sealed class EvidenceKey
{
    private const string _askedLogLine = "errorlog|True";
    private const string _endLine = "end|manifest";
    private const string _noKeyTag = "nokey";
    private const string _endTag = "end";
    private const string _sourceTag = "source";
    private const string _propertyTag = "prop";
    private const string _logTag = "errorlog";

    private readonly IFileSystem _fileSystem;
    private readonly string _treeFolder;
    private readonly string _compilerVersion;
    private readonly string _analyzerHash;
    private readonly Dictionary<string, string> _hashesOutsideTree = new Dictionary<string, string>(ToolchainPaths.Comparer);

    public EvidenceKey(IFileSystem fileSystem, string treeFolder, string compilerVersion, string analyzerHash)
    {
        _fileSystem = fileSystem;
        _treeFolder = ToolchainPaths.Normalize(treeFolder);
        _compilerVersion = compilerVersion;
        _analyzerHash = analyzerHash;
    }

    /// <summary>
    /// The key for the unit from the manifest its build wrote, or nothing when it cannot be made.
    /// </summary>
    public string? Create(ProjectUnit unit, string manifestPath)
    {
        try
        {
            if(!_fileSystem.FileExists(manifestPath))
                return null;

            var manifest = _fileSystem.ReadAllText(manifestPath).Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToList();
            if(!IsWhole(manifest))
                return null;

            var lines = new List<string> { "framework=" + unit.Framework, "compiler=" + _compilerVersion, "analyzer=" + _analyzerHash, "tree=" + _treeFolder, "settings=" + BuildSettings.Fingerprint() };
            lines.AddRange(manifest.Select(DescribeLine));

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)))).ToLowerInvariant();
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsWhole(List<string> manifest)
    {
        var asksOurLog = manifest.Count(line => line.StartsWith(_logTag + "|", StringComparison.Ordinal)) == 1 && manifest.Contains(_askedLogLine);

        return asksOurLog && manifest.Count > 0 && manifest[^1] == _endLine && manifest.Any(line => line.StartsWith(_sourceTag + "|", StringComparison.Ordinal)) && !manifest.Any(line => line.StartsWith(_noKeyTag + "|", StringComparison.Ordinal));
    }

    private string DescribeLine(string line)
    {
        var bar = line.IndexOf('|', StringComparison.Ordinal);
        var tag = line.Substring(0, bar < 0 ? line.Length : bar);
        if(bar < 0 || tag == _propertyTag || tag == _logTag || tag == _endTag)
            return line;

        var path = ToolchainPaths.Normalize(line.Substring(bar + 1));
        return $"{tag}|{ToolchainPaths.RelativeTo(_treeFolder, path)}|{HashOf(path)}";
    }

    private string HashOf(string path)
    {
        if(ToolchainPaths.IsUnder(_treeFolder, path))
            return HashFile(path);

        if(!_hashesOutsideTree.TryGetValue(path, out var hash))
        {
            hash = HashFile(path);
            _hashesOutsideTree[path] = hash;
        }

        return hash;
    }

    private string HashFile(string path)
    {
        using(var stream = _fileSystem.OpenRead(path))
        {
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}