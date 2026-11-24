using CF.Adapters.Updates;
using CF.Cli.Commands;
using CF.Core.Review;
using CF.Core.Updates;
using Xunit;

namespace CF.Tests;

/// <summary>
/// "commitfence update" and "commitfence update --check", and the one line that says a newer version exists: where it
/// shows, once a day, and the three places it never does (a hook run, output that is not a terminal, CI).
/// </summary>
public sealed class UpdateCommandTests
{
    private static readonly DateTimeOffset _now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly StringWriter _output = new StringWriter();
    private readonly StringWriter _error = new StringWriter();
    private readonly FakeUpdater _updater = new FakeUpdater();
    private readonly MemoryCheckRecord _record = new MemoryCheckRecord();
    private readonly FixedClock _clock = new FixedClock(_now);

    private CommandEnvironment CreateEnvironment(bool isTerminal = true, bool inCi = false)
    {
        return new CommandEnvironment("/work", "/program", "/program/commitfence", TextReader.Null, _output, _error, false, isTerminal, inCi);
    }

    private CommandRunner CreateRunner()
    {
        return new CommandRunner(null!, null!, null!, null!, _updater, CreateEnvironment());
    }

    private UpdateNotice CreateNotice(bool isTerminal = true, bool inCi = false)
    {
        return new UpdateNotice(_updater, _record, _clock, CreateEnvironment(isTerminal, inCi));
    }

    [Fact]
    public void UpdateCheckSaysThereIsANewerVersionAndChangesNothing()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");

        var exitCode = CreateRunner().Run(new[] { "update", "--check" });

