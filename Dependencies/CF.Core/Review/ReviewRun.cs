using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Rules;
using CF.Core.Structure;

namespace CF.Core.Review;

/// <summary>
/// Collects what one review finds and what it could not cover, and turns it into the result.
/// </summary>
internal sealed class ReviewRun
{
    private readonly List<Finding> _findings = new List<Finding>();
    private readonly Dictionary<FindingKey, List<string>> _findingTrees = new();
    private readonly List<Finding> _pending = new List<Finding>();
    private readonly List<ExemptedFinding> _exempted = new List<ExemptedFinding>();
    private readonly ExemptionFilter _exemptions = new ExemptionFilter();
    private readonly List<Coverage> _coverage = new List<Coverage>();
    private readonly List<string> _notes = new List<string>();
    private readonly List<string> _skipped = new List<string>();
    private readonly List<string> _advice = new List<string>();
    private bool _uncommittedWork;

    public bool RanBuild { get; set; }

    public string? Tree { get; set; }

    private readonly Dictionary<string, string> _sources = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<FindingKey> _pendingKeys = new HashSet<FindingKey>();
    private readonly Dictionary<FindingKey, int> _findingPositions = new Dictionary<FindingKey, int>();
    private readonly HashSet<CoverageKey> _coverageKeys = new HashSet<CoverageKey>();
    private readonly Dictionary<FindingKey, StructureBreach> _structureBreaches = new Dictionary<FindingKey, StructureBreach>();
    private bool _judgedStructure;
    private bool _missedStructure;

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
            if(_pendingKeys.Add(KeyOf(finding)))
                _pending.Add(finding);
        }
    }

    public void AddStructureFinding(StructureViolation violation)
    {
        var finding = StructureFindings.Create(violation);
        var use = violation.Use;
        _structureBreaches.TryAdd(KeyOf(finding), new StructureBreach(violation.FromModule, violation.ToModule, use.Member, use.UsedType, use.File, violation.Place.Line));
        if(_pendingKeys.Add(KeyOf(finding)))
            _pending.Add(finding);
    }

    public void MarkStructureJudged()
    {
        _judgedStructure = true;
    }

    public void MarkStructureIncomplete()
    {
        _missedStructure = true;
    }

    public StructureJudgment? StructureJudgedAt(DateTimeOffset endedAt, string? folder)
    {
        if(!_judgedStructure || _missedStructure)
            return null;

        var breaches = _findings.Where(finding => finding.Source == FindingSource.StructureRule)
            .Select(finding => _structureBreaches.GetValueOrDefault(KeyOf(finding))).OfType<StructureBreach>().ToList();
        return new StructureJudgment(endedAt, folder, breaches);
    }

    public void ApplyPolicies(Strictness strictness, Func<Finding, IReadOnlyList<SourceExemption>>? readSource = null)
    {
        foreach(var finding in _pending)
        {
            var exempted = _exemptions.Match(finding, readSource);
            if(exempted != null)
                _exempted.Add(exempted);
            else
                AddUnderStrictness(finding, strictness);
        }

        _pending.Clear();
        _pendingKeys.Clear();
        _exemptions.Clear();
    }

    private void AddUnderStrictness(Finding finding, Strictness strictness)
    {
        var effect = StrictnessEffect(finding, strictness);
        if(effect == null)
            return;

        var judged = effect == finding.Severity ? finding : finding.WithSeverity(effect.Value);
        var key = KeyOf(finding);
        if(_findingPositions.TryAdd(key, _findings.Count))
            _findings.Add(judged);
        else if(judged.Severity == Severity.Block && _findings[_findingPositions[key]].Severity == Severity.Warn)
            _findings[_findingPositions[key]] = judged;

        if(Tree != null && finding.Source == FindingSource.EditorConfig)
            RememberTree(key, Tree);
    }

    private void RememberTree(FindingKey key, string tree)
    {
        if(!_findingTrees.TryGetValue(key, out var trees))
        {
            trees = new List<string>();
            _findingTrees.Add(key, trees);
        }

        if(!trees.Contains(tree, StringComparer.Ordinal))
            trees.Add(tree);
    }

    private static Severity? StrictnessEffect(Finding finding, Strictness strictness)
    {
        if(finding.Source != FindingSource.EditorConfig || finding.FileSeverity == null)
            return finding.Severity;

        return strictness.EffectOf(finding.FileSeverity);
    }

    private static FindingKey KeyOf(Finding finding)
    {
        return new FindingKey(finding.RuleId, finding.File, finding.Line, finding.Column, finding.Message);
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

    public void RememberSource(ILanguage language, CodeFile file, IReadOnlyList<LineRange>? changedLines)
    {
        _sources[file.Path] = file.Content;
        AddFindings(_exemptions.Remember(language, file, changedLines));
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

    public ReviewResult ToResult(TimeSpan duration, StructureJudgment? judgment = null, RepositoryLocation? repository = null)
    {
        ApplyPolicies(Strictness.Standard);
        var outcome = DecideOutcome();
        var sources = _sources.Where(source => _findings.Any(finding => string.Equals(finding.File, source.Key, StringComparison.Ordinal))).ToDictionary(source => source.Key, source => source.Value, StringComparer.Ordinal);
        return new ReviewResult(outcome, _findings, _coverage, _skipped, Summarize(), duration, sources)
        {
            StructureJudgment = judgment,
            LeavesOutUncommittedWork = _uncommittedWork,
            ExemptedFindings = _exempted,
            Repository = repository,
            FindingTrees = _findingTrees.ToDictionary(entry => _findings[_findingPositions[entry.Key]], entry => (IReadOnlyList<string>)entry.Value)
        };
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
        if(_coverage.Count == 0 && _notes.Count > 0 && _exempted.Count == 0)
            return string.Join(" ", _notes);

        var blocking = _findings.Count(finding => finding.Severity == Severity.Block);
        var warnings = _findings.Count - blocking;
        var notReviewed = CountNotReviewed();
        var summary = $"{DescribeCoverage()}: {blocking} blocking, {warnings} warning.";
        if(_exempted.Count > 0)
            summary += $" {_exempted.Count} {(_exempted.Count == 1 ? "finding" : "findings")} set aside by an allow comment.";

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
