using CF.Cli.Commands;
using CF.Core.Review;

namespace CF.Cli.Console;

/// <summary>
/// Puts a finished review where --format and --output say: the chosen report on the console in place of the plain text,
/// or, with --output, in that file while the console keeps the plain text. A report that cannot be written is said on the
/// error stream and never turns a passing review into a silent success.
/// </summary>
internal sealed class ReviewReportWriter
{
    private readonly ReviewArguments _arguments;
    private readonly CommandEnvironment _environment;
    private readonly string _version;
    private readonly string _repositoryFolder;
    private readonly bool _countByRule;

    public ReviewReportWriter(ReviewArguments arguments, CommandEnvironment environment, string version, string repositoryFolder, bool countByRule)
    {
        _arguments = arguments;
        _environment = environment;
        _version = version;
        _repositoryFolder = repositoryFolder;
        _countByRule = countByRule;
    }

    /// <summary>Writes the report and gives the exit code of the command: the review's, or, when a passing review could not write its report, that of a command that could not do what was asked.</summary>
    public ExitCode Write(ReviewResult result)
    {
        if(_arguments.OutputPath == null)
        {
            WriteReport(result, _environment.Output);
            return result.ExitCode;
        }

        new PlainTextReporter(_environment.Output, _countByRule).Write(result);
        var written = WriteFile(result, _arguments.OutputPath);

        return written || result.ExitCode != ExitCode.Passed ? result.ExitCode : ExitCode.BadSettingsOrUsage;
    }

    private void WriteReport(ReviewResult result, TextWriter writer)
    {
        if(_arguments.AsSarif)
            new SarifReporter(writer, _version, SarifPaths.Of(result, _repositoryFolder, _environment), NamesRevision()).Write(result);
        else if(_arguments.AsPrompt)
            new PromptReporter(writer).Write(result);
        else
            new PlainTextReporter(writer, _countByRule).Write(result);
    }

    // the head commit of a push or a range is the revision that an upload of the report must be associated with
    private bool NamesRevision()
    {
        return _arguments.Mode is ReviewMode.Hook or ReviewMode.Push or ReviewMode.Range;
    }

    private bool WriteFile(ReviewResult result, string outputPath)
    {
        var report = new StringWriter();
        WriteReport(result, report);
        try
        {
            var fullPath = Path.GetFullPath(outputPath, _environment.CurrentFolder);
            var folder = Path.GetDirectoryName(fullPath);
            if(!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllText(fullPath, report.ToString());
            return true;
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _environment.Error.WriteLine($"The report could not be written to {outputPath}: {exception.Message}");
            return false;
        }
    }
}