        Assert.Equal(ExitCode.Passed, exitCode);
        Assert.Equal("CommitFence 9.0.0.0 is available (this is 1.0.0.0): run commitfence update" + Environment.NewLine, _output.ToString());
        Assert.Equal(0, _updater.Installs);
        Assert.Equal(string.Empty, _error.ToString());
    }

    [Fact]
    public void UpdateCheckSaysWhenThereIsNothingNewer()
    {
        var exitCode = CreateRunner().Run(new[] { "update", "--check" });

        Assert.Equal(ExitCode.Passed, exitCode);
        Assert.Equal("CommitFence 1.0.0.0 is already up to date." + Environment.NewLine, _output.ToString());
    }

    [Fact]
    public void UpdateCheckThatCannotBeAnsweredSaysWhyAndExitsOne()
    {
        _updater.Result = new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Problem = "GitHub could not be reached (No such host is known.)." };

        var exitCode = CreateRunner().Run(new[] { "update", "--check" });

        Assert.Equal(1, (int)exitCode);
        Assert.Contains("GitHub could not be reached", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, _output.ToString());
    }

    [Fact]
    public void UpdateInstallsTheNewestReleaseAndSaysWhatItDid()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");

        var exitCode = CreateRunner().Run(new[] { "update" });

        Assert.Equal(ExitCode.Passed, exitCode);
        Assert.Equal(1, _updater.Installs);
        Assert.Contains("Updating CommitFence from 1.0.0.0 to 9.0.0.0", _output.ToString(), StringComparison.Ordinal);
        Assert.Contains("CommitFence is now version 9.0.0.0.", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateWhenAlreadyUpToDateExitsZeroAndInstallsNothing()
    {
        var exitCode = CreateRunner().Run(new[] { "update" });

        Assert.Equal(ExitCode.Passed, exitCode);
        Assert.Equal(0, _updater.Installs);
        Assert.Contains("already up to date", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateThatFailsSaysWhyAndExitsOne()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        _updater.InstallFailure = new UpdateException("The download of version 9.0.0.0 is damaged. Nothing was changed.");

        var exitCode = CreateRunner().Run(new[] { "update" });

        Assert.Equal(1, (int)exitCode);
        Assert.Contains("is damaged. Nothing was changed.", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateWhereTheProgramCannotUpdateItselfNamesTheReasonAndThePage()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        _updater.Problem = "This copy of the program is not a release download, so it cannot update itself.";

        var exitCode = CreateRunner().Run(new[] { "update" });

        Assert.Equal(1, (int)exitCode);
        Assert.Equal(0, _updater.Installs);
        Assert.Contains("not a release download", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("releases/tag/v9.0.0.0", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateTakesNothingButCheck()
    {
        var exitCode = CreateRunner().Run(new[] { "update", "--now" });

        Assert.Equal(ExitCode.BadSettingsOrUsage, exitCode);
        Assert.Equal(0, _updater.Checks);
        Assert.Contains("commitfence update [--check]", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void HelpListsBothFormsOfUpdate()
    {
        Assert.Contains("  commitfence update [--check]", UsageText.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ANewerVersionIsMentionedOnceADayAtATerminalAfterTheCommand()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        var notice = CreateNotice();

        notice.ShowIfDue(new[] { "catalog" });

        Assert.Equal("CommitFence 9.0.0.0 is available: run commitfence update" + Environment.NewLine, _error.ToString());
        Assert.Equal(string.Empty, _output.ToString());
        Assert.Equal(_now.UtcDateTime, _record.LastCheckUtc);

        // the same day: no second line, no second request
        notice.ShowIfDue(new[] { "catalog" });
        Assert.Equal(1, _updater.Checks);
        Assert.Single(_error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

        // the next day: asked again
        _clock.Now = _now.AddHours(25);
        notice.ShowIfDue(new[] { "--version" });
        Assert.Equal(2, _updater.Checks);
        Assert.Equal(2, _error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void NothingIsSaidWhenThereIsNothingNewer()
    {
        CreateNotice().ShowIfDue(new[] { "catalog" });

        Assert.Equal(1, _updater.Checks);
        Assert.Equal(string.Empty, _error.ToString());
    }

    [Fact]
    public void AFailedOrRefusedCheckIsSilentAndTriedAgainTheNextDay()
    {
        _updater.Result = new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Problem = "GitHub answered 403 (Forbidden)." };
        var notice = CreateNotice();

        notice.ShowIfDue(new[] { "catalog" });
        notice.ShowIfDue(new[] { "catalog" });

        Assert.Equal(string.Empty, _error.ToString());
        Assert.Equal(1, _updater.Checks);

        _clock.Now = _now.AddHours(25);
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        notice.ShowIfDue(new[] { "catalog" });
        Assert.Equal(2, _updater.Checks);
        Assert.Contains("is available", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ARunOfAHookUsesNoNetworkAtAll()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");

        CreateNotice().ShowIfDue(new[] { "review", "--hook", "pre-push", "origin", "https://example.com/r.git" });

        Assert.Equal(0, _updater.Checks);
        Assert.Equal(string.Empty, _error.ToString());
        Assert.Null(_record.LastCheckUtc);
    }

    [Fact]
    public void OutputThatIsNotATerminalUsesNoNetworkAtAll()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");

        CreateNotice(isTerminal: false).ShowIfDue(new[] { "catalog" });

        Assert.Equal(0, _updater.Checks);
        Assert.Equal(string.Empty, _error.ToString());
        Assert.Null(_record.LastCheckUtc);
    }

    [Fact]
    public void WhenCiIsSetNoNetworkIsUsedEither()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");

        CreateNotice(inCi: true).ShowIfDue(new[] { "catalog" });

        Assert.Equal(0, _updater.Checks);
        Assert.Equal(string.Empty, _error.ToString());
        Assert.Null(_record.LastCheckUtc);
    }

    [Fact]
    public void TheUpdateCommandDoesNotAddTheLineToItsOwnAnswer()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");

        CreateNotice().ShowIfDue(new[] { "update", "--check" });

        Assert.Equal(0, _updater.Checks);
    }

    [Fact]
    public async Task TheWholeUpdateRunsAgainstAFeedInAFolder()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.CommandLine);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            ReleaseFixtures.PublishPackage(feed, ReleaseFixtures.RuntimePackageName(PackageKind.CommandLine), PackageKind.CommandLine, null);
            feed.Write("latest.json", ReleaseFixtures.ComposeReleaseJson(ReleaseFixtures.NewVersionTag, ReleaseFixtures.RuntimePackageName(PackageKind.CommandLine), true));
            var updater = ReleaseFixtures.CreateUpdater(feed, install);
            var runner = new CommandRunner(null!, null!, null!, null!, updater, CreateEnvironment());

            // --check changes nothing and downloads nothing
            var before = ReleaseFixtures.ListFolder(install.Folder);
            Assert.Equal(ExitCode.Passed, await Task.Run(() => runner.Run(new[] { "update", "--check" })));
            Assert.Equal(before, ReleaseFixtures.ListFolder(install.Folder));
            Assert.Single(feed.Requests);

            Assert.Equal(ExitCode.Passed, await Task.Run(() => runner.Run(new[] { "update" })));

            Assert.Equal("new cli", File.ReadAllText(Path.Combine(install.Folder, ReleaseFixtures.Program("commitfence"))));
            Assert.Equal("new analyzer", File.ReadAllText(Path.Combine(install.Folder, "analyzers", "CF.Meaning.dll")));
            Assert.Contains("CommitFence is now version 9.0.0.0.", _output.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AZipChangedAfterSigningIsRefusedByTheCommandAndTheProgramStillWorks()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.CommandLine);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var packageName = ReleaseFixtures.RuntimePackageName(PackageKind.CommandLine);
            ReleaseFixtures.PublishPackage(feed, packageName, PackageKind.CommandLine, null);
            var bytes = feed.Read(packageName);
            bytes[bytes.Length / 2] ^= 0xFF;
            feed.Write(packageName, bytes);
            var runner = new CommandRunner(null!, null!, null!, null!, ReleaseFixtures.CreateUpdater(feed, install), CreateEnvironment());

            var exitCode = await Task.Run(() => runner.Run(new[] { "update" }));

            Assert.Equal(1, (int)exitCode);
            Assert.Contains("Nothing was changed", _error.ToString(), StringComparison.Ordinal);
            Assert.Equal("old cli", File.ReadAllText(Path.Combine(install.Folder, ReleaseFixtures.Program("commitfence"))));
        }
    }

    [Fact]
    public void TheRecordOfTheLastCheckIsAFileInTheDataFolderOfEachProgram()
    {
        using(var folder = new TestFolder())
        {
            var dataFolder = new FixedDataFolder(folder.Path);
            var commandLine = new FileUpdateCheckRecord(dataFolder, "commitfence");
            var app = new FileUpdateCheckRecord(dataFolder, "commitfenceapp");
            Assert.Null(commandLine.LastCheckUtc);

            commandLine.Save(_now.UtcDateTime);

            Assert.Equal(_now.UtcDateTime, commandLine.LastCheckUtc);
            Assert.Null(app.LastCheckUtc);

            File.WriteAllText(Path.Combine(folder.Path, "update-check-commitfence"), "not a time");
            Assert.Null(commandLine.LastCheckUtc);
        }
    }

    /// <summary>
    /// A data folder that is the one the test gives.
    /// </summary>
    private sealed class FixedDataFolder : CF.Core.Ports.IDataFolder
    {
        public FixedDataFolder(string rootFolder)
        {
            RootFolder = rootFolder;
        }

        public string RootFolder { get; }
    }
}