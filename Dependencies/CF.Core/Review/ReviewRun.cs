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
    private readonly Dictionary<JudgedKey, List<string>> _findingTrees = new();
    private readonly List<Finding> _pending = new List<Finding>();
    private readonly List<ExemptedFinding> _exempted = new List<ExemptedFinding>();
    private readonly ExemptionFilter _exemptions = new ExemptionFilter();
    private readonly List<Coverage> _coverage = new List<Coverage>();
    private readonly List<string> _notes = new List<string>();
    private readonly List<string> _skipped = new List<string>();
    private readonly List<string> _advice = new List<string>();
    private bool _uncommittedWork;

    public ReviewRun(bool quick = false)
    {
        Quick = quick;
        if(quick)
            _skipped.Add("Quick review left out the build, .editorconfig diagnostics, rules that need the compiler, and structure rules.");
    }

    public bool Quick { get; }

    public bool RanBuild { get; set; }

    public string? Tree { get; set; }

    private readonly Dictionary<(string? Tree, string Path), string> _sources = new();
    private readonly Dictionary<Finding, string?> _reportedTrees = new();
    private readonly Dictionary<Finding, ReviewedRule> _rules = new();
    private readonly HashSet<FindingKey> _pendingKeys = new HashSet<FindingKey>();
    private readonly Dictionary<JudgedKey, int> _findingPositions = new Dictionary<JudgedKey, int>();
    private readonly HashSet<CoverageKey> _coverageKeys = new HashSet<CoverageKey>();
    private readonly Dictionary<FindingKey, StructureBreach> _structureBreaches = new Dictionary<FindingKey, StructureBreach>();
    private bool _judgedStructure;
    private bool _missedStructure;

    /// <summary>
    /// What makes two findings of one tree the same finding.
    /// </summary>
    private readonly record struct FindingKey(string RuleId, string? File, int? Line, int? Column, string Message);

    /// <summary>
    /// What makes two findings the same finding across trees: the same place and message, the same quoted source line
    /// and the same definition of the rule.
    /// </summary>
    private readonly record struct JudgedKey(FindingKey Place, string? QuotedLine, string Definition);

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

    public void ApplyPolicies(Strictness strictness, Func<Finding, IReadOnlyList<SourceExemption>>? readSource = null,
        IReadOnlyList<RuleDefinition>? activeRules = null, Func<IReadOnlyList<FileRule>>? readFileRules = null)
    {
        IReadOnlyList<FileRule>? fileRules = null;
        foreach(var finding in _pending)
        {
            var exempted = _exemptions.Match(finding, readSource);
            if(exempted != null)
                _exempted.Add(exempted);
            else
            {
                var teamRule = finding.Source == FindingSource.CodeRule
                    ? activeRules?.FirstOrDefault(rule => rule.Id == finding.RuleId) : null;
                if(finding.Source == FindingSource.EditorConfig && fileRules == null)
                    fileRules = readFileRules?.Invoke() ?? [];

                var fileRule = finding.Source == FindingSource.EditorConfig
                    ? fileRules?.FirstOrDefault(rule => string.Equals(rule.Id, finding.RuleId, StringComparison.OrdinalIgnoreCase)) : null;
                var reviewedRule = teamRule != null || fileRule != null ? new ReviewedRule(teamRule, fileRule) : null;
                if(AddUnderStrictness(finding, strictness, reviewedRule) is { } judged && reviewedRule != null)
                    _rules[judged] = reviewedRule;
            }
        }

        _pending.Clear();
        _pendingKeys.Clear();
        _exemptions.Clear();
    }

    private Finding? AddUnderStrictness(Finding finding, Strictness strictness, ReviewedRule? rule)
    {
        var effect = StrictnessEffect(finding, strictness);
        if(effect == null)
            return null;

        var judged = effect == finding.Severity ? finding : finding.WithSeverity(effect.Value);
        var key = new JudgedKey(KeyOf(finding), QuotedLine(finding), RuleFingerprint.Of(rule));
        if(Tree != null && finding.Source == FindingSource.EditorConfig)
            RememberTree(key, Tree);

        if(_findingPositions.TryAdd(key, _findings.Count))
            _findings.Add(judged);
        else
        {
            var previous = _findings[_findingPositions[key]];
            if(judged.Severity != Severity.Block || previous.Severity == Severity.Block)
                return null;

            _reportedTrees.Remove(previous);
            _rules.Remove(previous);
            _findings[_findingPositions[key]] = judged;
        }

        _reportedTrees[judged] = Tree;
        return judged;
    }

    private void RememberTree(JudgedKey key, string tree)
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

    /// <summary>
    /// The text of the line the finding points at, in the tree under review; nothing when that source was not read.
    /// </summary>
    private string? QuotedLine(Finding finding)
    {
        if(finding.File == null || finding.Line is not { } line || !_sources.TryGetValue((Tree, finding.File), out var text))
            return null;

        var lines = text.Split('\n');
        return line >= 1 && line <= lines.Length ? lines[line - 1].TrimEnd('\r') : null;
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
        _sources[(Tree, file.Path)] = file.Content;
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

    public ReviewResult ToResult(TimeSpan duration, StructureJudgment? judgment = null, RepositoryLocation? repository = null, ChangeSet? onlyChange = null)
    {
        ApplyPolicies(Strictness.Standard);
        var outcome = DecideOutcome();
        var sources = new Dictionary<(string? Tree, string Path), string>();
        foreach(var finding in _findings)
        {
            if(finding.File != null && _sources.TryGetValue((_reportedTrees[finding], finding.File), out var text))
                sources[(_reportedTrees[finding], finding.File)] = text;
        }

        return new ReviewResult(outcome, _findings, _coverage, _skipped, Summarize(), duration)
        {
            ReviewedRules = _rules,
            IsQuick = Quick,
            ReportedTrees = _reportedTrees,
            ReviewedSources = sources,
            StructureJudgment = judgment,
            LeavesOutUncommittedWork = _uncommittedWork,
            ExemptedFindings = _exempted,
            Repository = repository,
            TreeReviewed = onlyChange?.Tree ?? string.Empty,
            BaseTreeReviewed = onlyChange?.BeforeTree ?? string.Empty,
            CommitReviewed = onlyChange?.Commit ?? string.Empty,
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
