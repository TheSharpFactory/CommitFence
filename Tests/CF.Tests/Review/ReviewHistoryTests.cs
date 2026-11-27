using CF.Adapters.Machine;
using CF.Adapters.Storage;
using CF.Cli.Commands;
using CF.Cli.Console;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Services;
using CF.Languages.CSharp;
using CF.Tests.Review.Support;
using CF.Tests.Rules.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>Push history recovery, isolation from verdicts, and the shared Dashboard numbers.</summary>
public sealed class ReviewHistoryTests
{
    private static readonly DateTimeOffset _today = new DateTimeOffset(2026, 11, 24, 0, 30, 0, TimeSpan.FromHours(2));
    private const string _file = "Core.cs";
    private const string _settings = "commitfence.json";

    [Theory]
    [InlineData(ReviewSource.Hook)]
    [InlineData(ReviewSource.CommandLine)]
    [InlineData(ReviewSource.App)]
    public void OnlyPushReviewsRecordTheFinishedFacts(ReviewSource source)
    {
        using var harness = new ReviewHarness();
        var history = new FileReviewHistory(new FixedDataFolder(Path.Combine(harness.Scratch, "history")));
        harness.History = history;
        var started = harness.Clock.Now;
        var toolchain = new FakeToolchain
        {
            Answer = _ =>
            {
                harness.Clock.Now = started.AddMilliseconds(6400);
                return new ToolchainResult([], [], [], FakeToolchain.CompleteAnswers(), []) { RanBuild = true };
            }
        };
        harness.Languages = new List<ILanguage> { new TestLanguage(toolchain) };
        var scope = PreparePush(harness, true).WithSource(source);
        var review = harness.Service();
        var result = review.ReviewChanges(harness.RepositoryFolder, scope, null);

        Assert.Equal(ReviewOutcome.Blocked, result.Outcome);
        var record = Assert.Single(history.Read(harness.Fake.Repository).Records);
        Assert.Equal(ReviewRecord.CurrentFormat, record.Format);
        Assert.Equal(started.AddMilliseconds(6400).ToUniversalTime(), record.EndedAt);
        Assert.Equal(TimeSpan.Zero, record.EndedAt.Offset);
        Assert.Equal(source, record.Source);
        Assert.Equal(RecordedReviewOutcome.Blocked, record.Outcome);
        Assert.Equal(6400, record.DurationMilliseconds);
        Assert.True(record.Full);
        Assert.Equal(1, record.FilesReviewed);
        Assert.Equal(new RecordedRuleCount("TEAM010", 1), Assert.Single(record.Findings));

        review.ReviewChanges(harness.RepositoryFolder, ReviewScope.ForWholeFolder(string.Empty), null);
        review.ReviewChanges(harness.RepositoryFolder, ReviewScope.ForRange("before", "after"), null);
        harness.Fake.StagedTree = FakeGit.TreeIdOf("after");
        review.ReviewChanges(harness.RepositoryFolder, ReviewScope.ForStaged(), null);
        var file = Path.Combine(harness.RepositoryFolder, _file);
        review.ReviewFiles(Path.Combine(harness.RepositoryFolder, _settings), [new FileToReview(_file, file)]);
        Assert.Equal(source, Assert.Single(history.Read(harness.Fake.Repository).Records).Source);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixLeavesHistoryAloneAndStatsShowsWhereReviewsCameFrom(bool apply)
    {
        using var harness = new ReviewHarness();
        var history = new MemoryHistory();
        harness.History = history;
        var scope = PreparePush(harness, true);
        harness.Fake.Name("refs/remotes/origin/main", "before");
        var review = harness.Service();
        review.ReviewChanges(harness.RepositoryFolder, scope, null);
        review.ReviewChanges(harness.RepositoryFolder, scope.WithSource(ReviewSource.CommandLine), null);
        review.ReviewChanges(harness.RepositoryFolder, scope.WithSource(ReviewSource.App), null);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var environment = new CommandEnvironment(harness.RepositoryFolder, "/program", "/program/commitfence", TextReader.Null, output, error, false);
        var data = new FixedDataFolder(Path.Combine(harness.Scratch, "data"));
        var rules = new RuleService(harness.FileSystem, new FileSettingsLock(data), harness.Languages);
        var stats = new StatsService(harness.Git, harness.Clock, history, rules);
        var runner = new CommandRunner(null!, null!, review, null!, null!, environment, stats: stats);

        Assert.Equal(ExitCode.Blocked, runner.Run(apply ? ["fix", "UNKNOWN", _file, "--apply"] : ["fix", "UNKNOWN", _file]));
        Assert.DoesNotContain("there is nothing to push", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(3, history.Appends);
        Assert.Equal(ExitCode.Passed, runner.Run(["stats"]));
        Assert.Contains("3 reviews in 30 days\nFrom the hook: 1 review, 1 blocked. From the command line: 1. From Review: 1.",
            output.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, ExitCode.Blocked)]
    [InlineData(false, ExitCode.Passed)]
    public void HistoryFailureLeavesThePushAndItsPrintedReportAlone(bool blocked, ExitCode exitCode)
    {
        using var baseline = new ReviewHarness();
        var scope = PreparePush(baseline, blocked);
        var before = baseline.Service().ReviewChanges(baseline.RepositoryFolder, scope, null);
        using var failing = new ReviewHarness();
        var history = new MemoryHistory { Fail = true };
        failing.History = history;
        var after = failing.Service().ReviewChanges(failing.RepositoryFolder, PreparePush(failing, blocked), null);

        Assert.Equal(exitCode, before.ExitCode);
        Assert.Equal(exitCode, after.ExitCode);
        Assert.Equal(Print(before), Print(after));
        Assert.Equal(1, history.Appends);
    }

    [Theory]
    [InlineData("complete", 2, 0)]
    [InlineData("torn", 1, 1)]
    [InlineData("unknown", 1, 1)]
    [InlineData("empty", 1, 0)]
    [InlineData("missing fields", 1, 1)]
    public void AppendRepairsTheLineBoundaryAndReadCountsUnreadableLines(string before, int count, int unreadable)
    {
        using var temp = new TestFolder();
        var repo = new RepositoryLocation(temp.Path, Path.Combine(temp.Path, ".git"));
        var history = new FileReviewHistory(new FixedDataFolder(temp.Path));
        var original = Entry(_today, RecordedReviewOutcome.Clean, 2600, false);
        history.Append(repo, original);
        var path = Assert.Single(Directory.GetFiles(temp.Path, "history.jsonl", SearchOption.AllDirectories));
        var complete = File.ReadAllText(path).TrimEnd();
        var prefix = before switch
        {
            "complete" => complete,
            "torn" => complete[..^2],
            "unknown" => complete.Replace("\"format\":1", "\"format\":9", StringComparison.Ordinal),
            "missing fields" => "{\"format\":1}",
            _ => string.Empty
        };
        File.WriteAllText(path, prefix);

        var next = Entry(_today.AddDays(1), RecordedReviewOutcome.Warned, 8200, true);
        history.Append(repo, next);
        var read = history.Read(repo);
        Assert.Null(read.ReadError);
        Assert.Equal(unreadable, read.UnreadableLines);
        Assert.Equal(count, read.Records.Count);
        Assert.Equal(next.EndedAt, read.Records[^1].EndedAt);
        Assert.Equal(next.Outcome, read.Records[^1].Outcome);
        if(count == 2)
            Assert.Equal(original.EndedAt, read.Records[0].EndedAt);

        if(prefix.Length > 0)
            Assert.StartsWith(prefix + "\n", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingHistoryIsEmptyButAnUnopenableHistoryHasAnError()
    {
        using var temp = new TestFolder();
        var repo = new RepositoryLocation(temp.Path, Path.Combine(temp.Path, ".git"));
        var history = new FileReviewHistory(new FixedDataFolder(temp.Path));
        var missing = history.Read(repo);
        Assert.Empty(missing.Records);
        Assert.Null(missing.ReadError);

        var path = Path.Combine(RepositoryDataFolder.For(temp.Path, repo), "history.jsonl");
        Directory.CreateDirectory(path);
        var failed = history.Read(repo);
        Assert.Empty(failed.Records);
        Assert.False(string.IsNullOrWhiteSpace(failed.ReadError));
    }

    [Fact]
    public void StatisticsUseThirtyLocalDaysAndCurrentRules()
    {
        using var temp = new TestFolder();
        var report = Assert.IsType<StatsReport>(Service(temp.Path, false).Read(temp.Path));
        Assert.Equal(30, report.Days.Count);
        Assert.Equal(new DateOnly(2026, 10, 25), report.Oldest);
        Assert.Equal(new StatsDay(new DateOnly(2026, 11, 24), 1, 1, 2), report.Days[^1]);
        Assert.Equal(new StatsDay(new DateOnly(2026, 11, 23), 0, 0, 1), report.Days[^2]);
        Assert.Equal(new StatsDay(new DateOnly(2026, 11, 24), 1, 1, 3), report.Totals);
        Assert.Equal(12000, report.Timing.TypicalMilliseconds);
        Assert.Equal(48000, report.Timing.Slowest?.Milliseconds);
        Assert.Equal(4, report.Timing.Slowest?.FilesReviewed);
        Assert.Equal(new List<int> { 1, 2, 1, 1 }, report.Timing.Bands.Select(band => band.Count));
        Assert.Equal("1 of them quick review, which build nothing", report.Timing.Bands[0].Note);
        Assert.Empty(report.Timing.Bands[1].Note);
        Assert.Equal(2, report.UnreadableLines);
        Assert.Equal(new List<(string, long)> { ("TEAM010", 5L), ("REMOVED01", 2L), ("ALLOW001", 1L), ("ARCH001", 1L) },
            report.MostBroken.Select(row => (row.Id, row.Count)));
        Assert.Equal(Severity.Block, report.MostBroken[0].Rule?.Effect);
        Assert.Null(report.MostBroken[1].Rule);
        Assert.Equal(new StatsRule("ALLOW001", "An exemption needs a reason", false, Severity.Block), report.MostBroken[2].Rule);
        Assert.Equal(new StatsRule("ARCH001", "A use breaks a direction", false, Severity.Block), report.MostBroken[3].Rule);
        Assert.Equal(5, report.RulesInForce);
        Assert.Equal(new List<string> { "TEAM011", "CA1822", "IDE0005", "IDE0055" }, report.NeverFired.Select(rule => rule.Id));
    }

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(false, true, null)]
    [InlineData(true, false, null)]
    [InlineData(false, false, "Read interrupted.")]
    [InlineData(true, false, "Access denied")]
    public void StatsPrintsTheSharedFactsAndTheEmptyState(bool empty, bool showAll, string? readError)
    {
        using var temp = new TestFolder();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var environment = new CommandEnvironment(temp.Path, "/program", "/program/commitfence", TextReader.Null, output, error, false);
        var runner = new CommandRunner(null!, null!, null!, null!, null!, environment, stats: Service(temp.Path, empty, readError));
        var code = runner.Run(showAll ? ["stats", "--all"] : ["stats"]);

        Assert.Equal(ExitCode.Passed, code);
        Assert.Empty(error.ToString());
        var text = output.ToString().ReplaceLineEndings("\n");
        if(readError != null)
        {
            var notice = empty ? "The history of reviews could not be read (access denied)." :
                "The history of reviews could not be read in full (read interrupted), so the numbers below may leave reviews out.";
            Assert.StartsWith(notice + "\n", text, StringComparison.Ordinal);
        }

        if(empty)
        {
            if(readError == null)
                Assert.Equal(StatsReport.EmptyHeading + "\n" + StatsReport.EmptySentence + "\n", text);
            else
                Assert.DoesNotContain(StatsReport.EmptyHeading, text, StringComparison.Ordinal);

            return;
        }

        Assert.Contains("5 reviews in 30 days\nFrom the hook: 5 reviews, 1 blocked.\n1 blocked, 1 incomplete, 3 allowed\n12 s typical, 48 s slowest", text, StringComparison.Ordinal);
        Assert.Contains("Today, 24 Nov: 4 reviews, 1 blocked, 1 incomplete, 2 allowed", text, StringComparison.Ordinal);
        Assert.Contains("Most broken\n4 rules had findings", text, StringComparison.Ordinal);
        Assert.Contains("REMOVED01: 2", text, StringComparison.Ordinal);
        Assert.Contains("ALLOW001 An exemption needs a reason (Blocks): 1", text, StringComparison.Ordinal);
        Assert.Contains("ARCH001 A use breaks a direction (Blocks): 1", text, StringComparison.Ordinal);
        Assert.Contains("4 of the 5 rules in force found nothing in 30 days.", text, StringComparison.Ordinal);
        Assert.Contains("  IDE0005 ", text, StringComparison.Ordinal);
        if(showAll)
        {
            Assert.Contains("  IDE0055 ", text, StringComparison.Ordinal);
            Assert.DoesNotContain("more: commitfence stats --all", text, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("  IDE0055 ", text, StringComparison.Ordinal);
            Assert.Contains("and 1 more: commitfence stats --all", text, StringComparison.Ordinal);
        }

        Assert.Contains("Over 20 s: 1 - The slowest: 48 s, Tue 24 Nov, 00:30, 4 files", text, StringComparison.Ordinal);
    }

    private static StatsService Service(string folder, bool empty, string? readError = null)
    {
        File.WriteAllText(Path.Combine(folder, _settings), ReviewSources.Settings(ReviewSources.Team010, RulesFixture.MethodRule));
        File.WriteAllText(Path.Combine(folder, ".editorconfig"), "root = true\n\n[*.cs]\n" +
            "dotnet_diagnostic.IDE0055.severity = warning\ndotnet_diagnostic.CA1822.severity = warning\ndotnet_diagnostic.IDE0005.severity = warning\n");
        var files = new SystemFileSystem();
        var data = new FixedDataFolder(Path.Combine(folder, "data"));
        var languages = new List<ILanguage> { new CSharpLanguage(new ProcessRunner(), files, data) };
        var memory = new MemoryHistory { ReadError = readError };
        if(!empty)
        {
            memory.Records.AddRange([
                Entry(_today.AddDays(-30), RecordedReviewOutcome.Blocked, 60000, true, new RecordedRuleCount("TEAM011", 99)),
                Entry(_today.AddHours(-1), RecordedReviewOutcome.Warned, 8000, true, new RecordedRuleCount("TEAM010", 2)),
                Entry(_today, RecordedReviewOutcome.Blocked, 48000, true, new RecordedRuleCount("TEAM010", 3),
                    new RecordedRuleCount("ALLOW001", 1), new RecordedRuleCount("ARCH001", 1)),
                Entry(_today, RecordedReviewOutcome.Incomplete, 12000, true, new RecordedRuleCount("REMOVED01", 2)),
                Entry(_today, RecordedReviewOutcome.Clean, 9000, false),
                Entry(_today, RecordedReviewOutcome.Clean, 3000, false)
            ]);
            memory.Unreadable = 2;
        }

        return new StatsService(new FakeGit(folder), new FixedClock(_today), memory, new RuleService(files, new FileSettingsLock(data), languages));
    }

    private static ReviewScope PreparePush(ReviewHarness harness, bool blocked)
    {
        var settings = ReviewSources.Settings(ReviewSources.Team010);
        var before = ReviewSources.Class("Core", "public int Value() => 4;");
        var implementation = blocked ? "public int Value(int x)\n{\n    if(x > 0)\n    {\n        return 1;\n    }\n    return 0;\n}" : "public int Value() => 5;";
        var after = ReviewSources.Class("Core", implementation);
        harness.Fake.AddCommit("before", new Dictionary<string, string> { [_settings] = settings, [_file] = before });
        harness.Fake.AddCommit("after", new Dictionary<string, string> { [_settings] = settings, [_file] = after }, "before");
        harness.Fake.Name("HEAD", "after");
        File.WriteAllText(Path.Combine(harness.RepositoryFolder, _settings), settings);
        File.WriteAllText(Path.Combine(harness.RepositoryFolder, _file), after);
        var pushed = new PushedRef("refs/heads/main", FakeGit.IdOf("after"), "refs/heads/main", FakeGit.IdOf("before"));
        return ReviewScope.ForPush("origin", "https://example.invalid/origin", PushedRefs.FromRefs([pushed]));
    }

    private static ReviewRecord Entry(DateTimeOffset time, RecordedReviewOutcome outcome, long duration, bool full, params RecordedRuleCount[] findings)
    {
        return new ReviewRecord(ReviewRecord.CurrentFormat, time.ToUniversalTime(), ReviewSource.Hook, outcome, duration, full, 4, findings);
    }

    private static string Print(ReviewResult result)
    {
        using var writer = new StringWriter();
        new PlainTextReporter(writer, countByRule: false).Write(result);
        return writer.ToString();
    }

    private sealed class MemoryHistory : IReviewHistory
    {
        public bool Fail { get; init; }
        public string? ReadError { get; init; }
        public List<ReviewRecord> Records { get; } = new List<ReviewRecord>();
        public int Unreadable { get; set; }
        public int Appends { get; private set; }

        public ReviewHistoryRead Read(RepositoryLocation repository)
        {
            return new ReviewHistoryRead(Records, Unreadable, ReadError);
        }

        public void Append(RepositoryLocation repository, ReviewRecord record)
        {
            Appends++;
            if(Fail)
                throw new IOException("History is not writable.");

            Records.Add(record);
        }
    }
}
