using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Changes the parts of a rule that the options name.
/// </summary>
internal sealed class RulesSetCommand
{
    private readonly IRuleService _ruleService;
    private readonly TextWriter _output;
    private readonly string _settingsPath;
    private readonly string _ruleId;
    private readonly RuleFieldArguments _fields;

    public RulesSetCommand(IRuleService ruleService, TextWriter output, string settingsPath, string ruleId, RuleFieldArguments fields)
    {
        _ruleService = ruleService;
        _output = output;
        _settingsPath = settingsPath;
        _ruleId = ruleId;
        _fields = fields;
    }

    public ExitCode Run()
    {
        _ruleService.ChangeRule(_settingsPath, _ruleId, _fields.ToChange());
        _output.WriteLine($"Changed rule {_ruleId}.");

        return ExitCode.Passed;
    }
}