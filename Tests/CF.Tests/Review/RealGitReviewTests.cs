using CF.Adapters.Git;
using CF.Core.Changes;
using CF.Core.Ports;
using CF.Core.Setup;
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

    [Theory]
    [InlineData(false, "Empty.cs", "FILETYPES")]
    [InlineData(true, "Order.tmp.cs", "FILENAME")]
    public void PrePushReviewsFileRulesOnChangesWithoutHunks(bool rename, string changedPath, string expectedRule)
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("without-hunks");
        var rules = ReviewSources.Settings(
            """{"id":"FILETYPES","language":"csharp","for":"file","severity":"block","title":"Declare a type","require":{"typesInFile":{"atLeast":1}}}""",
            """{"id":"FILENAME","language":"csharp","for":"file","severity":"block","title":"Avoid temporary names","where":{"name":{"matches":"*.tmp.cs"}},"forbid":true}""",
            """{"id":"CLASSNAME","language":"csharp","for":"class","severity":"block","title":"Keep the original class name","require":{"name":"Original"}}""");
        repository.WriteProject(rules, ("Order.cs", "class Order {}\n"));
        var before = repository.Commit("initial");
        if(rename)
            repository.Git("mv", "Order.cs", changedPath);
        else
            repository.Write(changedPath, string.Empty);

        var after = repository.Commit("change without hunks");
        var arguments = new List<string> { "review", "--hook", "pre-push", "origin", repository.Remote };
        var input = $"refs/heads/main {after} refs/heads/main {before}\n";
        var review = sandbox.Execute(new ProcessRequest(GitSandbox.ProgramPath, arguments, repository.Folder, new Dictionary<string, string?>(), input, TimeSpan.FromMinutes(10)));

        Assert.True(review.ExitCode == 1, $"Expected {expectedRule} to block {changedPath}, but the review exited with {review.ExitCode}:{Environment.NewLine}{review.Output}{review.Error}");
        Assert.Contains(changedPath + "(1,", review.Output, StringComparison.Ordinal);
        Assert.Contains(expectedRule, review.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("CLASSNAME", review.Output, StringComparison.Ordinal);
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
    public void StagingTheSameFileAgainChangesWhatTheIndexIsKnownBy()
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("restaged");
        repository.WriteProject(ReviewSources.Settings(ReviewSources.Team010), ("src/Core.cs", _clean));
        repository.Commit("initial");
        var git = new GitCommandLine(sandbox.Runner, new FolderOfTest(sandbox.DataFolder));
        var finder = new StagedScopeFinder(git, git);
        repository.Write("src/Core.cs", _cleanAndMore);
        repository.Git("add", "src/Core.cs");
        var clean = finder.Find(repository.Folder);

        repository.Write("src/Core.cs", _breach);
        repository.Git("add", "src/Core.cs");
        var restaged = finder.Find(repository.Folder);

        // the same path is staged, so a list of paths cannot tell the two states apart; the trees can
        Assert.Equal(clean.Staged, restaged.Staged);
        Assert.NotEqual(clean.IndexTree, restaged.IndexTree);
        Assert.Equal(repository.Git("write-tree").Trim(), restaged.IndexTree);
        Assert.Equal(repository.Git("rev-parse", "HEAD^{tree}").Trim(), restaged.BaseTree);
        Assert.Equal(clean.BaseTree, restaged.BaseTree);
    }

    [Fact]
    public void ANewFolderIsCountedFileByFileInTheFilesChangedButNotStaged()
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("untracked");
        repository.WriteProject(ReviewSources.Settings(ReviewSources.Team010), ("src/Core.cs", _clean));
        repository.Commit("initial");
        var git = new GitCommandLine(sandbox.Runner, new FolderOfTest(sandbox.DataFolder));
        var finder = new StagedScopeFinder(git, git);
        repository.Write("src/Billing/Invoice.cs", _clean);
        repository.Write("src/Billing/Totals.cs", _clean);

        var answer = finder.Find(repository.Folder);

        Assert.Empty(answer.Staged);
        Assert.Equal(2, answer.UnstagedCount);
        Assert.Equal(["src/Billing/Invoice.cs", "src/Billing/Totals.cs"], answer.UnstagedFiles.Select(file => file.Path).Order().ToList());
        Assert.All(answer.UnstagedFiles, file => Assert.True(file.IsNew));
    }

    [Fact]
    public void SettingsCommandsUseTheRepositoryRootFromASubfolder()
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("settings");
        repository.Write("commitfence.json", ReviewSources.Settings(ReviewSources.Team010));
        repository.Write("src/custom.json", ReviewSources.Settings());
        var folder = Path.Combine(repository.Folder, "src");

        Assert.Contains("TEAM010", sandbox.RunProgram(folder, "rules", "list").Output, StringComparison.Ordinal);
        Assert.DoesNotContain("TEAM010", sandbox.RunProgram(folder, "rules", "list", "--settings", "custom.json").Output, StringComparison.Ordinal);
        Assert.Equal(0, sandbox.RunProgram(folder, "settings", "set", "strictness", "strict").ExitCode);
        Assert.Contains("Strictness: Strict", sandbox.RunProgram(folder, "settings", "show").Output, StringComparison.Ordinal);
        Assert.Equal(0, sandbox.RunProgram(folder, "structure", "set", "add-module", "Core").ExitCode);
        Assert.Contains("Core", sandbox.RunProgram(folder, "structure", "show").Output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(folder, "commitfence.json")));
    }

    [Fact]
    public void PushScopeChoosesTheConfiguredRemoteBeforeTheUpstream()
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("push-remote");
        repository.Write("src/Core.cs", _clean);
        var published = repository.Commit("published");
        repository.Write("src/Core.cs", _breach);
        var tip = repository.Commit("outgoing");
        repository.Git("remote", "add", "fork", "unused-fork");
        repository.Git("remote", "add", "backup", "unused-backup");
        repository.Git("config", "branch.main.remote", "origin");
        repository.Git("config", "branch.main.merge", "refs/heads/integration");
        repository.Git("update-ref", "refs/remotes/origin/integration", tip);
        repository.Git("update-ref", "refs/remotes/fork/main", published);
        repository.Git("config", "remote.pushDefault", "backup");
        repository.Git("config", "branch.main.pushRemote", "fork");
        var finder = new PushScopeFinder(new GitCommandLine(sandbox.Runner, new FolderOfTest(sandbox.DataFolder)));

        var fork = finder.Find(repository.Folder);
        Assert.Equal("fork", fork.Scope!.RemoteName);
        var forkRef = Assert.Single(fork.Scope.PushedRefs.Refs);
        Assert.Equal("refs/heads/main", forkRef.RemoteRef);
        Assert.Equal(published, forkRef.RemoteObject);

        repository.Git("config", "--unset", "branch.main.pushRemote");
        Assert.Equal("backup", finder.Find(repository.Folder).Scope!.RemoteName);

        repository.Git("config", "--unset", "remote.pushDefault");
        repository.Git("config", "push.default", "upstream");
        var upstream = finder.Find(repository.Folder);
        Assert.Equal("origin", upstream.Scope!.RemoteName);
        Assert.Equal("refs/heads/integration", Assert.Single(upstream.Scope.PushedRefs.Refs).RemoteRef);

        repository.Git("config", "--unset", "push.default");
        repository.Git("branch", "topic", published);
        repository.Git("config", "branch.topic.pushRemote", "fork");
        var topic = finder.Find(repository.Folder, "refs/heads/topic");
        Assert.Equal("fork", topic.Scope!.RemoteName);
        var topicRef = Assert.Single(topic.Scope.PushedRefs.Refs);
        Assert.Equal("refs/heads/topic", topicRef.LocalRef);
        Assert.Equal(published, topicRef.LocalObject);
        Assert.Equal("main", repository.Git("branch", "--show-current"));
    }

    [Theory]
    [InlineData(null, false, "published")]
    [InlineData("simple", false, "published")]
    [InlineData("matching", false, "published")]
    [InlineData("nothing", false, "published")]
    [InlineData("current", true, "published")]
    [InlineData("upstream", true, "tip")]
    [InlineData("tracking", true, "tip")]
    public void PushScopeFollowsPushDefaultForABranchThatFollowsAnotherName(string? pushDefault, bool pushes, string remoteObjectIs)
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("push-default");
        repository.Write("src/Core.cs", _clean);
        var published = repository.Commit("published");
        repository.Write("src/Core.cs", _breach);
        var tip = repository.Commit("outgoing");
        repository.Git("config", "branch.main.remote", "origin");
        repository.Git("config", "branch.main.merge", "refs/heads/integration");
        repository.Git("update-ref", "refs/remotes/origin/integration", tip);
        repository.Git("update-ref", "refs/remotes/origin/main", published);
        if(pushDefault != null)
            repository.Git("config", "push.default", pushDefault);
        var finder = new PushScopeFinder(new GitCommandLine(sandbox.Runner, new FolderOfTest(sandbox.DataFolder)));

        var answer = finder.Find(repository.Folder);

        Assert.Equal(pushes, answer.HasPush);
        if(!pushes)
        {
            Assert.Contains("refuse", answer.Reason, StringComparison.Ordinal);
            return;
        }

        var pushed = Assert.Single(answer.Scope!.PushedRefs.Refs);
        Assert.Equal(pushDefault is "current" ? "refs/heads/main" : "refs/heads/integration", pushed.RemoteRef);
        Assert.Equal(remoteObjectIs == "tip" ? tip : published, pushed.RemoteObject);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("right")]
    public void HookExplainsAMissingProgramAndPropagatesItsExitCode(string setting)
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("hook-line");
        repository.Write(".git/hooks/pre-push", "#!/bin/sh\n" + HookLine.PrePush + "\nexit 0\n");
        repository.Write("stub", "#!/bin/sh\nprintf '%s\\n' \"$@\"\nexit 7\n");
        SetExecutable(Path.Combine(repository.Folder, ".git", "hooks", "pre-push"));
        var program = Path.Combine(repository.Folder, "stub");
        SetExecutable(program);
        if(setting != "missing")
            repository.Git("config", HookLine.PathSetting, (setting == "right" ? program : program + "-missing").Replace('\\', '/'));

        var result = sandbox.RunGit(repository.Folder, "hook", "run", "pre-push", "--", "origin", "remote-url");

        if(setting == "right")
        {
            Assert.Equal(7, result.ExitCode);
            Assert.Equal("review\n--hook\npre-push\norigin\nremote-url", (result.Output + result.Error).Trim().Replace("\r\n", "\n", StringComparison.Ordinal));
        }
        else
        {
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("CommitFence is not set up on this machine: run commitfence install from the folder you unpacked it to.", result.Error, StringComparison.Ordinal);
        }
    }

    private static void SetExecutable(string path)
    {
        if(!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
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
