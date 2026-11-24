using CF.Adapters.Git;
using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// What a review does when git cannot answer: it says which git operation failed and the review is incomplete, never a
/// pass.
/// </summary>
public sealed class GitFailureTests
{
    [Theory]
    [InlineData("MissingObjectInTheDiff")]
    [InlineData("CheckoutThatTimesOut")]
    public void GitReadFailureLeavesReviewIncomplete(string scenario)
    {
        using(var harness = new ReviewHarness())
        {
            var runner = CreateGitScript(harness, scenario);
            var dataFolder = new FolderOfTest(Path.Combine(harness.Scratch, "data"));
            harness.Git = new GitCommandLine(runner, dataFolder);
            harness.Snapshots = new GitSnapshotStore(runner, dataFolder);

            var result = harness.Service().ReviewChanges(harness.RepositoryFolder, ReviewScope.ForRange("base", "head"), null);

            Assert.Equal(ExitCode.Incomplete, result.ExitCode);
            var failed = Assert.Single(result.Coverage, entry => entry.Status == CoverageStatus.Failed);
            if(scenario == "MissingObjectInTheDiff")
            {
                Assert.Contains("git diff failed", failed.Reason, StringComparison.Ordinal);
                var request = Assert.Single(runner.Requests, candidate => candidate.Arguments.Contains("diff"));
                Assert.Equal("1", request.Environment["GIT_NO_LAZY_FETCH"]);
                return;
            }

            Assert.Contains("git read-tree failed", failed.Reason, StringComparison.Ordinal);
            Assert.Contains("base..head", failed.What, StringComparison.Ordinal);
        }
    }

    private static ScriptedProcessRunner CreateGitScript(ReviewHarness harness, string scenario)
    {
        var diff = scenario == "MissingObjectInTheDiff"
            ? ScriptedProcessRunner.Failure(128, "fatal: unable to read the object of this commit")
            : ScriptedProcessRunner.Output("diff --git a/src/Core.cs b/src/Core.cs\n--- a/src/Core.cs\n+++ b/src/Core.cs\n@@ -1 +1 @@\n-before\n+after\n");

        return new ScriptedProcessRunner()
            .Route(request => request.Arguments.Contains("--show-toplevel"), request => ScriptedProcessRunner.Output(harness.RepositoryFolder + "\n" + Path.Combine(harness.RepositoryFolder, ".git") + "\n"))
            .Route(request => request.Arguments.Contains("--verify"), request => ScriptedProcessRunner.Output(ResolveRevision(request.Arguments[^1]) + "\n"))
            .Route(request => request.Arguments.Contains("diff"), request => diff)
            .Route(request => request.Arguments.Contains("read-tree"), request => new ProcessResult(-2, string.Empty, "git did not finish within 120 seconds and was stopped", true));
    }

    /// <summary>
    /// The object a name stands for: the commit of a revision, or, written with ^{tree}, its tree.
    /// </summary>
    private static string ResolveRevision(string expression)
    {
        var cut = expression.IndexOf('^', StringComparison.Ordinal);
        var name = expression.Substring(0, cut);
        return FakeGit.IdOf(expression.Substring(cut) == "^{tree}" ? "tree of " + name : name);
    }
}
