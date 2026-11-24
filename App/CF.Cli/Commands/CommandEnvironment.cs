namespace CF.Cli.Commands;

/// <summary>
/// Where and with what the command line runs: the folders, the program's own path, the streams, and whether an
/// unexpected error shows its stack trace.
/// </summary>
/// <param name="CurrentFolder">The folder the program was started in.</param>
/// <param name="ProgramFolder">The folder the program lives in.</param>
/// <param name="ProgramPath">The path of the program file.</param>
/// <param name="Input">Standard input.</param>
/// <param name="Output">Standard output.</param>
/// <param name="Error">Standard error.</param>
/// <param name="ShowTrace">Whether an error nobody expected also prints its stack trace (the variable COMMITFENCE_TRACE
/// set to 1).</param>
/// <param name="IsTerminal">Whether both standard output and standard error go to a terminal, where a person reads them.</param>
/// <param name="InContinuousIntegration">Whether the variable CI is set, which is how a pipeline says it runs.</param>
internal sealed record CommandEnvironment(string CurrentFolder, string ProgramFolder, string ProgramPath, TextReader Input, TextWriter Output, TextWriter Error, bool ShowTrace, bool IsTerminal = false, bool InContinuousIntegration = false);