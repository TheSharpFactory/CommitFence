using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Adds a rule from the options given.
/// </summary>
internal sealed class RulesAddCommand
{
    private readonly IRuleService _ruleService;
    private readonly TextWriter _output;
    private readonly string _settingsPath;
    private readonly string _ruleId;
    private readonly RuleFieldArguments _fields;
    private readonly string? _before;

    public RulesAddCommand(IRuleService ruleService, TextWriter output, string settingsPath, string ruleId, RuleFieldArguments fields, string? before)
    {
        _ruleService = ruleService;
        _output = output;
        _settingsPath = settingsPath;
        _ruleId = ruleId;
        _fields = fields;
        _before = before;
    }

    public ExitCode Run()
    {
        if(_before == null)
            _ruleService.AddRule(_settingsPath, _fields.ToDraft(_ruleId));
        else
            _ruleService.AddRuleBefore(_settingsPath, _fields.ToDraft(_ruleId), _before);

        _output.WriteLine(_before == null ? $"Added rule {_ruleId}." : $"Added rule {_ruleId} before {_before}.");

        return ExitCode.Passed;
    }
}