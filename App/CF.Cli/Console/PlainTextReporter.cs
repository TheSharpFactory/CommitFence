using CF.Core.Findings;
using CF.Core.Review;

namespace CF.Cli.Console;

/// <summary>
/// Writes a review result as plain text, without colour: one line for each finding, one for each file that could not be
/// reviewed, one for each rule that was not run, optionally a line that counts the findings
/// by rule, and a summary line.
/// </summary>
/// <remarks>
/// A finding reads file(line,column): severity RULEID: message, so editors can jump to it.
/// </remarks>
internal sealed class PlainTextReporter
{
    private readonly TextWriter _output;
    private readonly bool _countByRule;

    public PlainTextReporter(TextWriter output, bool countByRule)
    {
        _output = output;
        _countByRule = countByRule;
    }

    public void Write(ReviewResult result)
    {
        foreach(var finding in result.Findings)
            _output.WriteLine(FormatFinding(finding));

        foreach(var entry in result.Coverage)
        {
            var line = FormatCoverage(entry);
            if(line != null)
                _output.WriteLine(line);
        }

        foreach(var sentence in result.Skipped)
            _output.WriteLine("not run: " + sentence);

        if(_countByRule && result.Findings.Count > 0)
            _output.WriteLine("By rule: " + string.Join(", ", result.CountByRule().Select(count => $"{count.RuleId} {count.Count}")));

        _output.WriteLine(result.Summary);
    }

    private static string? FormatCoverage(Coverage entry)
    {
        if(entry.Kind == CoverageKind.Rule)
            return null;

        if(entry.Kind == CoverageKind.Build)
            return FormatBuildCoverage(entry);

        return entry.Status == CoverageStatus.Reviewed ? null : $"{entry.What}: incomplete: {entry.Reason}";
    }

    private static string FormatBuildCoverage(Coverage entry)
    {
        var reason = entry.Reason.Length == 0 ? string.Empty : ": " + entry.Reason;
        return entry.Status == CoverageStatus.Reviewed ? $"{entry.What}: reviewed{reason}" : $"{entry.What}: not reviewed: {entry.Reason}";
    }

    private static string FormatFinding(Finding finding)
    {
        var where = finding.Project == null ? string.Empty : $" [{finding.Project}, {finding.TargetFramework}]";
        return $"{FormatLocation(finding)}: {FormatSeverity(finding.Severity)} {finding.RuleId}: {finding.Message}{where}";
    }

    private static string FormatLocation(Finding finding)
    {
        var file = finding.File ?? "(no file)";
        if(finding.Line == null)
            return file;

        return $"{file}({finding.Line},{finding.Column})";
    }

    private static string FormatSeverity(Severity severity)
    {
        return severity == Severity.Block ? "error" : "warning";
    }
}