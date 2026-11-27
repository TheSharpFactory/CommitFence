using System.Text.Json;
using CF.Cli.Commands;
using CF.Cli.Console;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// The SARIF report of "commitfence review ... --format sarif": what a build server reads for each kind of ending, and that
/// nothing of the machine is in it.
/// </summary>
public sealed class SarifReportTests
{
    private const string _path = "src/My Core.cs";
    private const string _allow = "// commitfence:allow TEAM010: generated code";

    [Fact]
    public void ABlockedReviewIsReportedWithItsRuleLocationAndVerdictAndTheConsoleKeepsTheText()
    {
        using var harness = new ReviewHarness();
        var after = ReviewSources.Class("Core", ReviewSources.SignBad);
        AddChanges(harness, ReviewSources.Settings(ReviewSources.Team010), after);

        var plain = harness.Run("review", "--base", "old", "--head", "new");
        var report = harness.Run("review", "--base", "old", "--head", "new", "--format", "sarif");
        var filed = harness.Run("review", "--base", "old", "--head", "new", "--format", "sarif", "--output", "out/report.sarif");

        Assert.Equal(ExitCode.Blocked, report.ExitCode);
        Assert.Equal(report.ExitCode, plain.ExitCode);
        Assert.Equal(report.ExitCode, filed.ExitCode);
        Assert.Empty(report.Error);
        var written = File.ReadAllText(Path.Combine(harness.RepositoryFolder, "out", "report.sarif"));
        Assert.Equal(report.Output, written);
        Assert.Equal(plain.Output, filed.Output);
        Assert.DoesNotContain("\"$schema\"", filed.Output, StringComparison.Ordinal);

        var log = ReadLog(report.Output);
        Assert.Equal("https://json.schemastore.org/sarif-2.1.0.json", log.Schema);
        Assert.Equal("2.1.0", log.Version);
        var run = Assert.Single(log.Runs);
        var driver = run.Tool.Driver;
        Assert.Equal("CommitFence", driver.Name);
        Assert.Equal(FixedProduct.ProgramVersion, driver.Version);
        var rule = Assert.Single(driver.Rules);
        Assert.Equal("TEAM010", rule.Id);
        Assert.Equal("No braces around a single-line if", rule.ShortDescription.Text);
        Assert.Equal("team", rule.Properties.Source);

        var result = Assert.Single(run.Results);
        Assert.Equal("TEAM010", result.RuleId);
        Assert.Equal("error", result.Level);
        var location = Assert.Single(result.Locations!).PhysicalLocation;
        Assert.Equal("src/My%20Core.cs", location.ArtifactLocation.Uri);
        Assert.Equal("%SRCROOT%", location.ArtifactLocation.UriBaseId);
        Assert.Equal(ReviewSources.LineOf(after, "if(x > 0)"), location.Region?.StartLine);
        Assert.DoesNotContain("\"suppressions\"", report.Output, StringComparison.Ordinal);

        var properties = run.Properties;
        Assert.Equal("blocked", properties.Outcome);
        Assert.Equal(1, properties.ExitCode);
        Assert.False(properties.Quick);
        Assert.Contains("\"commitfence/quick\"", report.Output, StringComparison.Ordinal);
        Assert.Equal(FakeGit.IdOf("new"), properties.Commit);
        var provenance = Assert.Single(run.VersionControlProvenance!);
        Assert.Equal("https://example.invalid/origin", provenance.RepositoryUri);
        Assert.Equal(FakeGit.IdOf("new"), provenance.RevisionId);
        var hookInput = $"refs/heads/main {FakeGit.IdOf("new")} refs/heads/main {FakeGit.IdOf("old")}\n";
        var pushed = harness.RunWithInput(["review", "--hook", "pre-push", "origin", "https://example.invalid/origin", "--format", "sarif"], hookInput);
        var pushedRun = ReadLog(pushed.Output).Runs[0];
        Assert.Equal(FakeGit.IdOf("new"), pushedRun.VersionControlProvenance![0].RevisionId);
        var invocation = Assert.Single(run.Invocations);
        Assert.True(invocation.ExecutionSuccessful);
        Assert.Equal(1, invocation.ExitCode);
        AssertNoPathOfThisMachine(harness, report.Output);
    }

