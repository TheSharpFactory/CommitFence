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

    /// <summary>
    /// A short account of the review.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// How long the review took.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// The text of each file that has a finding, as the reviewed tree held it (never the working folder), by the
    /// path a finding names. It is empty for a review that read no tree.
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
