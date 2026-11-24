using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Rules;

namespace CF.Core.Review;

/// <summary>
/// Reviews one change set, read from a private copy of exactly its tree: the syntax rules on the files of the change
/// set (the files a push changed, or every file under the folder of a whole review), each read in every way the build
/// compiles it, then the rules that need meaning and the structure rules, judged from what the language's own tools
/// answered.
/// </summary>
/// <remarks>
/// A change set whose tree cannot be reviewed, for whatever reason, is recorded as not reviewed and the review goes on
/// with the next one.
/// </remarks>
public sealed class ChangeSetReview
{
    private readonly IReadOnlyList<ILanguage> _languages;
    private readonly ISnapshotStore _snapshots;
    private readonly SettingsInTree _settings;
    private readonly FileReviewer _fileReviewer;
    private readonly ToolchainStage _toolchains;
    private readonly StructureReview _structureReview;

    /// <summary>
    /// Creates the review.
    /// </summary>
    public ChangeSetReview(IReadOnlyList<ILanguage> languages, ISnapshotStore snapshots, SettingsInTree settings, FileReviewer fileReviewer, ToolchainStage toolchains, StructureReview structureReview)
    {
        _languages = languages;
        _snapshots = snapshots;
        _settings = settings;
        _fileReviewer = fileReviewer;
        _toolchains = toolchains;
        _structureReview = structureReview;
    }

    internal void Review(RepositoryLocation repository, ChangeSet changeSet, string? settingsPath, ReviewRun run)
    {
        run.AddNote(changeSet.Note);
        if(changeSet.RuleFilesChanged.Count > 0)
            run.AddNote("Rule files changed: " + string.Join(", ", changeSet.RuleFilesChanged) + ".");

        if(changeSet.HasUncommittedWork)
            run.MarkUncommittedWork();

        if(changeSet.Tree.Length == 0)
            return;

        try
        {
            var tree = new TreeUnderReview(repository, changeSet, _snapshots.MoveTo(repository, changeSet.Tree), settingsPath);
            ReviewTree(tree, run);
        }
        catch(Exception exception) when(!ReviewBoundary.PassesThrough(exception))
        {
            run.AddCoverage(changeSet.Label, CoverageStatus.Failed, DescribeFailure(exception));
        }
    }

    private static string DescribeFailure(Exception exception)
    {
        if(ReviewBoundary.IsExpected(exception))
            return FailureText.Describe(exception);

        return "an error nobody expected stopped the review of this change: " + ReviewBoundary.MessageOf(exception);
    }

    /// <summary>
    /// The steps in order: the toolchains first, because they say how each file is compiled; then the files; then the
    /// rules that need meaning; then the structure.
    /// </summary>
    private void ReviewTree(TreeUnderReview tree, ReviewRun run)
    {
        var toolchainWork = _toolchains.FindWork(tree);
        NoteFilesNotLookedAt(tree, toolchainWork, run);
        if(_settings.RefusesOlderProgram(tree.SettingsPath(), run))
            return;

        var toReview = tree.ChangeSet.Files.Where(file => file.Language != null && file.Status != ChangeStatus.Deleted).ToList();
        if(toReview.Count == 0 && toolchainWork.Count == 0 && !tree.ChangesTheSettingsOfTheTree())
            return;

        // a settings file that is there is always read strictly, also when it is the only thing the push changes
        var settings = _settings.Load(tree.SettingsPath(), toReview.Count > 0);
        var outcomes = _toolchains.Run(tree, toolchainWork);
        var sources = ReviewFiles(tree, toReview, settings, ToolchainStage.ReadingsOf(outcomes), run);
        var answers = ToolchainStage.Record(outcomes, tree.ChangeSet, run);
        if(settings == null)
            return;

        // a change with no file of code gives the rules that judge code nothing to judge: that is a complete review, not a rule that was not run
        if(sources.Count > 0)
            MeaningRulePass.Run(settings.ActiveRules, sources, answers, _languages, run);

        _structureReview.Run(tree, settings, answers, run);
    }

    private static void NoteFilesNotLookedAt(TreeUnderReview tree, IReadOnlyList<ToolchainWork> toolchainWork, ReviewRun run)
    {
        var count = ToolchainStage.CountNotLookedAt(tree, toolchainWork);
        if(count > 0)
            run.AddNote($"{count} {tree.ChangeSet.NameOfFiles(count)} of no known language {(count == 1 ? "was" : "were")} not looked at.");
    }

    private List<ReadSource> ReviewFiles(TreeUnderReview tree, IReadOnlyList<ChangedFile> toReview, RuleSettings? settings, IReadOnlyList<FileReading> readings, ReviewRun run)
    {
        var sources = new List<ReadSource>();
        if(settings == null)
            return sources;

        foreach(var file in toReview)
        {
            var readingsOfFile = readings.Where(reading => string.Equals(reading.Path, file.Path, StringComparison.Ordinal)).ToList();
            var source = _fileReviewer.Review(new FileToReview(file.Path, Path.Combine(tree.Folder, file.Path)), settings.ActiveRules, run, file.ChangedLines, readingsOfFile);
            if(source != null)
                sources.Add(source);
        }

        return sources;
    }
}