    [Fact]
    public void AWarnedReviewReportsAWarningAndTheFindingSetAsideAsAnAcceptedSuppression()
    {
        using var harness = new ReviewHarness();
        var warn = ReviewSources.Team010.Replace("\"block\"", "\"warn\"", StringComparison.Ordinal);
        var after = ReviewSources.Class("Core", "public int Sign(int x)\n{\n    if(x > 0) { return 1; } " + _allow + "\n    if(x < 0) { return 2; }\n\n    return 0;\n}");
        AddChanges(harness, ReviewSources.Settings(warn), after);

        var report = harness.Run("review", "--base", "old", "--head", "new", "--format", "sarif");

        Assert.Equal(ExitCode.Passed, report.ExitCode);
        var run = Assert.Single(ReadLog(report.Output).Runs);
        var results = run.Results;
        Assert.Equal(2, results.Count);
        var warning = Assert.Single(results, item => item.Suppressions == null);
        Assert.Equal("warning", warning.Level);
        Assert.Equal(ReviewSources.LineOf(after, "if(x < 0)"), warning.Locations![0].PhysicalLocation.Region?.StartLine);
        var setAside = Assert.Single(results, item => item.Suppressions != null);
        var suppression = Assert.Single(setAside.Suppressions!);
        // Only the set-aside result carries the key: the warning has no suppressions key at all, not even null.
        Assert.Equal(1, report.Output.Split("\"suppressions\"").Length - 1);
        Assert.Equal("inSource", suppression.Kind);
        Assert.Equal("accepted", suppression.Status);
        Assert.Equal("generated code", suppression.Justification);
        Assert.Equal(ReviewSources.LineOf(after, "commitfence:allow"), suppression.Location?.PhysicalLocation.Region?.StartLine);
        Assert.Equal("warned", run.Properties.Outcome);
        Assert.True(run.Invocations[0].ExecutionSuccessful);
        AssertNoPathOfThisMachine(harness, report.Output);
    }

    [Fact]
    public void AnIncompleteQuickReviewSaysSoInItsInvocationAndNamesNoPathOfTheMachine()
    {
        using var harness = new ReviewHarness();
        File.WriteAllText(Path.Combine(harness.RepositoryFolder, "commitfence.json"), ReviewSources.Settings(ReviewSources.Team010));

        var report = harness.Run("review", "--files", "src/Missing.cs", "--quick", "--format", "sarif");

        Assert.Equal(ExitCode.Incomplete, report.ExitCode);
        var run = Assert.Single(ReadLog(report.Output).Runs);
        var invocation = Assert.Single(run.Invocations);
        Assert.False(invocation.ExecutionSuccessful);
        Assert.Contains("\"executionSuccessful\"", report.Output, StringComparison.Ordinal);
        Assert.Equal(2, invocation.ExitCode);
        var notification = Assert.Single(invocation.ToolExecutionNotifications!);
        Assert.StartsWith("src/Missing.cs:", notification.Message.Text, StringComparison.Ordinal);
        var properties = run.Properties;
        Assert.Equal("incomplete", properties.Outcome);
        Assert.Equal(2, properties.ExitCode);
        Assert.True(properties.Quick);
        Assert.Contains("src/Missing.cs", properties.Coverage.Entries[0].What, StringComparison.Ordinal);
        Assert.Empty(run.Results);
        AssertNoPathOfThisMachine(harness, report.Output);
    }

