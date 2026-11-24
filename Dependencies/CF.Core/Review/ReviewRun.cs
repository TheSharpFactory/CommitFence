using CF.Core.Findings;

namespace CF.Core.Review;

/// <summary>
/// Collects what one review finds and what it could not cover, and turns it into the result.
/// </summary>
internal sealed class ReviewRun
{
    private readonly List<Finding> _findings = new List<Finding>();
    private readonly List<Coverage> _coverage = new List<Coverage>();
    private readonly List<string> _notes = new List<string>();
    private readonly List<string> _skipped = new List<string>();
    private readonly List<string> _advice = new List<string>();
    private bool _uncommittedWork;
    private readonly Dictionary<string, string> _sources = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<FindingKey> _findingKeys = new HashSet<FindingKey>();
    private readonly HashSet<CoverageKey> _coverageKeys = new HashSet<CoverageKey>();

    /// <summary>
    /// What makes two findings the same finding.
    /// </summary>
    private readonly record struct FindingKey(string RuleId, string? File, int? Line, int? Column, string Message);

    /// <summary>
    /// What makes two coverage entries the same entry.
    /// </summary>
    private readonly record struct CoverageKey(string What, CoverageStatus Status, string Reason, CoverageKind Kind);

    public void AddFindings(IEnumerable<Finding> findings)
    {
        var ordered = findings.OrderBy(finding => finding.Line).ThenBy(finding => finding.Column).ThenBy(finding => finding.RuleId, StringComparer.Ordinal);
        foreach(var finding in ordered)
        {
            if(_findingKeys.Add(new FindingKey(finding.RuleId, finding.File, finding.Line, finding.Column, finding.Message)))
                _findings.Add(finding);
        }
    }

    public void AddCoverage(string what, CoverageStatus status, string reason)
    {
        AddCoverage(new Coverage(what, status, reason));
    }

    public void AddCoverage(Coverage entry)
    {
        if(_coverageKeys.Add(new CoverageKey(entry.What, entry.Status, entry.Reason, entry.Kind)))
            _coverage.Add(entry);
    }

    public void MarkUncommittedWork()
    {
        _uncommittedWork = true;
    }

    public void RememberSource(string path, string content)
    {
        _sources[path] = content;
    }

    public void AddNote(string note)
    {
        _notes.Add(note);
    }

    public void AddNotRun(string ruleId, string reason)
    {
        var sentence = $"{ruleId} was not run: {reason}";
        if(!_skipped.Contains(sentence))
            _skipped.Add(sentence);

        AddCoverage(new Coverage("rule " + ruleId, CoverageStatus.Skipped, reason, CoverageKind.Rule));
    }

    /// <summary>
    /// How many rules this review did not run.
    /// </summary>
    public int CountNotRun()
    {
        return _skipped.Count;
    }

    /// <summary>
    /// A sentence for the end of the summary, on what the person can do about what the review could not do.
    /// </summary>
    public void AddAdvice(string sentence)
    {
        _advice.Add(sentence);
    }

    public ReviewResult ToResult(TimeSpan duration)
    {
        var outcome = DecideOutcome();
        var sources = _sources.Where(source => _findings.Any(finding => string.Equals(finding.File, source.Key, StringComparison.Ordinal))).ToDictionary(source => source.Key, source => source.Value, StringComparer.Ordinal);
        return new ReviewResult(outcome, _findings, _coverage, _skipped, Summarize(), duration, sources) { LeavesOutUncommittedWork = _uncommittedWork };
    }

    private ReviewOutcome DecideOutcome()
    {
        if(_findings.Any(finding => finding.Severity == Severity.Block))
            return ReviewOutcome.Blocked;

        if(CountNotReviewed() > 0)
            return ReviewOutcome.Incomplete;

        return _findings.Count > 0 ? ReviewOutcome.Warned : ReviewOutcome.Clean;
    }

    private int CountNotReviewed()
    {
        return _coverage.Count(entry => entry.Status != CoverageStatus.Reviewed);
    }

    private string Summarize()
    {
        if(_coverage.Count == 0 && _notes.Count > 0)
            return string.Join(" ", _notes);

        var blocking = _findings.Count(finding => finding.Severity == Severity.Block);
        var warnings = _findings.Count - blocking;
        var notReviewed = CountNotReviewed();
        var summary = $"{DescribeCoverage()}: {blocking} blocking, {warnings} warning.";
        if(notReviewed > 0)
            summary += $" {notReviewed} could not be reviewed, so the review is incomplete.";

        return string.Join(" ", _notes.Append(summary).Concat(_advice));
    }

    private string DescribeCoverage()
    {
        var files = _coverage.Where(entry => entry.Kind == CoverageKind.File).ToList();
        var builds = _coverage.Where(entry => entry.Kind == CoverageKind.Build).ToList();
        var parts = new List<string>();
        if(files.Count > 0 || builds.Count == 0)
            parts.Add($"{CountReviewed(files)} of {files.Count} files reviewed");

        if(builds.Count > 0)
            parts.Add($"{CountReviewed(builds)} of {builds.Count} builds checked");

        return string.Join(", ", parts);
    }

    private static int CountReviewed(IReadOnlyList<Coverage> entries)
    {
        return entries.Count(entry => entry.Status == CoverageStatus.Reviewed);
    }
}