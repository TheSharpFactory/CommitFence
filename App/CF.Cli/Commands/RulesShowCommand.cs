using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Prints one rule of the settings file in full.
/// </summary>
internal sealed class RulesShowCommand
{
    private readonly IRuleService _ruleService;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly string _settingsPath;
    private readonly string _ruleId;

    public RulesShowCommand(IRuleService ruleService, TextWriter output, TextWriter error, string settingsPath, string ruleId)
    {
        _ruleService = ruleService;
        _output = output;
        _error = error;
        _settingsPath = settingsPath;
        _ruleId = ruleId;
    }

    public ExitCode Run()
    {
        var rule = _ruleService.ShowRule(_settingsPath, _ruleId);
        if(rule == null)
            return ShowFileRule();

        _output.WriteLine(RuleTextFormatter.FormatDetail(rule));
        return ExitCode.Passed;
    }

    private ExitCode ShowFileRule()
    {
        var fileRule = _ruleService.ShowFileRule(_settingsPath, _ruleId);
        if(fileRule == null)
        {
            _error.WriteLine($"The settings file has no rule '{_ruleId}'.");
            return ExitCode.BadSettingsOrUsage;
        }

        _output.WriteLine(FileRuleTextFormatter.FormatDetail(fileRule));
        return ExitCode.Passed;
    }
}