using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Prompts;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Services;

namespace CF.Cli.Console;

/// <summary>
/// Writes a review result as the prompt for an AI coding agent, and nothing else. The titles and sentences of the rules
/// come from the settings file at the path given; a rule that file does not know is named by its id alone.
/// </summary>
internal sealed class PromptReporter
{
    private readonly TextWriter _output;
    private readonly IRuleService _ruleService;
    private readonly string _settingsPath;

    public PromptReporter(TextWriter output, IRuleService ruleService, string settingsPath)
    {
        _output = output;
        _ruleService = ruleService;
        _settingsPath = settingsPath;
    }

    public void Write(ReviewResult result)
    {
        var prompt = AgentPrompt.ForReview(result.Findings, result.Sources, ReadRules(result.Findings), result.Outcome == ReviewOutcome.Incomplete);
        _output.Write(prompt.Replace("\n", Environment.NewLine) + Environment.NewLine);
    }

    private Dictionary<string, PromptRule> ReadRules(IReadOnlyList<Finding> findings)
    {
        var rules = new Dictionary<string, PromptRule>(StringComparer.Ordinal);
        AddTeamRules(rules, findings);
        if(findings.Any(finding => !IsTeamRule(finding)))
            AddFileRules(rules, findings);

        return rules;
    }

    private void AddTeamRules(Dictionary<string, PromptRule> rules, IReadOnlyList<Finding> findings)
    {
        try
        {
            var team = _ruleService.ListRules(_settingsPath).ToDictionary(rule => rule.Id, StringComparer.Ordinal);
            foreach(var finding in findings.Where(finding => IsTeamRule(finding) && team.ContainsKey(finding.RuleId)))
                rules[finding.RuleId] = PromptRule.Of(team[finding.RuleId]);
        }
        catch(SettingsException)
        {
            // without readable settings the rules are named by their ids
        }
    }

    private void AddFileRules(Dictionary<string, PromptRule> rules, IReadOnlyList<Finding> findings)
    {
        try
        {
            var file = _ruleService.ListFileRules(_settingsPath).SelectMany(listing => listing.Rules).GroupBy(rule => rule.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach(var finding in findings.Where(finding => !IsTeamRule(finding) && file.ContainsKey(finding.RuleId)))
                rules[finding.RuleId] = PromptRule.Of(file[finding.RuleId]);
        }
        catch(Exception exception) when(exception is SettingsException || exception is RuleChangeException)
        {
            // without the file's rules they are named by their ids
        }
    }

    private static bool IsTeamRule(Finding finding)
    {
        return finding.Source is FindingSource.CodeRule or FindingSource.StructureRule;
    }
}