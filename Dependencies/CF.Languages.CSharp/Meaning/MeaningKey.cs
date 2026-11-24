using System.Security.Cryptography;
using System.Text;
using CF.Languages.CSharp.Toolchain;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// The key under which the answers for a tree are kept.
/// </summary>
/// <remarks>
/// It is made of the tree (git's name for exactly these files), the version of the SDK the tree is built with, the
/// version of our analyzer (the SHA-256 of its file) and the settings of ours (the fingerprint of our targets file and
/// fixed build arguments). Answers kept under another key are never used.
/// </remarks>
internal static class MeaningKey
{
    public static string Create(string tree, string sdkVersion, string analyzerHash)
    {
        var text = $"tree={tree}\nsdk={sdkVersion}\nanalyzer={analyzerHash}\nsettings={BuildSettings.Fingerprint()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}