using System.Globalization;

namespace CF.Core.Services;

/// <summary>The exemptions of one file.</summary>
/// <param name="Path">The path of the file in the repository.</param>
/// <param name="Exemptions">Its exemptions, by line.</param>
public sealed record ExemptionFileGroup(string Path, IReadOnlyList<ExemptionEntry> Exemptions);

/// <summary>How many exemptions name one rule, and how many of them have no reason.</summary>
/// <param name="RuleId">The rule the exemptions name.</param>
/// <param name="Count">All of them.</param>
/// <param name="WithoutReason">The ones that are findings: an allow comment with no reason.</param>
public sealed record ExemptionRuleCount(string RuleId, int Count, int WithoutReason);

/// <summary>
/// Counts and groups a list of exemptions: by file (the file with the most first), by rule, and the sentences about them. The screen
/// and the command both use it, so they say the same.
/// </summary>
public sealed class ExemptionCounts
{
    /// <summary>Counts the exemptions.</summary>
    public ExemptionCounts(IReadOnlyList<ExemptionEntry> exemptions)
    {
        Exemptions = exemptions;
        Files = exemptions.GroupBy(entry => entry.File, StringComparer.Ordinal)
            .Select(group => new ExemptionFileGroup(group.Key, group.OrderBy(entry => entry.Line).ToList()))
            .OrderByDescending(group => group.Exemptions.Count)
            .ThenBy(group => group.Path, StringComparer.Ordinal)
            .ToList();
        Rules = exemptions.GroupBy(entry => entry.RuleId, StringComparer.Ordinal)
            .Select(group => new ExemptionRuleCount(group.Key, group.Count(), group.Count(entry => entry.IsFinding)))
            .OrderByDescending(rule => rule.Count)
            .ThenBy(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The exemptions counted.</summary>
    public IReadOnlyList<ExemptionEntry> Exemptions { get; }

    /// <summary>The files, the one with the most exemptions first, equal ones by path.</summary>
    public IReadOnlyList<ExemptionFileGroup> Files { get; }

    /// <summary>The rules, the one named most often first.</summary>
    public IReadOnlyList<ExemptionRuleCount> Rules { get; }

    /// <summary>How many exemptions there are: one for each rule a line names.</summary>
    public int Entries
    {
        get { return Exemptions.Count; }
    }

    /// <summary>How many lines set a rule aside: one line that names two rules counts once.</summary>
    public int Lines
    {
        get { return LinesOf(Exemptions); }
    }

    /// <summary>How many of them are findings: an allow comment with no reason.</summary>
    public int WithoutReason
    {
        get { return Exemptions.Count(entry => entry.IsFinding); }
    }

    /// <summary>"28 lines set 8 rules aside, in 17 files" ("1 line sets 1 rule aside, in 1 file").</summary>
    public string Summary
    {
        get
        {
            var verb = Lines == 1 ? "sets" : "set";
            return $"{Number(Lines)} {Plural(Lines, "line")} {verb} {Number(Rules.Count)} {Plural(Rules.Count, "rule")} aside, in {Number(Files.Count)} {Plural(Files.Count, "file")}";
        }
    }

    /// <summary>"7 lines set TEAM010 aside, in 3 files": the summary while one rule is chosen.</summary>
    public string SummaryOfRule(string ruleId)
    {
        var verb = Lines == 1 ? "sets" : "set";
        return $"{Number(Lines)} {Plural(Lines, "line")} {verb} {ruleId} aside, in {Number(Files.Count)} {Plural(Files.Count, "file")}";
    }

    /// <summary>"2 have no reason" ("1 has no reason"); empty when every one gives a reason.</summary>
    public string WithoutReasonButton
    {
        get
        {
            var count = WithoutReason;
            return count == 0 ? string.Empty : $"{Number(count)} {(count == 1 ? "has" : "have")} no reason";
        }
    }

    /// <summary>"2 exemptions have no reason" ("1 exemption has no reason"): the summary of the view of them alone.</summary>
    public string WithoutReasonSummary
    {
        get
        {
            var count = WithoutReason;
            return $"{Number(count)} {Plural(count, "exemption")} {(count == 1 ? "has" : "have")} no reason";
        }
    }

    /// <summary>
    /// "7 lines set it aside, 5 of them in Dependencies/CF.Adapters/Updates." for the exemptions of one rule, with the deepest folder
    /// that holds more than half of them named when there is one. The subject is "it" or "this rule".
    /// </summary>
    public static string SetAside(IReadOnlyList<ExemptionEntry> ofOneRule, string subject, bool namesFolder = true)
    {
        var count = LinesOf(ofOneRule);
        var sentence = $"{Number(count)} {Plural(count, "line")} {(count == 1 ? "sets" : "set")} {subject} aside";
        var folder = namesFolder && count > 1 ? DeepestMajorityFolder(ofOneRule) : null;
        if(folder == null)
            return sentence + ".";

        var inside = LinesOf(ofOneRule.Where(entry => entry.File.StartsWith(folder + "/", StringComparison.Ordinal)));
        var of = inside == count ? "all of them" : $"{Number(inside)} of them";
        return $"{sentence}, {of} in {folder}.";
    }

    /// <summary>
    /// The deepest folder that holds more than half of the exemptions, as a path with forward slashes; null when no folder does.
    /// </summary>
    public static string? DeepestMajorityFolder(IReadOnlyList<ExemptionEntry> exemptions)
    {
        var holding = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach(var entry in exemptions)
        {
            foreach(var folder in FoldersAbove(entry.File))
                holding[folder] = holding.GetValueOrDefault(folder) + 1;
        }

        return holding.Where(pair => pair.Value * 2 > exemptions.Count)
            .OrderByDescending(pair => pair.Key.Count(character => character == '/'))
            .ThenByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .FirstOrDefault();
    }

    /// <summary>The folders that hold a file, from the top: "a", "a/b" for "a/b/c.cs".</summary>
    private static IEnumerable<string> FoldersAbove(string path)
    {
        var at = path.IndexOf('/', StringComparison.Ordinal);
        while(at > 0)
        {
            yield return path[..at];
            at = path.IndexOf('/', at + 1);
        }
    }

    private static int LinesOf(IEnumerable<ExemptionEntry> exemptions)
    {
        return exemptions.Select(entry => (entry.File, entry.Line)).Distinct().Count();
    }

    private static string Number(int count)
    {
        return count.ToString(CultureInfo.InvariantCulture);
    }

    private static string Plural(int count, string word)
    {
        return count == 1 ? word : word + "s";
    }
}
