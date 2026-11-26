using CF.Core.Changes;
using CF.Core.Findings;
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

    public Func<string, StagedAnswer> StagedNow { get; set; } = _ => StagedAnswer.Of(["a.cs"], 0, false, "4f2a91c");

    public Func<ReviewScope, ReviewResult> Result { get; set; } = _ => Samples.Result(ReviewOutcome.Clean);

    private Action<ReviewResult>? _addRules;

    public void UseRules(IRuleService rules, string repository)
    {
        var path = Path.Combine(repository, "commitfence.json");
        _addRules = result =>
        {
            if(result.Findings.Count == 0)
                return;

            var team = rules.ListRules(path).ToDictionary(rule => rule.Id, StringComparer.Ordinal);
            var file = rules.ListFileRules(path).SelectMany(listing => listing.Rules).ToLookup(rule => rule.Id, StringComparer.Ordinal);
            result.ReviewedRules = result.Findings.ToDictionary(finding => finding, finding => finding.Source is FindingSource.CodeRule or FindingSource.StructureRule
                ? new ReviewedRule(team.GetValueOrDefault(finding.RuleId), null)
                : new ReviewedRule(null, file[finding.RuleId].FirstOrDefault()));
        };
    }

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

    public ReviewResult ReviewFiles(string settingsPath, IReadOnlyList<FileToReview> files, bool quick = false)
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

        var result = Result(scope);
        _addRules?.Invoke(result);
        return result;
    }

    public StagedAnswer FindStaged(string folder)
    {
        return StagedNow(folder);
    }

    public PushScopeAnswer FindPushScope(string folder, string branch)
    {
        return PushScopeAnswer.None("No push.", branch);
    }

    public PushScopeAnswer FindPushScope(string folder)
    {
        PushScopeAsked++;
        return PushScope(folder);
    }
}