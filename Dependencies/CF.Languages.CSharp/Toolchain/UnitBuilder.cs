using System.Text.Json;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Languages.CSharp.Meaning;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Compiles one unit on its own, with the switches the proof found to work.
/// </summary>
/// <remarks>
/// The project's own references are not rebuilt (the walk has built them), no compiler server is left running, code
/// style analysis is on, analyzers are on whatever the project says, our analyzer is added from outside and the
/// compiler writes its log to the path this review chose. A unit under review is made to compile again by removing its
/// compiler output; without that the build may skip the compile and write no log. A log counts only when it did not
/// exist before this build and, for a unit under review, only when the closing entry of our analyzer in it matches the
/// facts it holds.
/// </remarks>
internal sealed class UnitBuilder
{
    private readonly IFileSystem _fileSystem;
    private readonly DotnetTool _dotnet;
    private readonly SarifReader _sarifReader;
    private readonly SarifFindingBuilder _findingBuilder;
    private readonly RuleErrorScanner _ruleErrorScanner;
    private readonly string _treeFolder;
    private readonly string _logFolder;
    private readonly string _targetsPath;
    private readonly string _meaningPath;
    private readonly bool _isWholeReview;
    private int _built;

    public UnitBuilder(IFileSystem fileSystem, DotnetTool dotnet, string treeFolder, string logFolder, string targetsPath, string meaningPath, bool isWholeReview)
    {
        _isWholeReview = isWholeReview;
        _fileSystem = fileSystem;
        _dotnet = dotnet;
        _sarifReader = new SarifReader(fileSystem);
        _findingBuilder = new SarifFindingBuilder(treeFolder);
        _ruleErrorScanner = new RuleErrorScanner(_sarifReader);
        _treeFolder = treeFolder;
        _logFolder = logFolder;
        _targetsPath = targetsPath;
        _meaningPath = meaningPath;
    }

    /// <summary>
    /// Builds the unit.
    /// </summary>
    /// <remarks>
    /// A breach of a rule never fails a build in a review: when the first build failed only because of rules set to
    /// error, the unit is built once more with exactly those rules let through, and that second build, with its own
    /// fresh log, is the evidence. The rule findings of the first log are added to it.
    /// </remarks>
    public UnitOutcome Build(ProjectUnit unit, bool underReview)
    {
        var first = BuildOnce(unit, underReview, new List<string>());
        if(first.Refusal != null)
            return first.Refusal;

        var ruleIds = first.Failed ? _ruleErrorScanner.FindRuleErrorIds(first.LogPath) : new List<string>();
        if(ruleIds.Count == 0)
            return Judge(unit, underReview, first);

        // the first log is read before the second build starts: what it found is kept in memory, not read again later
        var firstRuleFindings = ReadPlacedFindingsOf(first.LogPath, unit, ruleIds);
        var second = BuildOnce(unit, underReview, ruleIds);
        if(second.Refusal != null)
            return second.Refusal;

        return Judge(unit, underReview, second).WithMoreFindings(firstRuleFindings);
    }

    private BuildAttempt BuildOnce(ProjectUnit unit, bool underReview, IReadOnlyList<string> letThrough)
    {
        _built++;
        var logPath = ToolchainPaths.Combine(_logFolder, $"{_built:000}-{Path.GetFileNameWithoutExtension(unit.Project.Path)}.{unit.Framework}.sarif");
        if(_fileSystem.FileExists(logPath))
            return BuildAttempt.Refused(UnitOutcome.NoEvidence("a log already existed at the path chosen for it, so it cannot count as evidence from this review"));

        if(underReview && unit.IntermediateAssembly.Length > 0)
        {
            if(!ToolchainPaths.IsUnder(ToolchainPaths.Normalize(_treeFolder), unit.IntermediateAssembly))
                return BuildAttempt.Refused(UnitOutcome.NoEvidence("its project settings put the compiler output outside the private copy (" + unit.IntermediateAssembly + "), and nothing outside the copy is ever removed"));

            _fileSystem.DeleteFile(unit.IntermediateAssembly);
        }

        var result = _dotnet.Run(_treeFolder, CreateArguments(unit, logPath, letThrough), DotnetTimeLimits.Build);

        return BuildAttempt.Ran(result, logPath);
    }

    private UnitOutcome Judge(ProjectUnit unit, bool underReview, BuildAttempt attempt)
    {
        var result = attempt.Result!;
        return underReview ? JudgeReviewed(unit, result, attempt.LogPath) : JudgeSupport(result);
    }

