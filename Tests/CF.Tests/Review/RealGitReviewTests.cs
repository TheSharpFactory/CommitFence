using CF.Adapters.Git;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// The review on real git and the real program: a push through the team's hook, the staged content, and the private
/// copy that is reused. These start git and, for the first two, a real build.
/// </summary>
public sealed class RealGitReviewTests
{
    private static readonly string _clean = ReviewSources.Class("Core", ReviewSources.Add);
    private static readonly string _cleanAndMore = ReviewSources.Class("Core", ReviewSources.Add, ReviewSources.Twice);
    private static readonly string _breach = ReviewSources.Class("Core", ReviewSources.Add, ReviewSources.SignBad);
    private static readonly string _fixed = ReviewSources.Class("Core", ReviewSources.Add, ReviewSources.SignOk);

    [Fact]
    public void TeamHookJudgesAllProposedRefsFromCommits()
    {
        using(var sandbox = new GitSandbox())
        {
            sandbox.InstallProgram();
            var repository = sandbox.CreateRepository("team");
            repository.WriteProject(ReviewSources.Settings(ReviewSources.Team010), ("src/Core.cs", _clean));
            repository.InstallTeamHook();
            repository.Commit("initial");
            repository.Git("push", "-q", "--no-verify", "origin", "main");

            // a clean update goes through the hook and the remote follows
            repository.Write("src/Core.cs", _cleanAndMore);
            var cleanTip = repository.Commit("a clean change");
            var cleanPush = sandbox.RunGit(repository.Folder, "push", "origin", "main");

            Assert.Equal(0, cleanPush.ExitCode);
            Assert.Equal("refs/heads/main " + cleanTip, repository.RemoteRefs());

            // a clean new branch and a breaking change to main are proposed together; the breach is then fixed on disk only
            repository.Git("checkout", "-q", "-b", "clean-topic");
            repository.Write("src/Core.cs", ReviewSources.Class("Core", ReviewSources.Add, ReviewSources.Twice, ReviewSources.SignOk));
            repository.Commit("a clean branch");
            repository.Git("checkout", "-q", "main");
            repository.Write("src/Core.cs", _breach);
            repository.Commit("a breaking change");
            repository.LeaveWorkInProgress();
            repository.Write("src/Core.cs", _fixed);
            var remoteBefore = repository.RemoteRefs();
            var stateBefore = repository.CaptureDeveloperState();

            var push = sandbox.RunGitWith(repository.Folder, new Dictionary<string, string?> { ["GIT_INDEX_FILE"] = Path.Combine(sandbox.Root, "no-such-index") }, "push", "origin", "main", "clean-topic");

            var text = push.Output + push.Error;
            Assert.NotEqual(0, push.ExitCode);
            var location = $"src/Core.cs({ReviewSources.LineOf(_breach, "if(x > 0)")},";
            Assert.True(text.Contains(location, StringComparison.Ordinal), $"Expected a finding at {location} in the push output:{Environment.NewLine}{text}");
            Assert.Contains("TEAM010", text, StringComparison.Ordinal);
            Assert.Contains("refs/heads/main", text, StringComparison.Ordinal);
            Assert.Contains("refs/heads/clean-topic", text, StringComparison.Ordinal);
            Assert.Equal(remoteBefore, repository.RemoteRefs());
            Assert.Equal(stateBefore, repository.CaptureDeveloperState());
        }
    }

    [Fact]
    public void StagedReviewUsesAndPreservesTheIndex()
    {
        using(var sandbox = new GitSandbox())
        {
            var repository = sandbox.CreateRepository("staged");
            repository.WriteProject(ReviewSources.Settings(ReviewSources.Team010), ("src/Core.cs", _clean));
            repository.Commit("initial");
            repository.LeaveWorkInProgress();
            repository.Write("src/Core.cs", _breach);
            repository.Git("add", "src/Core.cs");
            repository.Write("src/Core.cs", _fixed);
            var stateBefore = repository.CaptureDeveloperState();

            var review = sandbox.RunProgram(repository.Folder, "review", "--staged");

            Assert.True(review.ExitCode == 1, $"Expected the staged review to block with a finding, but it exited with {review.ExitCode}:{Environment.NewLine}{review.Output}{review.Error}");
            Assert.Contains($"src/Core.cs({ReviewSources.LineOf(_breach, "if(x > 0)")},", review.Output, StringComparison.Ordinal);
            Assert.Contains("TEAM010", review.Output, StringComparison.Ordinal);
            Assert.Equal(stateBefore, repository.CaptureDeveloperState());
        }
    }

    [Fact]
    public void ReusingSnapshotRestoresTrackedContent()
    {
        using(var sandbox = new GitSandbox())
        {
            var repository = sandbox.CreateRepository("snapshot");
            repository.Write("a.txt", "committed a\n");
            repository.Write("b.txt", "committed b\n");
            repository.Write("sub/c.txt", "committed c\n");
            repository.Commit("initial");
            var tree = repository.Git("rev-parse", "HEAD^{tree}");
            var data = new FolderOfTest(Path.Combine(sandbox.Root, "private-copies"));
            var location = new GitCommandLine(sandbox.Runner, data).FindRepository(repository.Folder);
            var store = new GitSnapshotStore(sandbox.Runner, data);

            var folder = store.MoveTo(location!, tree);

            Assert.Equal("committed a\n", File.ReadAllText(Path.Combine(folder, "a.txt")));
            File.WriteAllText(Path.Combine(folder, "a.txt"), "edited and longer than it was\n");
            File.Delete(Path.Combine(folder, "b.txt"));

            var again = store.MoveTo(location!, tree);

            Assert.Equal(folder, again);
            Assert.Equal("committed a\n", File.ReadAllText(Path.Combine(again, "a.txt")));
            Assert.Equal("committed b\n", File.ReadAllText(Path.Combine(again, "b.txt")));
            Assert.Equal("committed c\n", File.ReadAllText(Path.Combine(again, "sub", "c.txt")));
        }
    }
}
