using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Prints one line for each rule of the settings file, then the rules the repository's own files set.
/// </summary>
internal sealed class RulesListCommand
{
    private readonly IRuleService _ruleService;
    private readonly TextWriter _output;
    private readonly string _settingsPath;

    public RulesListCommand(IRuleService ruleService, TextWriter output, string settingsPath)
    {
        _ruleService = ruleService;
        _output = output;
        _settingsPath = settingsPath;
    }

    public ExitCode Run()
    {
        var rules = _ruleService.ListRules(_settingsPath);
        var fileListings = _ruleService.ListFileRules(_settingsPath);
        WriteTeamRules(rules);
        foreach(var line in fileListings.SelectMany(FileRuleTextFormatter.FormatListing))
            _output.WriteLine(line);

        return ExitCode.Passed;
    }

    private void WriteTeamRules(IReadOnlyList<RuleDefinition> rules)
    {
        if(rules.Count == 0)
            _output.WriteLine("The settings file has no rules.");

        var idWidth = rules.Count == 0 ? 0 : rules.Max(rule => rule.Id.Length);
        foreach(var rule in rules)
            _output.WriteLine(RuleTextFormatter.FormatListLine(rule, idWidth));
    }
}