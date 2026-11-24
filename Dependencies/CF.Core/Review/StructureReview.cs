using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Structure;

namespace CF.Core.Review;

/// <summary>
/// Judges the structure rules of a change set:
/// the layer directions on the tree before the change and on the tree after it, and reports the violations that are
/// new.
/// </summary>
/// <remarks>
/// A change that starts from nothing (a whole review, or the first push of a repository) has no tree before, so
/// nothing is already known and no tree before is moved to or built; a whole review reports the violations whose file
/// lies under its target.
/// A violation that was already there is not reported. If the structure rules themselves changed in the push, both
/// trees are judged by the new rules and the summary says so. When the answers for either tree are missing or
/// incomplete, the rule is reported as not run and the review is incomplete.
/// </remarks>
public sealed class StructureReview
{
    private readonly IGit _git;
    private readonly ISnapshotStore _snapshots;
    private readonly IReviewLogs _reviewLogs;
    private readonly SettingsFile _settingsFile;

    /// <summary>
    /// Creates the review.
    /// </summary>
    public StructureReview(IGit git, ISnapshotStore snapshots, IReviewLogs reviewLogs, SettingsFile settingsFile)
    {
        _git = git;
        _snapshots = snapshots;
        _reviewLogs = reviewLogs;
        _settingsFile = settingsFile;
    }

    internal void Run(TreeUnderReview tree, RuleSettings settings, IReadOnlyList<LanguageAnswers> afterAnswers, ReviewRun run)
    {
        if(settings.Structure.IsEmpty)
            return;

        NoteChangedRules(tree, settings, run);
        foreach(var after in afterAnswers.Where(candidate => candidate.Language.Meaning != null))
            RunLanguage(tree.Repository, tree.ChangeSet, settings.Structure, after, run);
    }

    /// <summary>
    /// One flow for every change set. What differs is what is already known (the violations of the tree before; nothing
    /// when the change starts from nothing) and which violations are in scope (all of a change; in a whole review those
    /// of the files of the change set).
    /// </summary>
    private void RunLanguage(RepositoryLocation repository, ChangeSet changeSet, StructureRules rules, LanguageAnswers after, ReviewRun run)
    {
        if(!after.Answers.IsComplete)
        {
            run.AddNotRun(StructureRuleInfo.LayerDirectionId, $"the build did not answer completely for {changeSet.NameOfTreeAfter()}: " + after.Answers.Gaps[0]);
            return;
        }

        var judge = new StructureJudge(rules);
        var known = FindKnownViolations(repository, changeSet, judge, after, run);
        if(known == null)
            return;

        var made = judge.Judge(after.Answers).Where(violation => !known.Contains(violation.Identity) && IsInScope(changeSet, violation.Use.File));
        run.AddFindings(made.Select(StructureFindings.Create).ToList());
        foreach(var use in judge.FindUnplaced(after.Answers).Where(candidate => IsInScope(changeSet, candidate.File)))
            run.AddNotRun(StructureRuleInfo.LayerDirectionId, DescribeUnplaced(use));
    }

