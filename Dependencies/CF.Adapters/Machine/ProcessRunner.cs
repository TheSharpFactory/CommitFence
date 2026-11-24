using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using CF.Core.Ports;

namespace CF.Adapters.Machine;

/// <summary>
/// Starts programs on the machine the product runs on. A program that runs past its time limit is stopped together with
/// every process it started.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <summary>
    /// The name of an environment variable that caps every time limit, in whole seconds. It exists so that a check can
    /// see a hung program stopped within seconds instead of minutes; a hook never needs it.
    /// </summary>
    public const string LimitCapVariable = "COMMITFENCE_PROCESS_LIMIT_SECONDS";

    private static readonly UTF8Encoding _utf8WithoutMark = new UTF8Encoding(false);
    private static readonly TimeSpan _graceAfterStop = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public ProcessResult Run(ProcessRequest request)
    {
        try
        {
            using(var process = Process.Start(CreateStartInfo(request)))
            {
                if(process == null)
                    return new ProcessResult(-1, string.Empty, $"{request.FileName} could not be started.");

                return Complete(process, request);
            }
        }
        catch(Exception exception) when(IsStartFailure(exception))
        {
            return new ProcessResult(-1, string.Empty, $"{request.FileName} could not be started: {exception.Message}");
        }
    }

    private static bool IsStartFailure(Exception exception)
    {
        return exception is Win32Exception || exception is InvalidOperationException || exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException;
    }

    private static ProcessStartInfo CreateStartInfo(ProcessRequest request)
    {
        var startInfo = new ProcessStartInfo(request.FileName)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = _utf8WithoutMark,
            StandardOutputEncoding = _utf8WithoutMark,
            StandardErrorEncoding = _utf8WithoutMark
        };

        foreach(var argument in request.Arguments)
            startInfo.ArgumentList.Add(argument);

        if(request.WorkingFolder != null)
            startInfo.WorkingDirectory = request.WorkingFolder;

        foreach(var variable in request.Environment)
            ApplyVariable(startInfo, variable.Key, variable.Value);

        return startInfo;
    }

    private static void ApplyVariable(ProcessStartInfo startInfo, string name, string? value)
    {
        if(value == null)
            startInfo.Environment.Remove(name);
        else
            startInfo.Environment[name] = value;
    }

    private static ProcessResult Complete(Process process, ProcessRequest request)
    {
        var limit = CapLimit(request.TimeLimit);
        var clock = Stopwatch.StartNew();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var input = Task.Run(() => WriteInput(process, request.StandardInput));
        var tasks = new Task[] { output, error, input };

        var ended = process.WaitForExit(limit);
        if(ended && Task.WaitAll(tasks, Remaining(limit, clock)))
            return new ProcessResult(process.ExitCode, output.Result, error.Result);

        return StopAndDescribe(process, request, limit, ended, output, error, tasks);
    }

    private static TimeSpan CapLimit(TimeSpan limit)
    {
        var text = Environment.GetEnvironmentVariable(LimitCapVariable);
        if(int.TryParse(text, out var seconds) && seconds > 0 && TimeSpan.FromSeconds(seconds) < limit)
            return TimeSpan.FromSeconds(seconds);

        return limit;
    }

    private static TimeSpan Remaining(TimeSpan limit, Stopwatch clock)
    {
        var remaining = limit - clock.Elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static ProcessResult StopAndDescribe(Process process, ProcessRequest request, TimeSpan limit, bool ended, Task<string> output, Task<string> error, Task[] tasks)
    {
        StopWithEverythingItStarted(process);
        Task.WaitAll(tasks, _graceAfterStop);

        var seconds = (int)Math.Ceiling(limit.TotalSeconds);
        var message = ended ? $"{request.FileName} ended, but something it started kept its output open for more than {seconds} seconds; its output is not complete." : $"{request.FileName} did not finish within {seconds} seconds and was stopped.";
        var written = output.IsCompletedSuccessfully ? output.Result : string.Empty;
        var errorText = error.IsCompletedSuccessfully ? error.Result : string.Empty;

        return new ProcessResult(-2, written, (message + " " + errorText).TrimEnd(), true);
    }

    private static void StopWithEverythingItStarted(Process process)
    {
        try
        {
            process.Kill(true);
            process.WaitForExit(_graceAfterStop);
        }
        catch(Exception exception) when(exception is InvalidOperationException || exception is Win32Exception || exception is NotSupportedException)
        {
            // the program ended by itself in the meantime, or the machine refuses to stop it; the caller is told that
            // it timed out either way
        }
    }

    private static void WriteInput(Process process, string? standardInput)
    {
        try
        {
            if(!string.IsNullOrEmpty(standardInput))
                process.StandardInput.Write(standardInput);

            process.StandardInput.Close();
        }
        catch(IOException)
        {
            // the program ended without reading its input; its exit code says what happened
        }
        catch(InvalidOperationException)
        {
            // the program was stopped while its input was being written
        }
    }
}