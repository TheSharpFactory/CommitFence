namespace CF.Core.Ports;

/// <summary>
/// A program to run: what to start, with which arguments and environment, what to give it on standard input, and how
/// long it may run.
/// </summary>
public sealed class ProcessRequest
{
    /// <summary>
    /// Creates a request.
    /// </summary>
    public ProcessRequest(string fileName, IReadOnlyList<string> arguments, string? workingFolder, IReadOnlyDictionary<string, string?> environment, string? standardInput, TimeSpan timeLimit)
    {
        FileName = fileName;
        Arguments = arguments;
        WorkingFolder = workingFolder;
        Environment = environment;
        StandardInput = standardInput;
        TimeLimit = timeLimit;
    }

    /// <summary>
    /// The program, as a name the machine can find or a full path.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// The arguments, each one as it should arrive: no quoting is needed.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// The folder to start in, or nothing for the current one.
    /// </summary>
    public string? WorkingFolder { get; }

    /// <summary>
    /// Environment variables to set on top of the current ones; a value of nothing removes the variable.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Environment { get; }

    /// <summary>
    /// Text to give on standard input, or nothing for an empty input.
    /// </summary>
    public string? StandardInput { get; }

    /// <summary>
    /// How long the program may run, from its start until it has ended and written everything. After that the program
    /// and every process it started are stopped and the result says so.
    /// </summary>
    public TimeSpan TimeLimit { get; }
}