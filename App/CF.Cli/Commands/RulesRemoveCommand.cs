using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Removes a rule.
/// </summary>
internal sealed class RulesRemoveCommand
{
    private readonly IRuleService _ruleService;
    private readonly TextWriter _output;
    private readonly string _settingsPath;
    private readonly string _ruleId;

    public RulesRemoveCommand(IRuleService ruleService, TextWriter output, string settingsPath, string ruleId)
    {
        _ruleService = ruleService;
        _output = output;
        _settingsPath = settingsPath;
        _ruleId = ruleId;
    }

    public ExitCode Run()
    {
        _ruleService.RemoveRule(_settingsPath, _ruleId);
        _output.WriteLine($"Removed rule {_ruleId}.");

        return ExitCode.Passed;
    }
}