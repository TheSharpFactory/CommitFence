using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// One run of the build of a unit: what the process said and where its log was to be written, or the reason the build
/// was not even started.
/// </summary>
internal sealed class BuildAttempt
{
    private BuildAttempt(ProcessResult? result, string logPath, UnitOutcome? refusal)
    {
        Result = result;
        LogPath = logPath;
        Refusal = refusal;
    }

    public ProcessResult? Result { get; }

    public string LogPath { get; }

    /// <summary>
    /// Why the build was not started, or nothing when it was.
    /// </summary>
    public UnitOutcome? Refusal { get; }

    /// <summary>
    /// Whether the build ended by itself with an error: a build that was stopped at the time limit is not one that
    /// failed.
    /// </summary>
    public bool Failed
    {
        get { return Result != null && !Result.TimedOut && Result.ExitCode != 0; }
    }

    public static BuildAttempt Ran(ProcessResult result, string logPath)
    {
        return new BuildAttempt(result, logPath, null);
    }

    public static BuildAttempt Refused(UnitOutcome refusal)
    {
        return new BuildAttempt(null, string.Empty, refusal);
    }
}