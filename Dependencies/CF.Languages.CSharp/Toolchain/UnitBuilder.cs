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
/// facts it holds. A unit under review that owns no changed file is left to the build: when the build skips the compile,
/// the log kept from its last compile stands in for the missing one, but only when it is keyed to exactly what that
/// compile read; otherwise the unit is compiled again.
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
    private readonly EvidenceKey _evidenceKey;
    private readonly KeptEvidence _keptEvidence;
    private readonly bool _isWholeReview;
    private int _built;

    public UnitBuilder(IFileSystem fileSystem, DotnetTool dotnet, string treeFolder, string logFolder, string targetsPath, string meaningPath, EvidenceKey evidenceKey, KeptEvidence keptEvidence, bool isWholeReview)
    {
        _evidenceKey = evidenceKey;
        _keptEvidence = keptEvidence;
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
    public UnitOutcome Build(ProjectUnit unit, bool underReview, bool ownsChange)
    {
        var mayUseKept = underReview && !ownsChange && _keptEvidence.Has(unit.PartKeyIn(_treeFolder));
        var first = BuildOnce(unit, underReview, new List<string>(), !mayUseKept);
        if(first.Refusal != null)
            return first.Refusal;

        if(mayUseKept && WasSkipped(first))
        {
            var kept = JudgeKept(unit, first);
            if(kept.Succeeded)
                return kept;

            first = BuildOnce(unit, underReview, new List<string>(), true);
            if(first.Refusal != null)
                return first.Refusal;
        }

        var ruleIds = first.Failed ? _ruleErrorScanner.FindRuleErrorIds(first.LogPath) : new List<string>();
        if(ruleIds.Count == 0)
            return KeepWhenSucceeded(unit, first, Judge(unit, underReview, first));

        // the first log is read before the second build starts: what it found is kept in memory, not read again later
        var firstRuleFindings = ReadPlacedFindingsOf(first.LogPath, unit, ruleIds);
        var second = BuildOnce(unit, underReview, ruleIds, true);
        if(second.Refusal != null)
            return second.Refusal;

        return Judge(unit, underReview, second).WithMoreFindings(firstRuleFindings);
    }

    /// <summary>
    /// Whether the build ended well without writing a log. That alone proves nothing: what the compile read is told by
    /// the manifest the build wrote, and the kept log is used only when its key is made from that.
    /// </summary>
    private bool WasSkipped(BuildAttempt attempt)
    {
        return attempt.Result != null && !attempt.Result.TimedOut && attempt.Result.ExitCode == 0 && !_fileSystem.FileExists(attempt.LogPath);
    }

    private string? KeyOf(ProjectUnit unit, BuildAttempt attempt)
    {
        return _evidenceKey.Create(unit, ManifestPathOf(attempt.LogPath));
    }

    private static string ManifestPathOf(string logPath)
    {
        return Path.ChangeExtension(logPath, ".manifest");
    }

    private UnitOutcome JudgeKept(ProjectUnit unit, BuildAttempt attempt)
    {
        var key = KeyOf(unit, attempt);
        var keptLogPath = key == null ? null : _keptEvidence.Find(unit.PartKeyIn(_treeFolder), key);
        if(keptLogPath == null)
            return UnitOutcome.NoEvidence("no log was kept for exactly what this compile reads");

        var outcome = JudgeReviewed(unit, new ProcessResult(0, string.Empty, string.Empty), keptLogPath);
        return outcome.Succeeded ? outcome.WithReason("taken from the result kept from its last compile, which read exactly what it reads now, so it was not compiled again") : outcome;
    }

    /// <summary>
    /// Keeps the log of a compile that succeeded and whose closing entry matched its facts. Keeping is only a saving: a
    /// file that cannot be written never fails a review.
    /// </summary>
    private UnitOutcome KeepWhenSucceeded(ProjectUnit unit, BuildAttempt attempt, UnitOutcome outcome)
    {
        if(!outcome.Succeeded || outcome.Facts == null)
            return outcome;

        try
        {
            var key = KeyOf(unit, attempt);
            if(key != null)
                _keptEvidence.Save(unit.PartKeyIn(_treeFolder), key, attempt.LogPath);
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            // the next review compiles again
        }

        return outcome;
    }

    private BuildAttempt BuildOnce(ProjectUnit unit, bool underReview, IReadOnlyList<string> letThrough, bool removeOutput)
    {
        _built++;
        var logPath = ToolchainPaths.Combine(_logFolder, $"{_built:000}-{Path.GetFileNameWithoutExtension(unit.Project.Path)}.{unit.Framework}.sarif");
        if(_fileSystem.FileExists(logPath))
            return BuildAttempt.Refused(UnitOutcome.NoEvidence("a log already existed at the path chosen for it, so it cannot count as evidence from this review"));

        if(_fileSystem.FileExists(ManifestPathOf(logPath)))
            return BuildAttempt.Refused(UnitOutcome.NoEvidence("a manifest already existed at the path chosen for it, so it cannot count as evidence from this review"));

        if(underReview && removeOutput && unit.IntermediateAssembly.Length > 0)
        {
            if(!ToolchainPaths.IsUnder(ToolchainPaths.Normalize(_treeFolder), unit.IntermediateAssembly))
                return BuildAttempt.Refused(UnitOutcome.NoEvidence("its project settings put the compiler output outside the private copy (" + unit.IntermediateAssembly + "), and nothing outside the copy is ever removed"));

            _fileSystem.DeleteFile(unit.IntermediateAssembly);
        }

        var result = _dotnet.Run(_treeFolder, CreateArguments(unit, logPath, letThrough), DotnetTimeLimits.Build);

        return ForgetKeptWhenCompiled(unit, logPath) ?? BuildAttempt.Ran(result, logPath);
    }

    /// <summary>
    /// A compile that ran, under review or not, overtakes what was kept for the unit: the kept log is removed before
    /// anything else, so that a kept log always belongs to the newest compile. One that cannot be removed could be
    /// mistaken for this compile by a later review, so the unit then has no evidence.
    /// </summary>
    private BuildAttempt? ForgetKeptWhenCompiled(ProjectUnit unit, string logPath)
    {
        if(!_fileSystem.FileExists(logPath))
            return null;

        try
        {
            _keptEvidence.Remove(unit.PartKeyIn(_treeFolder));
            return null;
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            return BuildAttempt.Refused(UnitOutcome.NoEvidence("the result kept from its earlier compile could not be removed, so a later review could take it for this one: " + exception.Message));
        }
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
        arguments.Add(BuildProperty.Argument("CommitFenceManifest", ManifestPathOf(logPath)));
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
            var findings = _findingBuilder.Build(run, unit);
            var unplaced = findings.FirstOrDefault(finding => finding.File != null && Path.IsPathRooted(finding.File));
            if(unplaced != null)
                return UnitOutcome.NoEvidence($"the compiler reported {unplaced.RuleId} at {unplaced.File}, which is not inside the private copy ({_treeFolder}), so the finding cannot be placed in the repository and the log cannot be relied on");

            return Conclude(unit, result, findings, run);
        }
        catch(Exception exception) when(exception is JsonException || exception is IOException || exception is UnauthorizedAccessException)
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