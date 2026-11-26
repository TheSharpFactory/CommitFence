using CF.Core.Review;
using CF.Core.Services;
using CF.Core.Setup;

namespace CF.Cli.Commands;

/// <summary>Sets up a repository's settings and hook.</summary>
internal sealed class InitCommand(ISetupService setup, CommandEnvironment environment)
{
    public ExitCode Run(string[] args)
    {
        HookKind? hook = null;
        var print = false;
        for(var index = 1; index < args.Length; index++)
        {
            if(args[index] == "--print" && !print)
            {
                print = true;
                continue;
            }

            if(args[index] != "--hook" || hook != null || index + 1 == args.Length)
                return WrongUsage();

            hook = args[++index] switch
            {
                "pre-push" => HookKind.PrePush,
                "pre-commit" => HookKind.PreCommit,
                _ => null
            };

            if(hook == null)
                return WrongUsage();
        }

        return new SetupCommand(environment.Output, environment.Error)
            .Show(setup.Init(environment.CurrentFolder, hook ?? HookKind.PrePush, print));
    }

    private ExitCode WrongUsage()
    {
        environment.Error.WriteLine("Usage: commitfence init [--hook pre-push|pre-commit] [--print]");
        return ExitCode.BadSettingsOrUsage;
    }
}
