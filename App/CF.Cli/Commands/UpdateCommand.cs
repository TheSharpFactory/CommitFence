using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Updates;

namespace CF.Cli.Commands;

/// <summary>
/// Installs the newest release over the running program, or only says whether there is one.
/// </summary>
internal sealed class UpdateCommand
{
    /// <summary>
    /// The exit code of an update that could not be made, or a check that could not be answered (1).
    /// </summary>
    internal const ExitCode CouldNot = (ExitCode)1;

    private readonly IUpdater _updater;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly bool _checkOnly;

    public UpdateCommand(IUpdater updater, TextWriter output, TextWriter error, bool checkOnly)
    {
        _updater = updater;
        _output = output;
        _error = error;
        _checkOnly = checkOnly;
    }

    public ExitCode Run()
    {
        var result = _updater.CheckAsync(CancellationToken.None).GetAwaiter().GetResult();
        if(result.Status == UpdateCheckStatus.Failed || result.Release == null)
        {
            _error.WriteLine($"Could not check for updates. {result.Problem}");
            return CouldNot;
        }

        var current = _updater.CurrentVersion;
        if(result.Status == UpdateCheckStatus.UpToDate)
        {
            _output.WriteLine($"CommitFence {current} is already up to date.");
            return ExitCode.Passed;
        }

        if(_checkOnly)
        {
            _output.WriteLine($"CommitFence {result.Release.Version} is available (this is {current}): run commitfence update");
            return ExitCode.Passed;
        }

        return Install(result.Release, current);
    }

    private ExitCode Install(ReleaseInfo release, Version current)
    {
        var problem = _updater.FindProblem(release);
        if(problem != null)
        {
            var page = release.PageUri == null ? string.Empty : $" Download version {release.Version} from {release.PageUri}.";
            _error.WriteLine($"CommitFence could not be updated. {problem}{page}");
            return CouldNot;
        }

        _output.WriteLine($"Updating CommitFence from {current} to {release.Version}...");
        try
        {
            _updater.InstallAsync(release, null, null).GetAwaiter().GetResult();
        }
        catch(UpdateException exception)
        {
            _error.WriteLine($"CommitFence could not be updated. {exception.Message}");
            return CouldNot;
        }

        _output.WriteLine($"CommitFence is now version {release.Version}.");
        return ExitCode.Passed;
    }
}