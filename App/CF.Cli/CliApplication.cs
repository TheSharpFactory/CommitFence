using CF.Cli.Commands;
using CF.Composition;
using CF.Core.Review;

namespace CF.Cli;

/// <summary>
/// Starts the command line: puts the parts together, runs the command, and ends any error nobody expected as one plain
/// line on standard error and an exit code, never as a crash.
/// </summary>
internal static class CliApplication
{
    /// <summary>
    /// Runs the command the arguments name. The exit code is 2 for a review that could not finish and 3 for any other
    /// command that could not.
    /// </summary>
    public static ExitCode Run(string[] args, CommandEnvironment environment)
    {
        try
        {
            var services = CompositionRoot.Create();

            var runner = new CommandRunner(services.ProductInfo, services.Rules, services.Review, services.Setup, services.Updater, environment, services.Doctor, services.Settings, services.Structure, services.Clock, services.Exemptions, services.Stats, services.Fixes, services.Folders);
            if(args.Length > 0 && args[0] == CommandRunner.DoctorCommandName)
                return runner.Run(args);

            // what an earlier update left beside the program; one try, a review must not wait for it
            services.Updater.CleanUpAsync(1).GetAwaiter().GetResult();
            var exitCode = runner.Run(args);

            // after the command's own work, so it never delays it
            new UpdateNotice(services.Updater, services.UpdateChecks, services.Clock, environment).ShowIfDue(args);
            return exitCode;
        }
        catch(Exception exception)
        {
            return Report(exception, args, environment);
        }
    }

    private static ExitCode Report(Exception exception, string[] args, CommandEnvironment environment)
    {
        var message = exception.Message.ReplaceLineEndings(" ").Trim();
        environment.Error.WriteLine($"CommitFence could not finish: {exception.GetType().Name}: {message}");
        if(environment.ShowTrace)
            environment.Error.WriteLine(exception.ToString());

        return IsReview(args) ? ExitCode.Incomplete : ExitCode.BadSettingsOrUsage;
    }

    private static bool IsReview(string[] args)
    {
        return args.Length > 0 && args[0] == CommandRunner.ReviewCommandName;
    }
}
