using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Rules;

namespace CF.Core.Review;

/// <summary>
/// Reviews one source file: finds the language that owns it, reads it into artifacts and applies the syntax rules.
/// </summary>
/// <remarks>
/// A file compiled in several ways by the build is read once for each way. Whatever cannot be done is recorded in the
/// run as a file that was not reviewed.
/// </remarks>
public sealed class FileReviewer
{
    private readonly IFileSystem _fileSystem;
    private readonly IReadOnlyList<ILanguage> _languages;

    /// <summary>
    /// Creates the reviewer.
    /// </summary>
    public FileReviewer(IFileSystem fileSystem, IReadOnlyList<ILanguage> languages)
    {
        _fileSystem = fileSystem;
        _languages = languages;
    }

    /// <summary>
    /// Reviews the file.
    /// </summary>
    /// <remarks>
    /// The readings are the ways a build compiles it; with none, the file is read as it is written. Returns the
    /// changed source for the rules that need meaning when the review has a change and the file was read.
    /// </remarks>
    internal ReadSource? Review(FileToReview file, IReadOnlyList<RuleDefinition> rules, ReviewRun run, ChangedFile? change, IReadOnlyList<FileReading> readings)
    {
        var path = file.ReportedPath.Replace('\\', '/');
        var language = _languages.FirstOrDefault(candidate => candidate.OwnsFile(path));
        if(language == null)
        {
            run.AddCoverage(path, CoverageStatus.Skipped, "no language of the product owns this kind of file");
            return null;
        }

        var content = TryReadText(file.ReadPath, path, run);
        if(content == null)
            return null;

        var codeFile = new CodeFile(path, language.Id, content);
        run.RememberSource(language, codeFile, change?.ChangedLines);
        var ways = readings.Count > 0 ? ReadingRules.Read(language, codeFile, readings, run) : ReadAsWritten(language, codeFile, run);
        if(ways == null)
            return null;

        var findings = ReadingRules.Evaluate(rules, language, ways);
        run.AddFindings(change == null ? findings : ReachFilter.Keep(findings, change));
        AddCoverage(language, codeFile, readings, run);

        return change == null ? null : new ReadSource(language, codeFile, ways, change);
    }

    /// <summary>
    /// A file read with no symbol defined, because no build said how it is compiled, is read in part when it holds
    /// conditional sections: the review is then incomplete, never a pass.
    /// </summary>
    private static void AddCoverage(ILanguage language, CodeFile codeFile, IReadOnlyList<FileReading> readings, ReviewRun run)
    {
        if(readings.Count == 0 && language.Reader.HasConditionalSections(codeFile))
            run.AddCoverage(codeFile.Path, CoverageStatus.Failed, "read in part: code switched by a symbol was not read; a review with a build reads it");
        else
            run.AddCoverage(codeFile.Path, CoverageStatus.Reviewed, string.Empty);
    }

    /// <summary>
    /// The file read with no symbol defined, for a review that has no build to say how the file is compiled.
    /// </summary>
    private static IReadOnlyList<ReadWay>? ReadAsWritten(ILanguage language, CodeFile codeFile, ReviewRun run)
    {
        var artifacts = TryReadArtifacts(language, codeFile, run);
        if(artifacts == null)
            return null;

        return new List<ReadWay> { new ReadWay(new List<FileReading>(), artifacts) };
    }

    private string? TryReadText(string readPath, string path, ReviewRun run)
    {
        try
        {
            return _fileSystem.ReadAllText(readPath);
        }
        catch(IOException exception)
        {
            run.AddCoverage(path, CoverageStatus.Failed, "cannot be read: " + exception.Message);
        }
        catch(UnauthorizedAccessException exception)
        {
            run.AddCoverage(path, CoverageStatus.Failed, "cannot be read: " + exception.Message);
        }

        return null;
    }

    private static IReadOnlyList<Artifact>? TryReadArtifacts(ILanguage language, CodeFile file, ReviewRun run)
    {
        try
        {
            return language.Reader.Read(file);
        }
        catch(CodeReadException exception)
        {
            var place = exception.Line == null ? string.Empty : $" at line {exception.Line}, column {exception.Column}";
            run.AddCoverage(file.Path, CoverageStatus.Failed, $"does not parse{place}: {exception.Message}");
            return null;
        }
    }
}