    /// <summary>
    /// The identities of the violations the tree before already had, or nothing when they cannot be told (the rule is
    /// then reported as not run). A change that starts from the empty tree has nothing already known, and no tree is
    /// built for it.
    /// </summary>
    private HashSet<string>? FindKnownViolations(RepositoryLocation repository, ChangeSet changeSet, StructureJudge judge, LanguageAnswers after, ReviewRun run)
    {
        if(StartsFromNothing(repository, changeSet))
            return new HashSet<string>(StringComparer.Ordinal);

        var before = CollectBefore(repository, changeSet, after, run);
        if(!before.IsComplete)
        {
            run.AddNotRun(StructureRuleInfo.LayerDirectionId, "the tree before the push could not be answered, so what the push made worse cannot be told: " + before.Gaps[0]);
            return null;
        }

        return judge.Judge(before).Select(violation => violation.Identity).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// A change reports every violation it made worse, wherever its file lies; a whole review reports those of the
    /// files it reviews.
    /// </summary>
    private static bool IsInScope(ChangeSet changeSet, string path)
    {
        return !changeSet.IsWholeReview || ReachFilter.Holds(changeSet, path);
    }

    private static string DescribeUnplaced(MeaningUse use)
    {
        var member = use.Member.Length == 0 ? "outside any member" : "in " + use.Member;
        return $"{use.File} ({member}) uses {use.UsedType} of {use.UsedAssembly}, a project this review did not compile, so the file of that type is unknown and the module it belongs to cannot be told while modules are named by folder";
    }

    private MeaningAnswers CollectBefore(RepositoryLocation repository, ChangeSet changeSet, LanguageAnswers after, ReviewRun run)
    {
        try
        {
            return CollectInBeforeTree(repository, changeSet, after, run);
        }
        catch(Exception exception) when(ReviewBoundary.IsExpected(exception))
        {
            return MeaningAnswers.Missing(after.Language.Id, exception.Message);
        }
    }

    private MeaningAnswers CollectInBeforeTree(RepositoryLocation repository, ChangeSet changeSet, LanguageAnswers after, ReviewRun run)
    {
        try
        {
            var folder = _snapshots.MoveTo(repository, changeSet.BeforeTree);
            var logs = _reviewLogs.CreateFolder(repository);

            return CollectFromLanguage(after.Language, new MeaningRequest(changeSet.BeforeTree, folder, logs, after.Answers.Parts));
        }
        finally
        {
            // the private copy holds the tree under review when a review ends, whatever became of the tree before
            MoveBackToTreeUnderReview(repository, changeSet, run);
        }
    }

    /// <summary>
    /// The failure boundary of a language's meaning source, drawn once for every language: a source that throws gives
    /// answers with a gap that says the tree could not be built.
    /// </summary>
    private static MeaningAnswers CollectFromLanguage(ILanguage language, MeaningRequest request)
    {
        try
        {
            return language.Meaning!.Collect(request);
        }
        catch(Exception exception) when(!ReviewBoundary.PassesThrough(exception))
        {
            return MeaningAnswers.Missing(language.Id, "the tree could not be built: " + ReviewBoundary.MessageOf(exception));
        }
    }

    private void MoveBackToTreeUnderReview(RepositoryLocation repository, ChangeSet changeSet, ReviewRun run)
    {
        try
        {
            _snapshots.MoveTo(repository, changeSet.Tree);
        }
        catch(Exception exception) when(ReviewBoundary.IsExpected(exception))
        {
            // a failure here must not replace the error that is already on its way, so it is told as a gap of the
            // review
            run.AddCoverage(new Coverage("the private copy", CoverageStatus.Failed, "it could not be moved back to the tree under review, so what it holds cannot be trusted: " + exception.Message, CoverageKind.Build));
        }
    }

    /// <summary>
    /// Whether the tree before is git's empty tree: a first push, or a whole review. There is then nothing to compare
    /// with.
    /// </summary>
    private bool StartsFromNothing(RepositoryLocation repository, ChangeSet changeSet)
    {
        return changeSet.BeforeTree == _git.EmptyTree(repository);
    }

    private void NoteChangedRules(TreeUnderReview tree, RuleSettings settings, ReviewRun run)
    {
        if(tree.GivenSettingsPath != null || !tree.ChangeSet.RuleFilesChanged.Contains(TreeUnderReview.DefaultSettingsFile, StringComparer.OrdinalIgnoreCase))
            return;

        if(StartsFromNothing(tree.Repository, tree.ChangeSet))
            return;

        if(!AreSameRules(tree.Repository, tree.ChangeSet, settings.Structure))
            run.AddNote("The structure rules changed in this push, so the tree before and the tree after were both judged by the new rules.");
    }

    private bool AreSameRules(RepositoryLocation repository, ChangeSet changeSet, StructureRules after)
    {
        var text = _git.ReadFile(repository, changeSet.BeforeTree, TreeUnderReview.DefaultSettingsFile);
        if(text == null)
            return false;

        try
        {
            return _settingsFile.Parse(text).Structure.Describe() == after.Describe();
        }
        catch(SettingsException)
        {
            return false;
        }
    }
}