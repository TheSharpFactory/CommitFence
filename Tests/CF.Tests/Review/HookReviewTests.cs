using CF.Core.Review;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// The pre-push hook command, "commitfence review --hook pre-push", over a repository in memory: what it does with the
/// settings the pushed tree holds, with the lines git gives it (or none), and with a push that changes only the settings.
/// </summary>
public sealed class HookReviewTests
{
    private const string _remoteName = "origin";

    private static readonly string[] _hookArguments = { "review", "--hook", "pre-push", _remoteName, "https://example.invalid/origin" };

    [Theory]
    [InlineData("{ \"requires\": \"99.0.0.0\", \"unknownRootKey\": 1, \"rules\": [] }", "readme", ExitCode.Incomplete, "99.0.0.0", TestDisplayName = "a minimum the program does not meet is told before an unknown key is, and before a push of no code is skipped")]
    [InlineData("{ \"requires\": \"1.0.0.0\", \"rules\": [" + ReviewSources.Team010 + "] }", "breach", ExitCode.Blocked, "TEAM010", TestDisplayName = "a minimum the program meets still has the change judged by the rule")]
    [InlineData("{ \"requires\": \"1.0.0.0\", \"unknownRootKey\": true, \"rules\": [] }", "clean", ExitCode.BadSettingsOrUsage, "unknownRootKey", TestDisplayName = "an unknown key of the settings is named")]
    [InlineData("{ \"requires\": \"1-2\", \"rules\": [] }", "clean", ExitCode.BadSettingsOrUsage, "1-2", TestDisplayName = "a minimum that is not a version is named")]
    public void HookReviewHonorsRepositorySettingsBeforeSkippingOrJudgingChanges(string settings, string change, ExitCode expectedExit, string expectedText)
    {
        var before = ReviewSources.Class("Core", ReviewSources.Add);
        var after = change switch
        {
            "breach" => ReviewSources.Class("Core", ReviewSources.Add, ReviewSources.SignBad),
            "clean" => ReviewSources.Class("Core", ReviewSources.Add, ReviewSources.Twice),
            _ => before
        };
        using(var harness = new ReviewHarness())
        {
            harness.Fake.AddCommit("old", new Dictionary<string, string> { ["commitfence.json"] = settings, ["README.md"] = "first text\n", ["src/Core.cs"] = before });
            harness.Fake.AddCommit("new", new Dictionary<string, string> { ["commitfence.json"] = settings, ["README.md"] = change == "readme" ? "second text\n" : "first text\n", ["src/Core.cs"] = after }, "old");
            harness.Fake.RemoteTips.Add(FakeGit.IdOf("old"));

            var outcome = harness.RunWithInput(_hookArguments, PushLine("new", "old"));

            Assert.Equal(expectedExit, outcome.ExitCode);
            Assert.Contains(expectedText, outcome.Text, StringComparison.Ordinal);
            if(expectedExit == ExitCode.Incomplete)
                Assert.Contains("update", outcome.Text, StringComparison.OrdinalIgnoreCase);

            if(change == "breach")
                Assert.Contains($"src/Core.cs({ReviewSources.LineOf(after, "if(x > 0)")},", outcome.Text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("KnownUpToDate")]
    [InlineData("UnpublishedLocalCommits")]
    public void EmptyHookInputDependsOnUnpublishedWork(string scenario)
    {
        using(var harness = new ReviewHarness())
        {
            harness.Fake.AddCommit("published", new Dictionary<string, string> { ["README.md"] = "text\n" });
            harness.Fake.AddCommit("local", new Dictionary<string, string> { ["README.md"] = "more text\n" }, "published");
            harness.Fake.RemoteTips.Add(FakeGit.IdOf("published"));
            harness.Fake.LocalTips.Add(FakeGit.IdOf(scenario == "KnownUpToDate" ? "published" : "local"));

            var outcome = harness.RunWithInput(_hookArguments, string.Empty);

            if(scenario == "KnownUpToDate")
            {
                Assert.Equal(ExitCode.Passed, outcome.ExitCode);
                Assert.Empty(harness.Fake.Diffs);
                return;
            }

            Assert.Equal(ExitCode.Incomplete, outcome.ExitCode);
            Assert.Contains("no ref lines", outcome.Text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Malformed")]
    [InlineData("EnablesBuildDependentRule")]
    public void SettingsOnlyChangeIsValidatedWithoutBuilding(string scenario)
    {
        var toolchain = new FakeToolchain();
        var source = ReviewSources.Class("Core", ReviewSources.Add);
        var newSettings = scenario == "Malformed" ? "{ \"rules\": [ \n" : ReviewSources.Settings(ReviewSources.Team011);
        using(var harness = new ReviewHarness())
        {
            harness.Languages = new List<CF.Core.Code.ILanguage> { new TestLanguage(toolchain) };
            harness.Fake.AddCommit("old", new Dictionary<string, string> { ["commitfence.json"] = ReviewSources.Settings(ReviewSources.Team010), ["src/Core.cs"] = source });
            harness.Fake.AddCommit("new", new Dictionary<string, string> { ["commitfence.json"] = newSettings, ["src/Core.cs"] = source }, "old");

            var outcome = harness.Run("review", "--base", "old", "--head", "new");

            Assert.Empty(toolchain.Requests);
            if(scenario == "Malformed")
            {
                Assert.Equal(ExitCode.BadSettingsOrUsage, outcome.ExitCode);
                Assert.Contains("commitfence.json", outcome.Text, StringComparison.Ordinal);
                Assert.Contains("tree under review", outcome.Text, StringComparison.Ordinal);
                return;
            }

            Assert.Equal(ExitCode.Passed, outcome.ExitCode);
            Assert.Contains("Rule files changed: commitfence.json", outcome.Text, StringComparison.Ordinal);
        }
    }

    private static string PushLine(string localCommit, string remoteCommit)
    {
        return $"refs/heads/main {FakeGit.IdOf(localCommit)} refs/heads/main {FakeGit.IdOf(remoteCommit)}\n";
    }
}
