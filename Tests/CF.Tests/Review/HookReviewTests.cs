using CF.Core.Code;
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
    [Theory]
    [InlineData("--staged")]
    [InlineData("--hook")]
    [InlineData("--files")]
    public void QuickReviewReadsFilesAndLeavesTheBuildOut(string form)
    {
        using var harness = new ReviewHarness();
        var toolchain = new FakeToolchain();
        harness.Languages = new List<ILanguage> { new TestLanguage(toolchain) };
        var contents = new Dictionary<string, string>
        {
            ["Example.cs"] = "internal class Example {}",
            ["commitfence.json"] = ReviewSources.Settings(ReviewSources.Team010, ReviewSources.Team011)
        };
        harness.Fake.AddCommit("index", contents);
        harness.Fake.StagedTree = FakeGit.TreeIdOf("index");
        foreach(var (file, text) in contents)
            File.WriteAllText(Path.Combine(harness.RepositoryFolder, file), text);

        var result = form switch
        {
            "--hook" => harness.Run("review", form, "pre-commit", "--quick"),
            "--files" => harness.Run("review", form, "Example.cs", "--quick"),
            _ => harness.Run("review", form, "--quick")
        };

        Assert.Equal(ExitCode.Passed, result.ExitCode);
        Assert.Contains("1 of 1 files reviewed", result.Output, StringComparison.Ordinal);
        Assert.Contains("Quick review left out the build", result.Output, StringComparison.Ordinal);
        Assert.Empty(toolchain.Requests);
    }

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

    [Fact]
    public void PromptUsesEachTreesRuleAndSourceForTheSameFile()
    {
        using var harness = new ReviewHarness();
        const string path = "src/Core.cs";
        var firstRule = ReviewSources.Team010.Replace("No braces", "First rule: no braces", StringComparison.Ordinal);
        var secondRule = ReviewSources.Team010.Replace("No braces", "Second rule: no braces", StringComparison.Ordinal);
        var firstSource = ReviewSources.Class("Core", ReviewSources.SignBad.Replace("x > 0", "x > 1", StringComparison.Ordinal));
        var secondSource = ReviewSources.Class("Core", ReviewSources.SignBad.Replace("x > 0", "x > 2", StringComparison.Ordinal));
        harness.Fake.AddCommit("old", new Dictionary<string, string> { ["commitfence.json"] = ReviewSources.Settings(ReviewSources.Team010) });
        harness.Fake.AddCommit("first", new Dictionary<string, string> { ["commitfence.json"] = ReviewSources.Settings(firstRule), [path] = firstSource }, "old");
        harness.Fake.AddCommit("second", new Dictionary<string, string> { ["commitfence.json"] = ReviewSources.Settings(secondRule), [path] = secondSource }, "old");

        var outcome = harness.RunWithInput([.. _hookArguments, "--format", "prompt"], PushLine("first", "old") + "\n" + PushLine("second", "old").Replace("refs/heads/main", "refs/heads/topic", StringComparison.Ordinal));

        Assert.Equal(ExitCode.Blocked, outcome.ExitCode);
        var blocks = outcome.Output.Split("2 of 2", StringSplitOptions.None);
        Assert.Equal(2, blocks.Length);
        Assert.Contains("TEAM010: First rule: no braces", blocks[0], StringComparison.Ordinal);
        Assert.Contains("x > 1", blocks[0], StringComparison.Ordinal);
        Assert.DoesNotContain("x > 2", blocks[0], StringComparison.Ordinal);
        Assert.Contains("TEAM010: Second rule: no braces", blocks[1], StringComparison.Ordinal);
        Assert.Contains("x > 2", blocks[1], StringComparison.Ordinal);
    }

    [Fact]
    public void FindingsOfDifferentTreesAreKeptWhenTheirSourceOrRuleDiffers()
    {
        using var harness = new ReviewHarness();
        const string path = "src/Core.cs";
        var withMessage = ReviewSources.Team010.Replace("\"title\":", "\"message\": \"Remove the braces\", \"title\":", StringComparison.Ordinal);
        var otherTitle = withMessage.Replace("No braces", "Braces are not allowed", StringComparison.Ordinal);
        var firstSource = ReviewSources.Class("Core", ReviewSources.SignBad.Replace("x > 0", "x > 1", StringComparison.Ordinal));
        var secondSource = ReviewSources.Class("Core", ReviewSources.SignBad.Replace("x > 0", "x > 2", StringComparison.Ordinal));
        harness.Fake.AddCommit("old", new Dictionary<string, string> { ["commitfence.json"] = ReviewSources.Settings(withMessage) });
        harness.Fake.AddCommit("first", new Dictionary<string, string> { ["commitfence.json"] = ReviewSources.Settings(withMessage), [path] = firstSource }, "old");
        harness.Fake.AddCommit("second", new Dictionary<string, string> { ["commitfence.json"] = ReviewSources.Settings(withMessage), [path] = secondSource }, "old");
        harness.Fake.AddCommit("third", new Dictionary<string, string> { ["commitfence.json"] = ReviewSources.Settings(otherTitle), [path] = firstSource }, "old");
        var input = PushLine("first", "old") + "\n" + PushLine("second", "old").Replace("refs/heads/main", "refs/heads/topic", StringComparison.Ordinal)
            + "\n" + PushLine("third", "old").Replace("refs/heads/main", "refs/heads/other", StringComparison.Ordinal);

        var outcome = harness.RunWithInput([.. _hookArguments, "--format", "prompt"], input);

        Assert.Equal(ExitCode.Blocked, outcome.ExitCode);
        Assert.Contains("3 of 3", outcome.Output, StringComparison.Ordinal);
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
