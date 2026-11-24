using System.Security.Cryptography;
using System.Text;

namespace CF.Adapters.Storage;

/// <summary>
/// A short hash of a full path, written in lower case so that two spellings of one path on a machine that ignores case
/// give one name.
/// </summary>
internal static class PathHash
{
    private const int _hashLength = 8;

    public static string Of(string path)
    {
        var normalized = Path.GetFullPath(path).TrimEnd('/', '\\').ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();

        return hash.Substring(0, _hashLength);
    }
}