    [Fact]
    public void ABlockedReviewThatCouldNotCoverEverythingStillSaysItIsIncomplete()
    {
        using var harness = new ReviewHarness();
        File.WriteAllText(Path.Combine(harness.RepositoryFolder, "commitfence.json"), ReviewSources.Settings(ReviewSources.Team010));
        Directory.CreateDirectory(Path.Combine(harness.RepositoryFolder, "src"));
        File.WriteAllText(Path.Combine(harness.RepositoryFolder, _path), ReviewSources.Class("Core", ReviewSources.SignBad));

        var report = harness.Run("review", "--files", _path, "src/Missing.cs", "--format", "sarif");

        Assert.Equal(ExitCode.Blocked, report.ExitCode);
        var run = Assert.Single(ReadLog(report.Output).Runs);
        Assert.Equal("blocked", run.Properties.Outcome);
        var invocation = Assert.Single(run.Invocations);
        Assert.False(invocation.ExecutionSuccessful);
        Assert.Contains("\"executionSuccessful\"", report.Output, StringComparison.Ordinal);
        Assert.Equal(1, invocation.ExitCode);
        var notification = Assert.Single(invocation.ToolExecutionNotifications!);
        Assert.StartsWith("src/Missing.cs:", notification.Message.Text, StringComparison.Ordinal);
        Assert.Equal("error", notification.Level);
        AssertNoPathOfThisMachine(harness, report.Output);
    }

    [Theory]
    [InlineData("failed in /workspaces/runner/private/Build.cs(12,5): boom", "failed in <path>(12,5): boom")]
    [InlineData("failed in \"C:\\Build Cache\\private\\Build.cs\": boom", "failed in <path>: boom")]
    [InlineData("failed in C:\\Build Cache\\private\\Build.cs(12,5): boom", "failed in <path>(12,5): boom")]
    [InlineData("failed in \\\\build-server\\share\\private\\Build.cs(3): boom", "failed in <path>(3): boom")]
    [InlineData("failed in '\\\\build-server\\my share\\private\\Build.cs': boom", "failed in <path>: boom")]
    [InlineData("failed in \"/Users/me/My Project/private/Build.cs\": boom", "failed in <path>: boom")]
    [InlineData("failed in /work/repo/src/A.cs(3,1): boom", "failed in src/A.cs(3,1): boom")]
    [InlineData("failed in \"/work/repo/src/My Core.cs\": boom", "failed in \"src/My Core.cs\": boom")]
    [InlineData("see https://example.invalid/a/b and and/or the next one", "see https://example.invalid/a/b and and/or the next one")]
    public void TextFromTheBuildOrGitLosesEveryAbsolutePathWhateverItsForm(string text, string expected)
    {
        var paths = PathsOfRepository();

        Assert.Equal(expected, paths.Clean(text));
        Assert.Equal("Use /api/users here", paths.CleanAuthored("Use /api/users here"));
        Assert.Equal("src/A.cs", paths.CleanAuthored("/work/repo/src/A.cs"));
    }

    [Theory]
    [InlineData("src/My Core.cs", "src/My%20Core.cs")]
    [InlineData("src/a\\b.cs", "src/a%5Cb.cs")]
    [InlineData("./src//Core.cs", "src/Core.cs")]
    [InlineData("/work/Repo/src/A.cs", null)]
    [InlineData("/work/repo/src/A.cs", null)]
    [InlineData("C:/work/repo/src/A.cs", null)]
    [InlineData("\\\\server\\share\\A.cs", null)]
    [InlineData("src/../../A.cs", null)]
    public void ALocationIsTheRepositoryRelativePathAsGitGivesItAndNeverAnotherFile(string file, string? expected)
    {
        Assert.Equal(expected, SarifPaths.RelativeUri(file));
    }