    /// <summary>
    /// The findings of the rules that were let through, as the first log has them.
    /// </summary>
    /// <remarks>
    /// A finding with no place in a file cannot be tied to what the change touched, so it is an old breach like any
    /// other and is left out. In a whole review nothing is old, so it is kept.
    /// </remarks>
    private IReadOnlyList<Finding> ReadPlacedFindingsOf(string logPath, ProjectUnit unit, IReadOnlyList<string> ruleIds)
    {
        var run = _sarifReader.Read(logPath);
        return _findingBuilder.Build(run, unit).Where(finding => ruleIds.Contains(finding.RuleId) && (_isWholeReview || finding.Reach != null)).ToList();
    }

    private List<string> CreateArguments(ProjectUnit unit, string logPath, IReadOnlyList<string> letThrough)
    {
        var arguments = new List<string> { "msbuild", unit.Project.Path };
        arguments.AddRange(BuildSettings.FixedArguments);
        arguments.Add(BuildProperty.Argument("TargetFramework", unit.Framework));
        arguments.Add(BuildProperty.Argument("CustomAfterDirectoryBuildTargets", _targetsPath));
        arguments.Add(BuildProperty.Argument("CommitFenceLog", logPath));
        arguments.Add(BuildProperty.Argument("CommitFenceMeaning", _meaningPath));
        if(unit.RepositoryAfterTargets.Length > 0)
            arguments.Add(BuildProperty.Argument("CommitFenceRepositoryTargets", unit.RepositoryAfterTargets));

        if(letThrough.Count > 0)
            arguments.Add(BuildProperty.ListArgument("CommitFenceLetThrough", letThrough));

        return arguments;
    }

    private static UnitOutcome JudgeSupport(ProcessResult result)
    {
        if(result.TimedOut)
            return UnitOutcome.NoEvidence("it is not under review but is needed first, and its build did not finish: " + DotnetTool.SummarizeErrors(result));

        return result.ExitCode == 0 ? UnitOutcome.Fine(new List<Finding>(), null) : UnitOutcome.NoEvidence("it is not under review but is needed first, and it does not build: " + DotnetTool.SummarizeErrors(result));
    }

    private UnitOutcome JudgeReviewed(ProjectUnit unit, ProcessResult result, string logPath)
    {
        if(result.TimedOut)
            return UnitOutcome.NoEvidence("the build did not finish, so this project was not reviewed: " + DotnetTool.SummarizeErrors(result));

        if(!_fileSystem.FileExists(logPath))
            return UnitOutcome.NoEvidence(result.ExitCode == 0 ? "the build skipped the compile, so there is no log from this review" : "the build failed before the compiler ran: " + DotnetTool.SummarizeErrors(result));

        try
        {
            var run = _sarifReader.Read(logPath);
            return Conclude(unit, result, _findingBuilder.Build(run, unit), run);
        }
        catch(Exception exception) when(exception is JsonException || exception is IOException)
        {
            return UnitOutcome.NoEvidence("its log cannot be read: " + exception.Message);
        }
    }

    private UnitOutcome Conclude(ProjectUnit unit, ProcessResult result, IReadOnlyList<Finding> findings, SarifRunDto run)
    {
        if(result.ExitCode == 0)
            return ReadFacts(unit, findings, run);

        var compileErrors = findings.Count(finding => finding.Source == FindingSource.Compiler && finding.Severity == Severity.Block && finding.Reach == null);
        var ruleErrors = findings.Count(finding => finding.Severity == Severity.Block) - compileErrors;
        if(compileErrors == 0 && ruleErrors == 0)
            return UnitOutcome.NoEvidence("the build failed without an error in its log: " + DotnetTool.SummarizeErrors(result));

        return UnitOutcome.Broken($"the build fails: {compileErrors} compile errors, {ruleErrors} errors of rules set to error", findings);
    }

    private UnitOutcome ReadFacts(ProjectUnit unit, IReadOnlyList<Finding> findings, SarifRunDto run)
    {
        var notes = SarifMeaningNotes.From(run);
        try
        {
            var facts = new MeaningFactReader(_treeFolder).Read(notes.Facts, notes.Closings, unit.PartKeyIn(_treeFolder), unit.CompileFiles.Count);
            return UnitOutcome.Fine(findings, facts);
        }
        catch(MeaningLogException exception)
        {
            return UnitOutcome.NoEvidence(exception.Message, findings);
        }
    }
}