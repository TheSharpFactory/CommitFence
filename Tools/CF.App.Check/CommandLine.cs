using System.Diagnostics;

namespace CF.App.Check;

/// <summary>
/// The result of one run of the command-line program.
/// </summary>
internal sealed record CommandResult(int ExitCode, string Output, string Error);

/// <summary>
/// Runs the published command-line program in a folder, the way a person or a hook would.
/// </summary>
internal sealed class CommandLine
{
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(120);

    private readonly string _program;

    public CommandLine(string program)
    {
        _program = program;
    }

    public CommandResult Run(string folder, params string[] arguments)
    {
        return RunWithInput(folder, string.Empty, arguments);
    }

    /// <summary>
    /// Runs the program with this text on its standard input, as git feeds the lines of a push to a hook.
    /// </summary>
    public CommandResult RunWithInput(string folder, string input, params string[] arguments)
    {
        var start = new ProcessStartInfo(_program)
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach(var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("the command-line program did not start");
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(_limit))
        {
            process.Kill(true);
            throw new TimeoutException($"the command-line program ran longer than {_limit.TotalSeconds:0} seconds: {string.Join(' ', arguments)}");
        }

        return new CommandResult(process.ExitCode, output.Result, error.Result);
    }
}
