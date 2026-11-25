using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Ports;

namespace CF.Core.Review;

/// <summary>
/// Reviews what a push, the staged content or a range changes in a repository: finds the repository, takes the program's
/// lock and the review lock, resolves the scope into change sets and reviews each.
/// </summary>
/// <remarks>
/// When the scope cannot be established the result is incomplete, never a pass.
/// </remarks>
public sealed class TreeReview
{
    private readonly IClock _clock;
    private readonly IGit _git;
    private readonly IReviewLock _reviewLock;
    private readonly IProgramLock _programLock;
    private readonly ChangeResolver _changeResolver;
    private readonly ChangeSetReview _changeSetReview;
    private readonly IStructureJudgments? _structureJudgments;
    private readonly IReviewHistory? _history;

    /// <summary>
    /// Creates the review.
    /// </summary>
    public TreeReview(IClock clock, IGit git, IReviewLock reviewLock, IProgramLock programLock, ChangeResolver changeResolver, ChangeSetReview changeSetReview, IStructureJudgments? structureJudgments = null, IReviewHistory? history = null)
    {
        _clock = clock;
        _git = git;
        _reviewLock = reviewLock;
        _programLock = programLock;
        _changeResolver = changeResolver;
        _changeSetReview = changeSetReview;
        _structureJudgments = structureJudgments;
        _history = history;
    }

    /// <summary>
    /// Reviews the change in the repository the folder belongs to. The settings file is read from the reviewed tree
    /// unless a path is given.
    /// </summary>
    /// <exception cref="Rules.Settings.SettingsException">The settings file is missing or wrong.</exception>
    public ReviewResult Review(string folder, ReviewScope scope, string? settingsPath)
    {
        var started = _clock.Now;
        try
        {
            var repository = _git.FindRepository(folder);
            if(repository == null)
                return CreateIncompleteResult("the current folder is not inside a git repository", started);

            // an update of the program waits for this review: the lock is held for as long as the review runs
            using(_programLock.HoldForReview())
            {
                using(_reviewLock.Acquire(repository))
                {
                    return FinishLocked(repository, scope, settingsPath, started);
                }
            }
        }
        catch(Exception exception) when(exception is ScopeException || exception is GitException || exception is TimeoutException)
        {
            return CreateIncompleteResult(FailureText.Describe(exception), started);
        }
    }

    private ReviewResult FinishLocked(RepositoryLocation repository, ReviewScope scope, string? settingsPath, DateTimeOffset started)
    {
        var run = new ReviewRun();
        ReviewResult result;
        DateTimeOffset ended;
        try
        {
            result = ReviewLocked(repository, scope, settingsPath, started, run, out ended);
        }
        catch(Exception exception) when(exception is ScopeException or GitException or TimeoutException)
        {
            ended = _clock.Now;
            result = CreateIncompleteResult(FailureText.Describe(exception), started, ended);
        }

        if(scope.Kind == ScopeKind.Push && _history != null)
            Record(repository, scope.Source, result, ended, run.RanBuild);

        return result;
    }

    private void Record(RepositoryLocation repository, ReviewSource source, ReviewResult result, DateTimeOffset ended, bool full)
    {
        try
        {
            var files = result.Coverage.Where(entry => entry.Kind == CoverageKind.File && entry.Status == CoverageStatus.Reviewed)
                .Select(entry => entry.What).Distinct(StringComparer.Ordinal).Count();
            var counts = result.CountByRule().Select(count => new RecordedRuleCount(count.RuleId, count.Count)).ToList();
            var outcome = result.Outcome switch
            {
                ReviewOutcome.Clean => RecordedReviewOutcome.Clean,
                ReviewOutcome.Warned => RecordedReviewOutcome.Warned,
                ReviewOutcome.Blocked => RecordedReviewOutcome.Blocked,
                ReviewOutcome.Incomplete => RecordedReviewOutcome.Incomplete,
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            };
            _history!.Append(repository, new ReviewRecord(ReviewRecord.CurrentFormat, ended.ToUniversalTime(), source, outcome,
                Math.Max(0, (long)result.Duration.TotalMilliseconds), full, files, counts));
        }
        catch(Exception)
        {
            // Storage failure must never change the finished result of a push review.
        }
    }

    private ReviewResult ReviewLocked(RepositoryLocation repository, ReviewScope scope, string? settingsPath, DateTimeOffset started, ReviewRun run, out DateTimeOffset ended)
    {
        string? wholeFolder = null;
        foreach(var changeSet in _changeResolver.Resolve(repository, scope))
        {
            _changeSetReview.Review(repository, changeSet, settingsPath, run);
            wholeFolder = changeSet.WholeFolder;
        }

        ended = _clock.Now;
        var folder = wholeFolder == string.Empty ? Path.GetFileName(repository.WorkFolder) : wholeFolder;
        var judgment = run.StructureJudgedAt(ended, folder);
        if(judgment != null)
            _structureJudgments?.Save(repository, judgment);

        return run.ToResult(ended - started, judgment);
    }

    private ReviewResult CreateIncompleteResult(string reason, DateTimeOffset started, DateTimeOffset? ended = null)
    {
        var coverage = new List<Coverage> { new Coverage("review scope", CoverageStatus.Failed, string.IsNullOrWhiteSpace(reason) ? "no reason was given" : reason) };
        return new ReviewResult(ReviewOutcome.Incomplete, new List<Finding>(), coverage, new List<string>(), "The review is incomplete.", (ended ?? _clock.Now) - started);
    }
}
