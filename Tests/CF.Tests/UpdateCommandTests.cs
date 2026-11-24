using System.Security.Cryptography;
using CF.Adapters.Updates;
using CF.Cli.Commands;
using CF.Core.Review;
using CF.Core.Updates;
using Xunit;

namespace CF.Tests;

/// <summary>
/// "commitfence update" and "commitfence update --check", the one line that says a newer version exists (where it shows, once a day,
/// and the places it never does), and the whole update against a feed in a folder.
/// </summary>
public sealed class UpdateCommandTests
{
    private static readonly DateTimeOffset _now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly StringWriter _output = new StringWriter();
    private readonly StringWriter _error = new StringWriter();
    private readonly FakeUpdater _updater = new FakeUpdater();
    private readonly MemoryCheckRecord _record = new MemoryCheckRecord();
    private readonly FixedClock _clock = new FixedClock(_now);

    [Theory]
    [InlineData("check, newer version")]
    [InlineData("check, up to date")]
    [InlineData("check, cannot be answered")]
    [InlineData("update, up to date")]
    [InlineData("update, fails")]
    [InlineData("update, copy cannot update itself")]
    [InlineData("update, unknown option")]
    public void CommandHonorsCheckOnlyAndInstallOutcome(string row)
    {
        var args = row.StartsWith("check", StringComparison.Ordinal) ? new[] { "update", "--check" } : new[] { "update" };
        if(row is "check, newer version" or "update, fails" or "update, copy cannot update itself")
            _updater.Result = FakeUpdater.Available("9.0.0.0");
        else if(row == "check, cannot be answered")
            _updater.Result = new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Problem = "GitHub could not be reached (No such host is known.)." };
        else if(row == "update, unknown option")
            args = new[] { "update", "--now" };

        if(row == "update, fails")
            _updater.InstallFailure = new UpdateException("The download of version 9.0.0.0 is damaged. Nothing was changed.");
        else if(row == "update, copy cannot update itself")
            _updater.Problem = "This copy of the program is not a release download, so it cannot update itself.";

        var exitCode = CreateRunner().Run(args);

