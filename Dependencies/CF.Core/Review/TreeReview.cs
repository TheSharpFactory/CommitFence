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

    /// <summary>
    /// Creates the review.
    /// </summary>
    public TreeReview(IClock clock, IGit git, IReviewLock reviewLock, IProgramLock programLock, ChangeResolver changeResolver, ChangeSetReview changeSetReview)
    {
        _clock = clock;
        _git = git;
        _reviewLock = reviewLock;
        _programLock = programLock;
        _changeResolver = changeResolver;
        _changeSetReview = changeSetReview;
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
                    return ReviewLocked(repository, scope, settingsPath, started);
                }
            }
        }
        catch(Exception exception) when(exception is ScopeException || exception is GitException || exception is TimeoutException)
        {
            return CreateIncompleteResult(FailureText.Describe(exception), started);
        }
    }

    private ReviewResult ReviewLocked(RepositoryLocation repository, ReviewScope scope, string? settingsPath, DateTimeOffset started)
    {
        var run = new ReviewRun();
        foreach(var changeSet in _changeResolver.Resolve(repository, scope))
            _changeSetReview.Review(repository, changeSet, settingsPath, run);

        return run.ToResult(_clock.Now - started);
    }

    private ReviewResult CreateIncompleteResult(string reason, DateTimeOffset started)
    {
        var coverage = new List<Coverage> { new Coverage("review scope", CoverageStatus.Failed, string.IsNullOrWhiteSpace(reason) ? "no reason was given" : reason) };
        return new ReviewResult(ReviewOutcome.Incomplete, new List<Finding>(), coverage, new List<string>(), "The review is incomplete.", _clock.Now - started);
    }
}