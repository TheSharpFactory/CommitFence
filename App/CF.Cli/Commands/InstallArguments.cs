using CF.Core.Setup;

namespace CF.Cli.Commands;

/// <summary>
/// The arguments after the word install: --print to only say what would be done, and --no-path to leave the PATH alone.
/// </summary>
internal sealed class InstallArguments
{
    private InstallArguments(InstallOptions? options, string? error)
    {
        Options = options;
        Error = error;
    }

    public InstallOptions? Options { get; }

    public string? Error { get; }

    public static InstallArguments Parse(string[] args)
    {
        var dryRun = false;
        var changePath = true;
        foreach(var argument in args.Skip(1))
        {
            if(argument == "--print")
                dryRun = true;
            else if(argument == "--no-path")
                changePath = false;
            else
                return new InstallArguments(null, $"'{argument}' is not understood here.");
        }

        return new InstallArguments(new InstallOptions(dryRun, changePath), null);
    }
}