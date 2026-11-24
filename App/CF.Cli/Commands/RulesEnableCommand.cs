using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Switches a rule on or off, and says which.
/// </summary>
internal sealed class RulesEnableCommand
{
    private readonly IRuleService _ruleService;
    private readonly TextWriter _output;
    private readonly string _settingsPath;
    private readonly string _ruleId;
    private readonly bool _enabled;

    public RulesEnableCommand(IRuleService ruleService, TextWriter output, string settingsPath, string ruleId, bool enabled)
    {
        _ruleService = ruleService;
        _output = output;
        _settingsPath = settingsPath;
        _ruleId = ruleId;
        _enabled = enabled;
    }

    public ExitCode Run()
    {
        _ruleService.SetEnabled(_settingsPath, _ruleId, _enabled);
        _output.WriteLine($"{_ruleId} is now {(_enabled ? "on" : "off")}.");

        return ExitCode.Passed;
    }
}