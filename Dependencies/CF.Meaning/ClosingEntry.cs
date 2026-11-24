using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CF.Meaning;

/// <summary>
/// The entry that closes a project's facts.
/// </summary>
/// <remarks>
/// It holds the format version, the assembly, how many files were analyzed, how many facts there are and the SHA-256 of
/// the facts, sorted by ordinal comparison and joined by line feeds. A reader that finds another count or checksum
/// knows the log is cut short or altered.
/// </remarks>
internal static class ClosingEntry
{
    public const string FormatVersion = "1";

    public static string Create(IEnumerable<string> facts, string assembly, int analyzedFiles)
    {
        var sorted = facts.OrderBy(fact => fact, StringComparer.Ordinal).ToList();
        var checksum = Checksum(sorted);

        return FactText.Join("END", FormatVersion, assembly, analyzedFiles.ToString(CultureInfo.InvariantCulture), sorted.Count.ToString(CultureInfo.InvariantCulture), checksum);
    }

    private static string Checksum(List<string> sortedFacts)
    {
        using(var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", sortedFacts)));
            var text = new StringBuilder();
            foreach(var value in hash)
                text.Append(value.ToString("x2", CultureInfo.InvariantCulture));

            return text.ToString();
        }
    }
}