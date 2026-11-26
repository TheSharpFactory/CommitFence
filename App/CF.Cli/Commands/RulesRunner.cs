using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Runs the <c>rules</c> commands: reads the arguments, finds the settings file and hands the subcommand to its class.
/// A refusal of the rule service is printed as one plain message with exit code 3.
/// </summary>
internal sealed class RulesRunner
{
    private readonly IRuleService _ruleService;
    private readonly CommandEnvironment _environment;
    private readonly Func<string> _defaultSettingsPath;

    public RulesRunner(IRuleService ruleService, CommandEnvironment environment, Func<string> defaultSettingsPath)
    {
        _ruleService = ruleService;
        _environment = environment;
        _defaultSettingsPath = defaultSettingsPath;
    }

    public ExitCode Run(string[] args)
    {
        var arguments = RulesArguments.Parse(args);
        if(arguments.Error != null)
            return Fail(arguments.Error + Environment.NewLine + UsageText.Text);

        var settingsPath = arguments.SettingsPath == null ? _defaultSettingsPath() : Path.GetFullPath(arguments.SettingsPath, _environment.CurrentFolder);
        try
        {
            return RunSubcommand(arguments, settingsPath);
        }
        catch(SettingsException exception)
        {
            return Fail(SettingsFileName.FormatError(settingsPath, exception));
        }
        catch(RuleChangeException exception)
        {
            return Fail(exception.Message);
        }
    }

    private ExitCode RunSubcommand(RulesArguments arguments, string settingsPath)
    {
        var output = _environment.Output;
        switch(arguments.Subcommand)
        {
            case "list":
                return new RulesListCommand(_ruleService, output, settingsPath).Run();
            case "enable":
                return new RulesEnableCommand(_ruleService, output, settingsPath, arguments.RuleId, true).Run();
            case "disable":
                return new RulesEnableCommand(_ruleService, output, settingsPath, arguments.RuleId, false).Run();
            case "severity":
                return new RulesSeverityCommand(_ruleService, output, settingsPath, arguments.RuleId, arguments.Severity!.Value).Run();
            case "add":
                return new RulesAddCommand(_ruleService, output, settingsPath, arguments.RuleId, arguments.Fields, arguments.Before).Run();
            case "copy":
                return new RulesCopyCommand(_ruleService, output, settingsPath, arguments.RuleId, arguments.NewRuleId!).Run();
            case "set":
                return new RulesSetCommand(_ruleService, output, settingsPath, arguments.RuleId, arguments.Fields).Run();
            case "remove":
                return new RulesRemoveCommand(_ruleService, output, settingsPath, arguments.RuleId).Run();
            case "try":
                return new RulesTryCommand(_ruleService, _environment, settingsPath, arguments.RuleId, arguments.File).Run();
            case "show":
                return new RulesShowCommand(_ruleService, output, _environment.Error, settingsPath, arguments.RuleId).Run();
            default:
                return Fail($"rules {arguments.Subcommand} is not a command." + Environment.NewLine + UsageText.Text);
        }
    }

    private ExitCode Fail(string message)
    {
        _environment.Error.WriteLine(message);
        return ExitCode.BadSettingsOrUsage;
    }
}