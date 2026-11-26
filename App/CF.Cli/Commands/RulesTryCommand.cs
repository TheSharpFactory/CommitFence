using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Tries a rule on the code of a file, or of standard input, and prints one line for each artifact of the rule's type.
/// </summary>
internal sealed class RulesTryCommand
{
    private const string _standardInputName = "stdin";

    private readonly IRuleService _ruleService;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly string _settingsPath;
    private readonly string _ruleId;
    private readonly string? _file;
    private readonly string _currentFolder;

    public RulesTryCommand(IRuleService ruleService, CommandEnvironment environment, string settingsPath, string ruleId, string? file)
    {
        _ruleService = ruleService;
        _input = environment.Input;
        _output = environment.Output;
        _settingsPath = settingsPath;
        _ruleId = ruleId;
        _file = file;
        _currentFolder = environment.CurrentFolder;
    }

    public ExitCode Run()
    {
        var source = ReadSource();
        var name = _file == null ? null : ReportedPath(_file);
        var result = _ruleService.TryRule(_settingsPath, _ruleId, source, name);
        Print(result, name ?? _standardInputName);

        return result.Status == TryStatus.Tried ? ExitCode.Passed : ExitCode.Incomplete;
    }

    private string ReadSource()
    {
        if(_file == null)
            return _input.ReadToEnd();

        try
        {
            return File.ReadAllText(Path.GetFullPath(_file, _currentFolder));
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            throw new RuleChangeException($"The file '{_file}' cannot be read: {exception.Message}");
        }
    }

    private string ReportedPath(string file)
    {
        var repositoryFolder = Path.GetDirectoryName(_settingsPath) ?? _currentFolder;
        return Path.GetRelativePath(repositoryFolder, Path.GetFullPath(file, _currentFolder)).Replace('\\', '/');
    }

    private void Print(RuleTryResult result, string name)
    {
        if(result.Status == TryStatus.NeedsFileName)
        {
            _output.WriteLine(result.Reason);
            return;
        }

        if(result.Status == TryStatus.NeedsBuild)
        {
            _output.WriteLine($"{_ruleId}: cannot be tried without a build: {result.Reason}.");
            return;
        }

        if(result.Status == TryStatus.DoesNotParse)
        {
            _output.WriteLine($"{name}({result.Line},{result.Column}): does not parse: {result.Reason}");
            return;
        }

        PrintVerdicts(result, name);
    }

    private void PrintVerdicts(RuleTryResult result, string name)
    {
        foreach(var verdict in result.Verdicts)
            _output.WriteLine($"{name}({verdict.Span.StartLine},{verdict.Span.StartColumn}): {FormatOutcome(verdict.Outcome)}: {verdict.Reason}");

        if(result.Verdicts.Count == 0)
            _output.WriteLine("No artifact of the rule's type in this code.");

        _output.WriteLine($"{result.Count(TryOutcome.BreaksIt)} breaks it, {result.Count(TryOutcome.FollowsIt)} follows it, {result.Count(TryOutcome.NotCovered)} not covered.");
    }

    private static string FormatOutcome(TryOutcome outcome)
    {
        switch(outcome)
        {
            case TryOutcome.BreaksIt:
                return "breaks it";
            case TryOutcome.FollowsIt:
                return "follows it";
            default:
                return "not covered";
        }
    }
}