using CF.Core.Findings;
using CF.Core.Review;
using CF.Core.Services;

namespace CF.App.Tests.Support;

/// <summary>
/// The small pieces the tests are written with: findings, reviews, an opened folder.
/// </summary>
internal static class Samples
{
    public const string InstallFolder = @"C:\Program Files\CommitFence";
    public const string DataFolder = @"C:\Users\dev\AppData\Local\CommitFence";
    public static readonly string InstalledVersion = new ProductInfoService().Version;

    /// <summary>The version that waits in the samples: always newer than the one installed.</summary>
    public static readonly string WaitingVersion = NextAfter(InstalledVersion);
    public static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private static string NextAfter(string version)
    {
        var parts = version.Split('.', '-', '+');
        return $"{parts[0]}.{parts[1]}.{int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) + 1}.0";
    }

    public static Finding Finding(string file, int line, string ruleId, Severity severity, string message = "A finding.", FindingSource source = FindingSource.CodeRule)
    {
        return new Finding(ruleId, source, severity, "csharp", null, null, file, line, 1, message, null, null);
    }

    public static Coverage Reviewed(string file)
    {
        return new Coverage(file, CoverageStatus.Reviewed, string.Empty);
    }

    public static Coverage NotReviewed(string file, string reason)
    {
        return new Coverage(file, CoverageStatus.Failed, reason);
    }

    public static ReviewResult Result(ReviewOutcome outcome, IReadOnlyList<Finding>? findings = null, IReadOnlyList<Coverage>? coverage = null, IReadOnlyDictionary<string, string>? sources = null)
    {
        return new ReviewResult(outcome, findings ?? new List<Finding>(), coverage ?? new List<Coverage>(), new List<string>(), string.Empty, TimeSpan.FromSeconds(1.5), sources ?? new Dictionary<string, string>());
    }

    public static OpenedFolder Folder(string path, bool insideRepository = true, string name = "invoicing")
    {
        return new OpenedFolder(path, Path.Combine(path, "commitfence.json"), File.Exists(Path.Combine(path, "commitfence.json")), name, insideRepository ? "main" : null, insideRepository);
    }

    /// <summary>
    /// A commit id as git writes one: forty hexadecimal digits.
    /// </summary>
    public static string Commit(char digit)
    {
        return new string(digit, 40);
    }
}
