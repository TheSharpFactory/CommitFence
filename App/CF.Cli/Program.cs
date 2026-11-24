using CF.Cli;
using CF.Cli.Commands;

var showTrace = Environment.GetEnvironmentVariable("COMMITFENCE_TRACE") == "1";
var currentFolder = Directory.GetCurrentDirectory();
var programFolder = AppContext.BaseDirectory;
var programPath = Environment.ProcessPath ?? string.Empty;
var isTerminal = !Console.IsOutputRedirected && !Console.IsErrorRedirected;
var inContinuousIntegration = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
var environment = new CommandEnvironment(currentFolder, programFolder, programPath, Console.In, Console.Out, Console.Error, showTrace, isTerminal, inContinuousIntegration);
var exitCode = CliApplication.Run(args, environment);

return (int)exitCode;
//the exit code is the contract with the hook runner