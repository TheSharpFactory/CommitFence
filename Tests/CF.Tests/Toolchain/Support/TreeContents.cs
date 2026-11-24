using System.Security.Cryptography;

namespace CF.Tests.Toolchain.Support;

/// <summary>
/// What a folder holds, to see that a review left it as it was.
/// </summary>
internal static class TreeContents
{
    /// <summary>
    /// Every file of the tree except the outputs of a build (bin and obj), each with the hash of its content.
    /// </summary>
    public static List<string> OutsideBuildOutputs(string tree)
    {
        var files = Directory.EnumerateFiles(tree, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(tree, path).Replace('\\', '/')).Where(path => !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal));
        return files.Select(path => path + "=" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(tree, path))))).Order(StringComparer.Ordinal).ToList();
    }
}
