using CF.Core.Changes;
using CF.Core.Review;
using CF.Core.Services;

namespace CF.App.Tests.Support;

/// <summary>
/// A review that answers from what the test sets and records every scope it was asked to review. A test can hold it at a gate:
/// the review then waits on the thread that runs it until the test lets it go.
/// </summary>
internal sealed class FakeReviewService : IReviewService
{
    private readonly Lock _syncRoot = new Lock();
    private readonly List<ReviewScope> _scopes = new List<ReviewScope>();

    public Func<string, PushScopeAnswer> PushScope { get; set; } = _ => PushScopeAnswer.None("No push.", string.Empty);

    public Func<ReviewScope, ReviewResult> Result { get; set; } = _ => Samples.Result(ReviewOutcome.Clean);

    public Exception? Failure { get; set; }

    /// <summary>
    /// When set, a review waits for it before it answers.
    /// </summary>
    public ManualResetEventSlim? Gate { get; set; }

    /// <summary>
    /// Set when a review has begun.
    /// </summary>
    public ManualResetEventSlim Entered { get; } = new ManualResetEventSlim();

    public IReadOnlyList<ReviewScope> Scopes
    {
        get
        {
            lock(_syncRoot)
            {
                return _scopes.ToList();
            }
        }
    }

    public int PushScopeAsked { get; private set; }

    public ReviewResult ReviewFiles(string settingsPath, IReadOnlyList<FileToReview> files)
    {
        throw new NotSupportedException();
    }

    public ReviewResult ReviewChanges(string folder, ReviewScope scope, string? settingsPath)
    {
        lock(_syncRoot)
        {
            _scopes.Add(scope);
        }

        Entered.Set();
        Gate?.Wait();
        if(Failure != null)
            throw Failure;

        return Result(scope);
    }

    public PushScopeAnswer FindPushScope(string folder)
    {
        PushScopeAsked++;
        return PushScope(folder);
    }
}