using System.Text.Json;
using CF.Core.Findings;
using CF.Core.Review;

namespace CF.Cli.Console;

/// <summary>
/// Writes a review result as a SARIF 2.1.0 log, the standard that build servers and code hosts read to show findings on
/// the lines they are about. One run holds the findings as results, the findings set aside by allow comments as accepted
/// suppressions, and, in namespaced properties, the verdict and what was covered.
/// </summary>
internal sealed class SarifReporter
{
    private const string _schema = "https://json.schemastore.org/sarif-2.1.0.json";
    private const string _sourceRoot = "%SRCROOT%";

    private readonly TextWriter _output;
    private readonly string _version;
    private readonly SarifPaths _paths;
    private readonly bool _namesRevision;

    /// <param name="output">Where the log is written.</param>
    /// <param name="version">The version of the program that reviewed.</param>
    /// <param name="paths">What the log may say about places.</param>
    /// <param name="namesRevision">Whether the review was of a push or a range, whose head commit is the revision a code host
    /// must associate the upload with.</param>
    public SarifReporter(TextWriter output, string version, SarifPaths paths, bool namesRevision = false)
    {
        _output = output;
        _version = version;
        _paths = paths;
        _namesRevision = namesRevision;
    }

    private sealed record Entry(Finding Finding, ExemptedFinding? Exempted);

    public void Write(ReviewResult result)
    {
        var all = result.Findings.Select(finding => new Entry(finding, null))
            .Concat(result.ExemptedFindings.Select(exempted => new Entry(exempted.Finding, exempted)))
            .ToList();
        var rules = all.Select(entry => entry.Finding.RuleId).Distinct(StringComparer.Ordinal).ToList();
        var run = new SarifRun(
            new SarifTool(new SarifDriver("CommitFence", _version, rules.Select(id => DescribeRule(result, all.First(entry => entry.Finding.RuleId == id).Finding)).ToList())),
            [CreateInvocation(result)],
            all.Select(entry => CreateResult(entry.Finding, entry.Exempted, rules.IndexOf(entry.Finding.RuleId))).ToList(),
            CreateProperties(result),
            CreateProvenance(result));

        var json = JsonSerializer.Serialize(new SarifLog(_schema, "2.1.0", [run]), SarifJsonContext.Default.SarifLog);
        _output.Write(json.Replace("\n", Environment.NewLine, StringComparison.Ordinal) + Environment.NewLine);
    }

    private SarifRule DescribeRule(ReviewResult result, Finding finding)
    {
        var reviewed = result.RuleOf(finding);
        var title = reviewed?.Team?.Title ?? reviewed?.File?.Title ?? finding.RuleId;
        return new SarifRule(finding.RuleId, new SarifText(_paths.CleanAuthored(title)), new SarifRuleProperties(SourceOf(finding.Source)));
    }

    private static string SourceOf(FindingSource source)
    {
        return source switch
        {
            FindingSource.Compiler => "compiler",
            FindingSource.EditorConfig => ".editorconfig",
            FindingSource.StructureRule => "structure",
            FindingSource.Exemption => "exemption",
            FindingSource.CodeRule or FindingSource.Pack => "team",
            _ => "team"
        };
    }

    private SarifResult CreateResult(Finding finding, ExemptedFinding? exempted, int ruleIndex)
    {
        var level = finding.Severity == Severity.Block ? "error" : "warning";
        var location = CreateLocation(finding.File, finding.Line, finding.Column);
        var suppressions = exempted == null ? null : CreateSuppressions(finding, exempted);

        return new SarifResult(finding.RuleId, ruleIndex, level, new SarifText(CleanMessage(finding)), location == null ? null : [location], suppressions);
    }

    // the build's own words may carry a path of the machine it ran on; a rule's message is the team's
    private string CleanMessage(Finding finding)
    {
        return finding.Source is FindingSource.Compiler or FindingSource.EditorConfig ? _paths.Clean(finding.Message) : _paths.CleanAuthored(finding.Message);
    }

    // the suppression is told even when the place of its comment cannot be given
    private IReadOnlyList<SarifSuppression> CreateSuppressions(Finding finding, ExemptedFinding exempted)
    {
        var comment = CreateLocation(finding.File, exempted.CommentLine, null);
        return [new SarifSuppression("inSource", "accepted", _paths.CleanAuthored(exempted.Reason), comment)];
    }

    private static SarifLocation? CreateLocation(string? file, int? line, int? column)
    {
        var uri = file == null ? null : SarifPaths.RelativeUri(file);
        if(uri == null)
            return null;

        var region = line is > 0 ? new SarifRegion(line.Value, column is > 0 ? column : null) : null;
        return new SarifLocation(new SarifPhysicalLocation(new SarifArtifactLocation(uri, _sourceRoot), region));
    }

    private SarifInvocation CreateInvocation(ReviewResult result)
    {
        // what could not be covered is told whatever the verdict: a blocked review can be incomplete as well
        var notifications = result.Coverage.Where(entry => entry.Status != CoverageStatus.Reviewed).Select(CreateNotification).ToList();

        return new SarifInvocation(notifications.Count == 0, (int)result.ExitCode, notifications.Count == 0 ? null : notifications);
    }

    private SarifNotification CreateNotification(Coverage entry)
    {
        var level = entry.Status == CoverageStatus.Failed ? "error" : "warning";
        return new SarifNotification(level, new SarifText(_paths.Clean($"{entry.What}: {entry.Reason}")));
    }

    private IReadOnlyList<SarifVersionControl>? CreateProvenance(ReviewResult result)
    {
        if(!_namesRevision || result.CommitReviewed.Length == 0 || result.RemoteAddress.Length == 0)
            return null;

        return [new SarifVersionControl(result.RemoteAddress, result.CommitReviewed)];
    }

    private SarifRunProperties CreateProperties(ReviewResult result)
    {
        var notReviewed = result.Coverage.Where(entry => entry.Status != CoverageStatus.Reviewed || entry.Kind == CoverageKind.Build).Select(CreateEntry).ToList();
        var coverage = new SarifCoverage(result.Coverage.Count(entry => entry.Status == CoverageStatus.Reviewed), notReviewed);

        return new SarifRunProperties(
            result.Outcome.ToString().ToLowerInvariant(),
            (int)result.ExitCode,
            result.IsQuick,
            NullWhenEmpty(result.CommitReviewed),
            NullWhenEmpty(result.TreeReviewed),
            NullWhenEmpty(result.BaseTreeReviewed),
            result.LeavesOutUncommittedWork,
            _paths.Clean(result.Summary),
            result.Skipped.Select(_paths.Clean).ToList(),
            coverage);
    }

    private SarifCoverageEntry CreateEntry(Coverage entry)
    {
        return new SarifCoverageEntry(_paths.Clean(entry.What), entry.Kind.ToString().ToLowerInvariant(), entry.Status.ToString().ToLowerInvariant(), _paths.Clean(entry.Reason));
    }

    private static string? NullWhenEmpty(string value)
    {
        return value.Length == 0 ? null : value;
    }
}