    [Fact]
    public void AnAcceptedSuppressionStaysOnItsResultWhenThePlaceOfTheCommentCannotBeGiven()
    {
        var finding = new Finding("CS0168", FindingSource.Compiler, Severity.Warn, "csharp", null, null, "/elsewhere/obj/Generated.cs", 4, 1, "unused", null, null);
        var result = new ReviewResult(ReviewOutcome.Clean, [], [], [], string.Empty, TimeSpan.Zero) { ExemptedFindings = [new ExemptedFinding(finding, 3, "generated code")] };
        var output = new StringWriter();

        new SarifReporter(output, "1.0.0", PathsOfRepository()).Write(result);

        var report = output.ToString();
        var item = Assert.Single(ReadLog(report).Runs[0].Results);
        Assert.DoesNotContain("\"locations\"", report, StringComparison.Ordinal);
        var suppression = Assert.Single(item.Suppressions!);
        Assert.Equal("accepted", suppression.Status);
        Assert.Equal("generated code", suppression.Justification);
        Assert.DoesNotContain("\"location\"", report, StringComparison.Ordinal);
        Assert.DoesNotContain("elsewhere", report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://example.invalid/acme/app.git", "https://example.invalid/acme/app.git")]
    [InlineData("https://user:secret-token@example.invalid/acme/app.git", "https://example.invalid/acme/app.git")]
    [InlineData("git@example.invalid:acme/app.git", "ssh://example.invalid/acme/app.git")]
    [InlineData("ssh://git@example.invalid:2222/acme/app.git", "ssh://example.invalid:2222/acme/app.git")]
    [InlineData("/srv/git/app.git", "")]
    [InlineData("C:\\repos\\app", "")]
    [InlineData("file:///srv/git/app.git", "")]
    [InlineData("../app", "")]
    [InlineData("", "")]
    public void ARemoteIsNamedByItsAddressWithoutCredentialsAndNeverByALocalPath(string address, string expected)
    {
        Assert.Equal(expected, RemoteAddress.PublicOf(address));
    }

    [Fact]
    public void AReportThatCannotBeWrittenFailsAReviewThatWouldHavePassedAndSaysWhy()
    {
        using var harness = new ReviewHarness();
        File.WriteAllText(Path.Combine(harness.RepositoryFolder, "commitfence.json"), ReviewSources.Settings(ReviewSources.Team010));
        Directory.CreateDirectory(Path.Combine(harness.RepositoryFolder, "src"));
        File.WriteAllText(Path.Combine(harness.RepositoryFolder, "src/Good.cs"), ReviewSources.Class("Core", ReviewSources.SignOk));
        File.WriteAllText(Path.Combine(harness.RepositoryFolder, _path), ReviewSources.Class("Core", ReviewSources.SignBad));
        File.WriteAllText(Path.Combine(harness.RepositoryFolder, "blocker"), "a file where a folder is needed");

        var passed = harness.Run("review", "--files", "src/Good.cs", "--format", "sarif", "--output", "blocker/report.sarif");
        var blocked = harness.Run("review", "--files", _path, "--format", "sarif", "--output", "blocker/report.sarif");

        Assert.Equal(ExitCode.BadSettingsOrUsage, passed.ExitCode);
        Assert.Contains("blocker/report.sarif", passed.Error, StringComparison.Ordinal);
        Assert.Equal(ExitCode.Blocked, blocked.ExitCode);
        Assert.Contains("blocker/report.sarif", blocked.Error, StringComparison.Ordinal);
    }

    private static SarifLog ReadLog(string report)
    {
        return Assert.IsType<SarifLog>(JsonSerializer.Deserialize(report, SarifJsonContext.Default.SarifLog));
    }

    private static SarifPaths PathsOfRepository()
    {
        var environment = new CommandEnvironment("/work/repo", "/program", "/program/commitfence", new StringReader(string.Empty), new StringWriter(), new StringWriter(), false);
        var empty = new ReviewResult(ReviewOutcome.Clean, [], [], [], string.Empty, TimeSpan.Zero);

        return SarifPaths.Of(empty, "/work/repo", environment);
    }

    private static void AssertNoPathOfThisMachine(ReviewHarness harness, string report)
    {
        foreach(var folder in new[] { harness.RepositoryFolder, harness.Scratch })
        {
            Assert.DoesNotContain(folder, report, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(folder.Replace('\\', '/'), report, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(folder.Replace("\\", "\\\\", StringComparison.Ordinal), report, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("/program", report, StringComparison.Ordinal);
    }

    private static void AddChanges(ReviewHarness harness, string settings, string after)
    {
        harness.Fake.AddCommit("old", new Dictionary<string, string> { ["commitfence.json"] = settings });
        harness.Fake.AddCommit("new", new Dictionary<string, string> { ["commitfence.json"] = settings, [_path] = after }, "old");
    }
}