        // a check never installs; nothing installs unless there is something newer and this copy can take it
        Assert.Equal(row == "update, fails" ? 1 : 0, _updater.Installs);
        switch(row)
        {
            case "check, newer version":
                Assert.Equal(ExitCode.Passed, exitCode);
                Assert.Contains("9.0.0.0", _output.ToString(), StringComparison.Ordinal);
                Assert.Contains("commitfence update", _output.ToString(), StringComparison.Ordinal);
                Assert.Equal(string.Empty, _error.ToString());
                break;
            case "check, up to date":
            case "update, up to date":
                Assert.Equal(ExitCode.Passed, exitCode);
                Assert.Contains("up to date", _output.ToString(), StringComparison.Ordinal);
                break;
            case "check, cannot be answered":
                Assert.Equal(ExitCode.Blocked, exitCode);
                Assert.Contains("GitHub could not be reached", _error.ToString(), StringComparison.Ordinal);
                Assert.Equal(string.Empty, _output.ToString());
                break;
            case "update, fails":
                Assert.Equal(ExitCode.Blocked, exitCode);
                Assert.Contains("is damaged. Nothing was changed.", _error.ToString(), StringComparison.Ordinal);
                break;
            case "update, copy cannot update itself":
                Assert.Equal(ExitCode.Blocked, exitCode);
                Assert.Contains("not a release download", _error.ToString(), StringComparison.Ordinal);
                Assert.Contains("releases/tag/v9.0.0.0", _error.ToString(), StringComparison.Ordinal);
                break;
            default:
                Assert.Equal(ExitCode.BadSettingsOrUsage, exitCode);
                Assert.Equal(0, _updater.Checks);
                Assert.Contains("commitfence update [--check]", _error.ToString(), StringComparison.Ordinal);
                break;
        }
    }

    [Theory]
    [InlineData("a hook")]
    [InlineData("output that is not a terminal")]
    [InlineData("a pipeline")]
    [InlineData("the update command")]
    [InlineData("a terminal")]
    public void PassiveNoticeObservesTerminalAndSchedule(string context)
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        if(context != "a terminal")
        {
            // the places the program never asks the network in: a hook, a pipe or file, CI, and the command that asks by itself
            var args = context switch
            {
                "a hook" => new[] { "review", "--hook", "pre-push", "origin", "https://example.com/r.git" },
                "the update command" => new[] { "update", "--check" },
                _ => new[] { "catalog" }
            };

            CreateNotice(isTerminal: context != "output that is not a terminal", inCi: context == "a pipeline").ShowIfDue(args);

            Assert.Equal(0, _updater.Checks);
            Assert.Equal(string.Empty, _error.ToString());
            Assert.Equal(string.Empty, _output.ToString());
            Assert.Null(_record.LastCheckUtc);
            return;
        }

        var notice = CreateNotice();
        notice.ShowIfDue(new[] { "catalog" });

        // at a terminal a newer release is one line on the error stream, after the command's own output
        Assert.Contains("9.0.0.0 is available", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, _output.ToString());
        Assert.Equal(_now.UtcDateTime, _record.LastCheckUtc);

        // the same day nothing more is said and nothing more is asked; the next day it is asked again
        notice.ShowIfDue(new[] { "catalog" });
        Assert.Equal(1, _updater.Checks);
        Assert.Single(_error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        _clock.Now = _now.AddHours(25);
        notice.ShowIfDue(new[] { "--version" });
        Assert.Equal(2, _updater.Checks);
        Assert.Equal(2, _error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);

        // a check that fails is silent, and still counts as the day's check: offline, it is not asked again at every command
        var quiet = new StringWriter();
        var failing = new FakeUpdater { Result = new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Problem = "GitHub answered 403 (Forbidden)." } };
        var failingRecord = new MemoryCheckRecord();
        var failingNotice = new UpdateNotice(failing, failingRecord, _clock, new CommandEnvironment("/work", "/program", "/program/commitfence", TextReader.Null, _output, quiet, false, true, false));
        failingNotice.ShowIfDue(new[] { "catalog" });
        failingNotice.ShowIfDue(new[] { "catalog" });
        Assert.Equal(string.Empty, quiet.ToString());
        Assert.Equal(_clock.Now.UtcDateTime, failingRecord.LastCheckUtc);
        Assert.Equal(1, failing.Checks);

        // a check is due when nothing was recorded, and after a day or when the clock went back
        var due = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(UpdateSchedule.IsCheckDue(null, due));
        Assert.False(UpdateSchedule.IsCheckDue(due.AddHours(-23), due));
        Assert.True(UpdateSchedule.IsCheckDue(due.AddHours(-24), due));
        Assert.True(UpdateSchedule.IsCheckDue(due.AddHours(5), due));
    }

    [Fact]
    public void ReminderRecordSurvivesRestartPerProgram()
    {
        using(var folder = new TestFolder())
        {
            var dataFolder = new FixedDataFolder(folder.Path);
            Assert.Null(new FileUpdateCheckRecord(dataFolder, "commitfence").LastCheckUtc);

            new FileUpdateCheckRecord(dataFolder, "commitfence").Save(_now.UtcDateTime);

            // a new record, as after a restart, reads the time back; the other program has its own
            Assert.Equal(_now.UtcDateTime, new FileUpdateCheckRecord(dataFolder, "commitfence").LastCheckUtc);
            Assert.Null(new FileUpdateCheckRecord(dataFolder, "commitfenceapp").LastCheckUtc);

            // a record that cannot be read is no record: the check may be made
            File.WriteAllText(Path.Combine(folder.Path, "update-check-commitfence"), "not a time");
            Assert.Null(new FileUpdateCheckRecord(dataFolder, "commitfence").LastCheckUtc);
        }
    }

    [Theory]
    [InlineData(true, "CommitFence-test.zip")]
    [InlineData(false, "commitfence-cli-test.tar.gz")]
    public async Task SignedPackageUpdatesOnlyOwnedFiles(bool fullDownload, string packageName)
    {
        var kind = fullDownload ? PackageKind.App : PackageKind.CommandLine;
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), kind);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            ReleaseFixtures.PublishPackage(feed, packageName, kind, null);
            var runner = CreateRunner(new FeedUpdater(feed, packageName, install));
            var before = ReleaseFixtures.Snapshot(install.Folder);

            // --check asks the feed for its latest release and changes nothing
            Assert.Equal(ExitCode.Passed, await Task.Run(() => runner.Run(new[] { "update", "--check" })));
            Assert.Contains("9.0.0.0", _output.ToString(), StringComparison.Ordinal);
            Assert.Equal(before, ReleaseFixtures.Snapshot(install.Folder));
            Assert.Single(feed.Requests);

            Assert.Equal(ExitCode.Passed, await Task.Run(() => runner.Run(new[] { "update" })));

            Assert.Contains("is now version 9.0.0.0", _output.ToString(), StringComparison.Ordinal);
            Assert.Equal("new cli", File.ReadAllText(Path.Combine(install.Folder, ReleaseFixtures.Program("commitfence"))));
            Assert.Equal("new analyzer", File.ReadAllText(Path.Combine(install.Folder, "analyzers", "CF.Meaning.dll")));
            Assert.Equal("new license", File.ReadAllText(Path.Combine(install.Folder, "LICENSE")));
            Assert.True(File.Exists(Path.Combine(install.Folder, "licenses", "New.txt")));
            Assert.False(File.Exists(Path.Combine(install.Folder, "licenses", "Old.txt")));

            // what is not the program's is left alone, and the app is there only for the download that has it
            Assert.Equal("mine", File.ReadAllText(Path.Combine(install.Folder, "notes.txt")));
            var appPath = Path.Combine(install.Folder, ReleaseFixtures.Program("commitfenceapp"));
            if(kind == PackageKind.App)
                Assert.Equal("new app", File.ReadAllText(appPath));
            else
                Assert.False(File.Exists(appPath));

            // the download folder is gone, and the old files are what the next start deletes
            Assert.False(Directory.Exists(Path.Combine(install.Folder, UpdateInstaller.StagingFolderName)));
            Assert.True(UpdateInstaller.TryCleanUp(install.Folder));
            Assert.DoesNotContain(ReleaseFixtures.ListFolder(install.Folder), name => name.EndsWith(UpdateInstaller.OldSuffix, StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("a package changed after it was signed", "damaged")]
    [InlineData("another key signed it", "not signed by CommitFence")]
    [InlineData("the checksums changed after they were signed", "not signed by CommitFence")]
    [InlineData("there is no signature", "not signed by CommitFence")]
    [InlineData("the connection is lost", "The download failed")]
    [InlineData("the package has no program", "Renamed.exe")]
    public async Task InvalidReleaseCannotChangeInstallation(string row, string said)
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            const string packageName = "CommitFence-test.zip";
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, packageName, PackageKind.App, null);
            string? trustedKey = null;
            switch(row)
            {
                case "a package changed after it was signed":
                    var bytes = feed.Read(packageName);
                    bytes[bytes.Length / 2] ^= 0xFF;
                    feed.Write(packageName, bytes);
                    break;
                case "another key signed it":
                    using(var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256))
                    {
                        trustedKey = otherKey.ExportSubjectPublicKeyInfoPem();
                    }

                    break;
                case "the checksums changed after they were signed":
                    feed.Write("SHA256SUMS.txt", System.Text.Encoding.UTF8.GetString(feed.Read("SHA256SUMS.txt")).Replace("cccc", "dddd", StringComparison.Ordinal));
                    break;
                case "there is no signature":
                    feed.Write("SHA256SUMS.txt.sig", Array.Empty<byte>());
                    break;
                case "the connection is lost":
                    feed.DropWhileReading = release.PackageUri?.ToString();
                    break;
                default:
                    install = new InstallLocation { Folder = install.Folder, Executable = Path.Combine(install.Folder, "Renamed.exe"), Kind = PackageKind.App };
                    break;
            }

            var before = ReleaseFixtures.Snapshot(install.Folder);
            var runner = CreateRunner(new FeedUpdater(feed, packageName, install, trustedKey));

            var exitCode = await Task.Run(() => runner.Run(new[] { "update" }));

            Assert.Equal(ExitCode.Blocked, exitCode);
            Assert.Contains(said, _error.ToString(), StringComparison.Ordinal);
            Assert.Contains("Nothing was changed", _error.ToString(), StringComparison.Ordinal);
            Assert.Equal(before, ReleaseFixtures.Snapshot(install.Folder));

            // a release that is not signed by the key is refused before its package is fetched at all
            if(said == "not signed by CommitFence")
                Assert.DoesNotContain(release.PackageUri?.ToString(), feed.Requests);
        }
    }

    private CommandEnvironment CreateEnvironment(bool isTerminal = true, bool inCi = false)
    {
        return new CommandEnvironment("/work", "/program", "/program/commitfence", TextReader.Null, _output, _error, false, isTerminal, inCi);
    }

    private CommandRunner CreateRunner()
    {
        return CreateRunner(_updater);
    }

    private CommandRunner CreateRunner(CF.Core.Ports.IUpdater updater)
    {
        return new CommandRunner(null!, null!, null!, null!, updater, CreateEnvironment());
    }

    private UpdateNotice CreateNotice(bool isTerminal = true, bool inCi = false)
    {
        return new UpdateNotice(_updater, _record, _clock, CreateEnvironment(isTerminal, inCi));
    }
}
