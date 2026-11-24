using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Adds a copy of a rule right after it, with a new id.
/// </summary>
internal sealed class RulesCopyCommand
{
    private readonly IRuleService _ruleService;
    private readonly TextWriter _output;
    private readonly string _settingsPath;
    private readonly string _ruleId;
    private readonly string _newRuleId;

    public RulesCopyCommand(IRuleService ruleService, TextWriter output, string settingsPath, string ruleId, string newRuleId)
    {
        _ruleService = ruleService;
        _output = output;
        _settingsPath = settingsPath;
        _ruleId = ruleId;
        _newRuleId = newRuleId;
    }

    public ExitCode Run()
    {
        _ruleService.CopyRule(_settingsPath, _ruleId, _newRuleId);
        _output.WriteLine($"Copied rule {_ruleId} as {_newRuleId}.");

        return ExitCode.Passed;
    }
}
