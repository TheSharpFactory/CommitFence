using System.Text;
using CF.Core.Code;
using CF.Core.Findings;

namespace CF.Core.Prompts;

/// <summary>
/// Writes what a review found as text that an AI coding agent can act on: the prompt for one finding, and the prompt for a
/// whole review. It only writes text: it reads no file, no clock and calls no model. Lines end with a line feed.
/// </summary>
public static class AgentPrompt
{
    private const int _linesAround = 2;
    private const string _opening = "CommitFence, the code review that runs before a push in this repository, found ";
    private const string _closing = "Change the code so that it follows the rule, and change nothing else. Do not silence the rule: no pragma, no allow comment, no change to .editorconfig or commitfence.json.";
    private const string _nothing = "CommitFence found nothing to fix.";
    private const string _nothingButIncomplete = "CommitFence found nothing to fix, but it could not check everything.";

    /// <summary>
    /// The block of one finding: the rule, what it does to a push, where the finding is, what was found and the code around it.
    /// </summary>
    /// <param name="finding">The finding.</param>
    /// <param name="rule">What is known of its rule, or nothing.</param>
    /// <param name="source">The text of the finding's file as the reviewed tree held it, or nothing when it is not at hand.</param>
    public static string Block(Finding finding, PromptRule? rule, string? source)
    {
        var lines = new List<string> { Heading(finding, rule) };
        if(rule?.Sentence != null)
            lines.Add("The rule: " + rule.Sentence);

        lines.Add(finding.Severity == Severity.Block ? "It blocks a push." : "It only warns.");
        if(finding.File != null)
            lines.Add(Where(finding));

        lines.Add("What was found: " + finding.Message);
        var code = Code(finding, source);
        return code.Count == 0 ? string.Join("\n", lines) : string.Join("\n", lines) + "\n\nThe code (the line is marked):\n" + string.Join("\n", code);
    }

    /// <summary>
    /// The prompt for one finding.
    /// </summary>
    public static string ForFinding(Finding finding, PromptRule? rule, string? source)
    {
        return _opening + "this. Fix it.\n\n" + Block(finding, rule, source) + "\n\n" + _closing;
    }

    /// <summary>
    /// The prompt for a whole review, the findings in the order given. Nothing found is one line, which says so when the review could not check everything.
    /// </summary>
    /// <param name="findings">The findings.</param>
    /// <param name="sources">The text of the files that have findings, by the path a finding names.</param>
    /// <param name="rules">What is known of the rules, by rule id.</param>
    /// <param name="isIncomplete">Whether the review could not check everything.</param>
    public static string ForReview(IReadOnlyList<Finding> findings, IReadOnlyDictionary<string, string> sources, IReadOnlyDictionary<string, PromptRule> rules, bool isIncomplete)
    {
        return ForReview(findings, finding => finding.File == null ? null : sources.GetValueOrDefault(finding.File),
            finding => rules.GetValueOrDefault(finding.RuleId), isIncomplete);
    }

    /// <summary>Writes the findings with the rule and source that each finding was judged with.</summary>
    public static string ForReview(IReadOnlyList<Finding> findings, Func<Finding, string?> sourceOf, Func<Finding, PromptRule?> ruleOf, bool isIncomplete, IReadOnlyList<Coverage>? coverage = null)
    {
        var problems = coverage?.Where(entry => entry.Status != CoverageStatus.Reviewed).ToList() ?? [];
        if(findings.Count == 0)
            return AddCoverage(new StringBuilder(isIncomplete || problems.Count > 0 ? _nothingButIncomplete : _nothing), problems);

        var builder = new StringBuilder();
        builder.Append(Opening(findings)).Append("\n\n");
        for(var index = 0; index < findings.Count; index++)
        {
            var finding = findings[index];
            builder.Append($"{index + 1} of {findings.Count}\n").Append(Block(finding, ruleOf(finding), sourceOf(finding))).Append("\n\n");
        }

        return AddCoverage(builder.Append(_closing), problems);
    }

    private static string AddCoverage(StringBuilder builder, List<Coverage> problems)
    {
        if(problems.Count > 0)
        {
            builder.Append("\n\nWhat could not be checked:\n");
            builder.AppendJoin('\n', problems.Select(entry => $"- {entry.What}: {entry.Reason}"));
        }

        return builder.ToString();
    }

    private static string Opening(IReadOnlyList<Finding> findings)
    {
        var files = findings.Where(finding => finding.File != null).Select(finding => finding.File).Distinct(StringComparer.Ordinal).Count();
        var things = findings.Count == 1 ? "1 thing" : $"{findings.Count} things";
        var inFiles = files == 1 ? "1 file" : $"{files} files";
        return $"{_opening}{things} in {inFiles}. Fix {(findings.Count == 1 ? "it" : "them")}.";
    }

    private static string Heading(Finding finding, PromptRule? rule)
    {
        return string.IsNullOrEmpty(rule?.Title) ? finding.RuleId : $"{finding.RuleId}: {rule.Title}";
    }

    private static string Where(Finding finding)
    {
        var path = finding.File!.Replace('\\', '/');
        return finding.Line == null ? $"Where: {path}" : $"Where: {path}, line {finding.Line.Value}";
    }

    private static List<string> Code(Finding finding, string? source)
    {
        if(source == null || finding.File == null || finding.Line == null)
            return new List<string>();

        var lines = Excerpt.Around(source, finding.Line.Value, _linesAround);
        if(lines.Count == 0)
            return new List<string>();

        var width = lines[^1].Number.ToString().Length;
        return lines.Select(line => FormatLine(line, source, width, line.Number == finding.Line.Value)).ToList();
    }

    private static string FormatLine(ExcerptLine line, string source, int width, bool isMarked)
    {
        var marker = isMarked ? ">" : " ";
        return $"{marker}   {line.Number.ToString().PadLeft(width)} | {source.Substring(line.Start, line.End - line.Start)}";
    }
}