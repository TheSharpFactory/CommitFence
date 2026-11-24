using CF.Core.Review;
using CF.Core.Setup;

namespace CF.Cli.Commands;

/// <summary>
/// Shows what a setup command (install or init) did, one line at a time, and ends with the exit code that says whether
/// it worked.
/// </summary>
internal sealed class SetupCommand
{
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public SetupCommand(TextWriter output, TextWriter error)
    {
        _output = output;
        _error = error;
    }

    public ExitCode Show(SetupReport report)
    {
        var writer = report.Failed ? _error : _output;
        foreach(var line in report.Lines)
            writer.WriteLine(line);

        return report.Failed ? ExitCode.BadSettingsOrUsage : ExitCode.Passed;
    }
}