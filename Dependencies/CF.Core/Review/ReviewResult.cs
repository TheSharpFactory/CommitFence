using CF.Core.Findings;
using CF.Core.Ports;

namespace CF.Core.Review;

/// <summary>
/// Everything a review found and everything it could and could not cover.
/// </summary>
public sealed class ReviewResult
{
    /// <summary>
    /// Creates a result.
    /// </summary>
    public ReviewResult(
        ReviewOutcome outcome,
        IReadOnlyList<Finding> findings,
        IReadOnlyList<Coverage> coverage,
        IReadOnlyList<string> skipped,
        string summary,
        TimeSpan duration)
        : this(outcome, findings, coverage, skipped, summary, duration, new Dictionary<string, string>())
    {
    }

    /// <summary>
    /// Creates a result that also holds the text of the files that have findings.
    /// </summary>
    public ReviewResult(
        ReviewOutcome outcome,
        IReadOnlyList<Finding> findings,
        IReadOnlyList<Coverage> coverage,
        IReadOnlyList<string> skipped,
        string summary,
        TimeSpan duration,
        IReadOnlyDictionary<string, string> sources)
    {
        Outcome = outcome;
        Findings = findings;
        Coverage = coverage;
        Skipped = skipped;
        Summary = summary;
        Duration = duration;
        Sources = sources;
    }

    /// <summary>
    /// How the review ended.
    /// </summary>
    public ReviewOutcome Outcome { get; }

    /// <summary>
    /// What was found.
    /// </summary>
    public IReadOnlyList<Finding> Findings { get; }

    /// <summary>
    /// The tree the review read, when it reviewed exactly one change; empty otherwise. With <see cref="BaseTreeReviewed" /> it
    /// tells which state of the repository the findings are about.
    /// </summary>
    public string TreeReviewed { get; init; } = string.Empty;

    /// <summary>The tree that <see cref="TreeReviewed" /> was compared with; empty when the review read no single change.</summary>
    public string BaseTreeReviewed { get; init; } = string.Empty;

    /// <summary>
    /// The full id of the commit a review was about, resolved once when the review took its snapshot: the commit the staged
    /// changes or a folder were read against, the head of a range, the tip of a pushed ref. Empty when the review read more
    /// than one change or the repository has no commit.
    /// </summary>
    public string CommitReviewed { get; init; } = string.Empty;

    /// <summary>
    /// The address of the remote the repository is hosted at, without credentials and never a local path, so that a report can
    /// name where the reviewed commit lives; empty when the repository has no such remote.
    /// </summary>
    public string RemoteAddress { get; init; } = string.Empty;

    /// <summary>The repository of the reviewed trees, when the review used git.</summary>
    public RepositoryLocation? Repository { get; internal init; }

    internal IReadOnlyDictionary<Finding, IReadOnlyList<string>> FindingTrees { get; init; } = new Dictionary<Finding, IReadOnlyList<string>>();

    /// <summary>All distinct trees where the review reported this finding.</summary>
    public IReadOnlyList<string> TreesOf(Finding finding)
    {
        return FindingTrees.GetValueOrDefault(finding) ?? [];
    }

    /// <summary>The tree a finding was produced from, when the review reported it in just one tree.</summary>
    public string? TreeOf(Finding finding)
    {
        var trees = TreesOf(finding);
        return trees.Count == 1 ? trees[0] : null;
    }

    internal IReadOnlyDictionary<Finding, ReviewedRule> ReviewedRules { get; set; } = new Dictionary<Finding, ReviewedRule>();

    internal IReadOnlyDictionary<Finding, string?> ReportedTrees { get; init; } = new Dictionary<Finding, string?>();

    internal IReadOnlyDictionary<(string? Tree, string Path), string> ReviewedSources { get; init; } = new Dictionary<(string? Tree, string Path), string>();

    /// <summary>Gets the definition used for this finding, when one was available.</summary>
    public ReviewedRule? RuleOf(Finding finding)
    {
        return ReviewedRules.GetValueOrDefault(finding);
    }

    /// <summary>Gets the source from the tree that supplied this finding.</summary>
    public string? SourceOf(Finding finding)
    {
        if(finding.File == null)
            return null;

        return ReportedTrees.TryGetValue(finding, out var tree)
            ? ReviewedSources.GetValueOrDefault((tree, finding.File))
            : Sources.GetValueOrDefault(finding.File);
    }

    /// <summary>Findings set aside by allow comments, with the comments' lines and reasons.</summary>
    public IReadOnlyList<ExemptedFinding> ExemptedFindings { get; init; } = [];

    /// <summary>The completed judgment of layer directions, when the review judged them.</summary>
    public StructureJudgment? StructureJudgment { get; internal init; }

    /// <summary>
    /// What was covered.
    /// </summary>
    public IReadOnlyList<Coverage> Coverage { get; }

    /// <summary>
    /// What was left out, one plain sentence each, such as a rule that was not run.
    /// </summary>
    public IReadOnlyList<string> Skipped { get; }

    /// <summary>Whether the review intentionally omitted the checks that require a build.</summary>
    public bool IsQuick { get; internal init; }

    /// <summary>
    /// A short account of the review.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// How long the review took.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Source text supplied by path for a result constructed without tree data. Use <see cref="SourceOf" /> for a finding.
    /// </summary>
    public IReadOnlyDictionary<string, string> Sources { get; }

    /// <summary>
    /// Whether the working folder held changes that were not committed when a folder was reviewed: they are not part of the review.
    /// </summary>
    public bool LeavesOutUncommittedWork { get; init; }

    /// <summary>
    /// How many findings each rule has, the rule with the most first and rules with as many by identifier.
    /// </summary>
    public IReadOnlyList<RuleCount> CountByRule()
    {
        return Findings.GroupBy(finding => finding.RuleId, StringComparer.Ordinal)
            .Select(group => new RuleCount(group.Key, group.Count()))
            .OrderByDescending(count => count.Count)
            .ThenBy(count => count.RuleId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The exit code that goes with the outcome.
    /// </summary>
    public ExitCode ExitCode
    {
        get { return Outcome.ToExitCode(); }
    }
}
