using CF.Core.Ports;
using CF.Core.Updates;

namespace CF.Cli.Commands;

/// <summary>
/// The one line on standard error that says a newer version exists. It is only for a person at a terminal: never in a
/// hook run, never when the output is not a terminal, never when CI is set. Those use no network at all.
/// </summary>
internal sealed class UpdateNotice
{
    private static readonly TimeSpan _checkTimeLimit = TimeSpan.FromSeconds(5);

    private readonly IUpdater _updater;
    private readonly IUpdateCheckRecord _record;
    private readonly IClock _clock;
    private readonly CommandEnvironment _environment;

    public UpdateNotice(IUpdater updater, IUpdateCheckRecord record, IClock clock, CommandEnvironment environment)
    {
        _updater = updater;
        _record = record;
        _clock = clock;
        _environment = environment;
    }

    /// <summary>
    /// Whether a command that was run may check for a newer version at all.
    /// </summary>
    public bool MayCheckAfter(string[] args)
    {
        if(!_environment.IsTerminal || _environment.InContinuousIntegration)
            return false;

        return !IsHookRun(args) && !IsUpdate(args);
    }

    /// <summary>
    /// After a command has done its own work: when a check is due (at most once a day), asks and prints the line if
    /// there is a newer version. A check that fails or is refused is silent and tried again the next day.
    /// </summary>
    public void ShowIfDue(string[] args)
    {
        if(!MayCheckAfter(args))
            return;

        var now = _clock.Now.UtcDateTime;
        if(!UpdateSchedule.IsCheckDue(_record.LastCheckUtc, now))
            return;

        // the day's check counts whatever it finds: offline, it is not asked again at every command
        _record.Save(now);
        var result = Check();
        if(result != null && result.Status == UpdateCheckStatus.Available && result.Release != null)
            _environment.Error.WriteLine($"CommitFence {result.Release.Version} is available: run commitfence update");
    }

    private UpdateCheckResult? Check()
    {
        using(var timeLimit = new CancellationTokenSource(_checkTimeLimit))
        {
            try
            {
                return _updater.CheckAsync(timeLimit.Token).GetAwaiter().GetResult();
            }
            catch(OperationCanceledException)
            {
                return null;
            }
        }
    }

    private static bool IsHookRun(string[] args)
    {
        return args.Length > 0 && args[0] == CommandRunner.ReviewCommandName && args.Contains("--hook", StringComparer.Ordinal);
    }

    private static bool IsUpdate(string[] args)
    {
        return args.Length > 0 && args[0] == CommandRunner.UpdateCommandName;
    }
}