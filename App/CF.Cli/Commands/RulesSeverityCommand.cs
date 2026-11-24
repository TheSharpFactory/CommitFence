using CF.Core.Findings;
using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Makes a rule block the push or only warn, and says which.
/// </summary>
internal sealed class RulesSeverityCommand
{
    private readonly IRuleService _ruleService;
    private readonly TextWriter _output;
    private readonly string _settingsPath;
    private readonly string _ruleId;
    private readonly Severity _severity;

    public RulesSeverityCommand(IRuleService ruleService, TextWriter output, string settingsPath, string ruleId, Severity severity)
    {
        _ruleService = ruleService;
        _output = output;
        _settingsPath = settingsPath;
        _ruleId = ruleId;
        _severity = severity;
    }

    public ExitCode Run()
    {
        _ruleService.SetSeverity(_settingsPath, _ruleId, _severity);
        _output.WriteLine($"{_ruleId} now {(_severity == Severity.Block ? "blocks the push" : "only warns")}.");

        return ExitCode.Passed;
    }
}