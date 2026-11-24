using CF.Cli.Console;
using CF.Core.Changes;
using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Reviews what a push, the staged content or a range changes, or the whole of a folder, and prints the result. The
/// first line says which version ran and from which folder.
/// </summary>
internal sealed class ChangesReviewCommand
{
    private readonly IProductInfoService _productInfo;
    private readonly IReviewService _reviewService;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly string _currentFolder;
    private readonly string _programFolder;
    private readonly string? _settingsPath;
    private readonly ReviewArguments _arguments;

    public ChangesReviewCommand(IProductInfoService productInfo, IReviewService reviewService, CommandEnvironment environment, string? settingsPath, ReviewArguments arguments)
    {
        _productInfo = productInfo;
        _reviewService = reviewService;
        _input = environment.Input;
        _output = environment.Output;
        _currentFolder = environment.CurrentFolder;
        _programFolder = environment.ProgramFolder;
        _settingsPath = settingsPath;
        _arguments = arguments;
    }

    public ExitCode Run()
    {
        _output.WriteLine($"{_productInfo.Name} {_productInfo.Version} ({_programFolder})");
        var scope = CreateScope();
        if(scope == null)
            return ExitCode.BadSettingsOrUsage;

        var result = _reviewService.ReviewChanges(_currentFolder, scope, _settingsPath);
        new PlainTextReporter(_output, countByRule: _arguments.Mode == ReviewMode.All).Write(result);

        return result.ExitCode;
    }

    private ReviewScope? CreateScope()
    {
        switch(_arguments.Mode)
        {
            case ReviewMode.Hook:
                return ReviewScope.ForPush(_arguments.RemoteName, _arguments.RemoteUrl, PushedRefParser.Parse(_input.ReadToEnd()));
            case ReviewMode.Push:
                return FindPushScope();
            case ReviewMode.Staged:
                return ReviewScope.ForStaged();
            case ReviewMode.All:
                return ReviewScope.ForWholeFolder(_arguments.Target.Length == 0 ? string.Empty : Path.GetFullPath(_arguments.Target, _currentFolder));
            default:
                return ReviewScope.ForRange(_arguments.BaseRevision, _arguments.HeadRevision);
        }
    }

    private ReviewScope? FindPushScope()
    {
        var answer = _reviewService.FindPushScope(_currentFolder);
        if(!answer.HasPush)
            _output.WriteLine("No push to review: " + answer.Reason);

        return answer.Scope;
    }
}
