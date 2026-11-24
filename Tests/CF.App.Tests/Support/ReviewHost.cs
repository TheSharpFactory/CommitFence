using CF.App.Review;
using CF.Core.Changes;

namespace CF.App.Tests.Support;

/// <summary>
/// One Review screen over a repository folder in a temporary place, a review the test controls, and the real rule service reading
/// the settings file of that folder (the finding's rule is named from it).
/// </summary>
internal sealed class ReviewHost : IDisposable
{
    private readonly TestFolder _folder = new TestFolder();

    public ReviewHost(bool insideRepository = true, string settings = SettingsText.Four)
    {
        RepositoryPath = Path.Combine(_folder.Path, "invoicing");
        Directory.CreateDirectory(RepositoryPath);
        File.WriteAllText(Path.Combine(RepositoryPath, "commitfence.json"), settings);
        Rules = new RuleServiceDouble(Path.Combine(_folder.Path, "data"));
        Model = new ReviewViewModel(Review, Rules, Clock, Samples.Folder(RepositoryPath, insideRepository), ShownRules.Add);
    }

    public string RepositoryPath { get; }

    public FakeReviewService Review { get; } = new FakeReviewService { PushScope = _ => PushScopeAnswer.Found(ReviewScope.ForPush("origin", "https://example.com/r.git", PushedRefs.None), "main", "origin") };

    public RuleServiceDouble Rules { get; }

    public FixedClock Clock { get; } = new FixedClock(Samples.Now);

    public List<string> ShownRules { get; } = new List<string>();

    public ReviewViewModel Model { get; }

    /// <summary>
    /// Presses the run button and waits for the review to be shown.
    /// </summary>
    public async Task RunAsync()
    {
        await Model.RunCommand.ExecuteAsync(null);
    }

    public void Dispose()
    {
        _folder.Dispose();
    }
}
