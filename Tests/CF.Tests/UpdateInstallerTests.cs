using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CF.Adapters.Updates;
using CF.Core.Updates;
using Xunit;

namespace CF.Tests;

/// <summary>
/// Installing a release: the checksum and the signature, the unpacking of both kinds of download, the swap of the files and
/// putting them back when it fails, Cancel, the lock against a second update, and the clean-up at the next start.
/// </summary>
public sealed class UpdateInstallerTests
{
    private static readonly string _appName = ReleaseFixtures.Program("commitfenceapp");
    private static readonly string _cliName = ReleaseFixtures.Program("commitfence");

    [Fact]
    public void TheChecksumIsFoundBySha256sumsFileName()
    {
        var hash = new string('a', 64);
        var other = new string('b', 64);
        var sums = $"{other}  commitfence-cli-win-x64.zip\n{hash}  CommitFence-win-x64.zip\n{other} *CommitFence-linux-x64.tar.gz\n";

        Assert.Equal(hash, UpdateInstaller.FindHash(sums, "CommitFence-win-x64.zip"));
        Assert.Equal(other, UpdateInstaller.FindHash(sums, "CommitFence-linux-x64.tar.gz"));
        Assert.Null(UpdateInstaller.FindHash(sums, "CommitFence-osx-arm64.zip"));
        Assert.Null(UpdateInstaller.FindHash("short  CommitFence-win-x64.zip", "CommitFence-win-x64.zip"));
    }

    [Fact]
    public async Task TheFullDownloadReplacesTheAppTheCommandLineAndTheAnalyzersAndLeavesOthersAlone()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, null);

            await ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync();

            Assert.Equal("new app", File.ReadAllText(Path.Combine(install.Folder, _appName)));
            Assert.Equal("new cli", File.ReadAllText(Path.Combine(install.Folder, _cliName)));
            Assert.Equal("new analyzer", File.ReadAllText(Path.Combine(install.Folder, "analyzers", "CF.Meaning.dll")));
            Assert.True(File.Exists(Path.Combine(install.Folder, "licenses", "New.txt")));
            Assert.False(File.Exists(Path.Combine(install.Folder, "licenses", "Old.txt")));
            Assert.Equal("mine", File.ReadAllText(Path.Combine(install.Folder, "notes.txt")));
            Assert.Equal("old app", File.ReadAllText(Path.Combine(install.Folder, _appName + UpdateInstaller.OldSuffix)));
            Assert.Equal("old cli", File.ReadAllText(Path.Combine(install.Folder, _cliName + UpdateInstaller.OldSuffix)));
            Assert.False(Directory.Exists(Path.Combine(install.Folder, UpdateInstaller.StagingFolderName)));

            // the new version deletes what the old one left
            Assert.True(UpdateInstaller.TryCleanUp(install.Folder));
            Assert.Equal(new List<string> { _appName, _cliName, "LICENSE", "THIRD-PARTY-NOTICES.md", "analyzers", ProgramLockFile.FileName, "licenses", "notes.txt" }.Order(StringComparer.Ordinal).ToList(), ReleaseFixtures.ListFolder(install.Folder));
        }
    }

    [Fact]
    public async Task TheCommandLineDownloadReplacesTheCommandLineAndTheAnalyzersOnly()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.CommandLine);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "commitfence-cli-test.zip", PackageKind.CommandLine, null);

            await ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync();

            Assert.Equal("new cli", File.ReadAllText(Path.Combine(install.Folder, _cliName)));
            Assert.Equal("new analyzer", File.ReadAllText(Path.Combine(install.Folder, "analyzers", "CF.Meaning.dll")));
            Assert.False(File.Exists(Path.Combine(install.Folder, _appName)));
            Assert.Equal("mine", File.ReadAllText(Path.Combine(install.Folder, "notes.txt")));
        }
    }

    [Fact]
    public async Task ATarGzDownloadIsUnpackedToo()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.tar.gz", PackageKind.App, null);

            await ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync();

            Assert.Equal("new app", File.ReadAllText(Path.Combine(install.Folder, _appName)));
            Assert.Equal("new cli", File.ReadAllText(Path.Combine(install.Folder, _cliName)));
            Assert.Equal("new analyzer", File.ReadAllText(Path.Combine(install.Folder, "analyzers", "CF.Meaning.dll")));
        }
    }

    [Fact]
    public async Task ADamagedDownloadChangesNothing()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, new string('0', 64));
            var before = ReleaseFixtures.ListFolder(install.Folder);

            var error = await Assert.ThrowsAsync<UpdateException>(ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync);

            Assert.Contains("damaged", error.Message, StringComparison.Ordinal);
            Assert.Equal(before, ReleaseFixtures.ListFolder(install.Folder));
            Assert.Equal("old app", File.ReadAllText(Path.Combine(install.Folder, _appName)));
        }
    }

    [Fact]
    public async Task AZipChangedAfterItWasSignedIsRefusedAndTheProgramStillWorks()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, null);
            var bytes = feed.Read("CommitFence-test.zip");
            bytes[bytes.Length / 2] ^= 0xFF;
            feed.Write("CommitFence-test.zip", bytes);
            var before = ReleaseFixtures.ListFolder(install.Folder);

            var error = await Assert.ThrowsAsync<UpdateException>(ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync);

            Assert.Contains("damaged", error.Message, StringComparison.Ordinal);
            Assert.Equal(before, ReleaseFixtures.ListFolder(install.Folder));
            Assert.Equal("old cli", File.ReadAllText(Path.Combine(install.Folder, _cliName)));
        }
    }

    [Fact]
    public async Task AReleaseNotSignedWithTheTrustedKeyChangesNothing()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, null);
            var before = ReleaseFixtures.ListFolder(install.Folder);

            // signed, but with another key: what a release made by someone else looks like
            using(var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256))
            {
                var installer = ReleaseFixtures.CreateInstaller(feed, release, install, null, null);
                installer.TrustedKey = otherKey.ExportSubjectPublicKeyInfoPem();
                var error = await Assert.ThrowsAsync<UpdateException>(installer.RunAsync);
                Assert.Contains("not signed by CommitFence", error.Message, StringComparison.Ordinal);
            }

            // the checksums changed after they were signed (a package swapped for another, with its hash)
            var signedChecksums = feed.Read("SHA256SUMS.txt");
            feed.Write("SHA256SUMS.txt", Encoding.UTF8.GetString(signedChecksums).Replace("cccc", "dddd", StringComparison.Ordinal));
            var tampered = await Assert.ThrowsAsync<UpdateException>(ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync);
            Assert.Contains("not signed by CommitFence", tampered.Message, StringComparison.Ordinal);

            // no signature at all
            feed.Write("SHA256SUMS.txt", signedChecksums);
            feed.Write("SHA256SUMS.txt.sig", Array.Empty<byte>());
            var unsigned = await Assert.ThrowsAsync<UpdateException>(ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync);
            Assert.Contains("not signed by CommitFence", unsigned.Message, StringComparison.Ordinal);

            Assert.Equal(before, ReleaseFixtures.ListFolder(install.Folder));
            Assert.Equal("old app", File.ReadAllText(Path.Combine(install.Folder, _appName)));

            // nothing past the checksums was downloaded
            Assert.DoesNotContain(release.PackageUri?.ToString(), feed.Requests);
        }
    }

    [Fact]
    public void SignaturesMadeByOpensslAreRead()
    {
        // the release workflow signs with "openssl dgst -sha256 -sign"; this one was made that way, with a throwaway key
        const string publicKey = "-----BEGIN PUBLIC KEY-----\n" +
            "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEKy5uyjzLcy+rwR0MA6mLiSWhxPpM\n" +
            "P590gTIWh+UEGCx4eP2pChuEpiUNLxd/6Gexb5KkByWek/qaB8xvmfd/sQ==\n" +
            "-----END PUBLIC KEY-----\n";
        var signature = Convert.FromBase64String("MEQCICyAAwCcCFPfm0ZjSCWBVxJsxO/SvoXlfHrrusV+RpIWAiBNjCQzUar/9vSHawkDWw4RI3gZCJ4RFWNQomCEKXV3Bw==");
        var checksums = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef  CommitFence-win-x64.zip\n");

        Assert.True(UpdateSignature.IsValid(checksums, signature, publicKey));

        var changed = (byte[])checksums.Clone();
        changed[0] = (byte)'1';
        Assert.False(UpdateSignature.IsValid(changed, signature, publicKey));
        Assert.False(UpdateSignature.IsValid(checksums, Encoding.UTF8.GetBytes("not a signature"), publicKey));
        Assert.False(UpdateSignature.IsValid(checksums, null, publicKey));
    }

    [Fact]
    public void TheReleaseKeyIsBuiltIntoTheProgram()
    {
        var releaseKey = UpdateSignature.ReleaseKey;
        using(var key = ECDsa.Create())
        {
            key.ImportFromPem(releaseKey);
            Assert.Equal(256, key.KeySize);
        }

        // the program trusts the release key unless a test says otherwise
        var installer = new UpdateInstaller(new HttpClient(), new ReleaseInfo { Version = new Version(1, 0) }, new InstallLocation(), new Version(1, 0), null, null);
        Assert.Equal(releaseKey, installer.TrustedKey);

        // and a release signed with the test key is not one of ours
        Assert.False(UpdateSignature.IsValid(Encoding.UTF8.GetBytes("x"), TestSigning.Sign(Encoding.UTF8.GetBytes("x")), releaseKey));
    }

    [Fact]
    public async Task AConnectionLostDuringTheDownloadChangesNothing()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, null);
            feed.DropWhileReading = release.PackageUri?.ToString();
            var before = ReleaseFixtures.ListFolder(install.Folder);

            var error = await Assert.ThrowsAsync<UpdateException>(ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync);

            Assert.StartsWith("The download failed", error.Message, StringComparison.Ordinal);
            Assert.EndsWith("Nothing was changed.", error.Message, StringComparison.Ordinal);
            Assert.Equal(before, ReleaseFixtures.ListFolder(install.Folder));
        }
    }

    [Fact]
    public async Task ADownloadWithoutTheProgramChangesNothing()
    {
        using(var folder = new TestFolder())
        {
            var located = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var install = new InstallLocation { Folder = located.Folder, Executable = Path.Combine(located.Folder, "Renamed.exe"), Kind = PackageKind.App };
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, null);
            var before = ReleaseFixtures.ListFolder(install.Folder);

            var error = await Assert.ThrowsAsync<UpdateException>(ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync);

            Assert.Contains("Renamed.exe", error.Message, StringComparison.Ordinal);
            Assert.Equal(before, ReleaseFixtures.ListFolder(install.Folder));
        }
    }

    [Fact]
    public async Task AnUpdateInProgressIsLeftAloneByAnotherCopyOfTheProgram()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var staging = Path.Combine(install.Folder, UpdateInstaller.StagingFolderName);
            Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(staging, "CommitFence-test.zip"), "half a download");
            File.WriteAllText(Path.Combine(install.Folder, _appName + UpdateInstaller.OldSuffix), "left over");
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, null);

            // the first copy is updating: it holds the lock
            using(new FileStream(Path.Combine(staging, UpdateInstaller.LockFileName), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Assert.True(UpdateInstaller.IsStagingInUse(staging));

                // a second copy starts: its clean-up leaves everything alone, the old files are what the first would put back
                Assert.True(UpdateInstaller.TryCleanUp(install.Folder));
                Assert.True(File.Exists(Path.Combine(staging, "CommitFence-test.zip")));
                Assert.True(File.Exists(Path.Combine(install.Folder, _appName + UpdateInstaller.OldSuffix)));

                // and its own update stops without touching it
                var error = await Assert.ThrowsAsync<UpdateException>(ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync);
                Assert.StartsWith("Another copy of the program is updating", error.Message, StringComparison.Ordinal);
                Assert.True(File.Exists(Path.Combine(staging, "CommitFence-test.zip")));
                Assert.Empty(feed.Requests);
            }

            // once the first copy is done (or gone), what it left is cleaned up
            Assert.False(UpdateInstaller.IsStagingInUse(staging));
            Assert.True(UpdateInstaller.TryCleanUp(install.Folder));
            Assert.False(Directory.Exists(staging));
            Assert.False(File.Exists(Path.Combine(install.Folder, _appName + UpdateInstaller.OldSuffix)));
        }
    }

    [Fact]
    public void CancelAndReplacingTheFilesCannotBothWin()
    {
        using(var cancelledFirst = new UpdateCancellation())
        {
            Assert.True(cancelledFirst.TryCancel());
            Assert.True(cancelledFirst.Token.IsCancellationRequested);
            Assert.False(cancelledFirst.TryBeginReplacing());
            Assert.True(cancelledFirst.IsCancelled);
        }

        using(var replacingFirst = new UpdateCancellation())
        {
            Assert.True(replacingFirst.TryBeginReplacing());
            Assert.False(replacingFirst.TryCancel());
            Assert.False(replacingFirst.Token.IsCancellationRequested);
            Assert.False(replacingFirst.IsCancelled);
        }
    }

    [Fact]
    public async Task ACancelledUpdateStopsBeforeReplacingAnything()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, null);
            var before = ReleaseFixtures.ListFolder(install.Folder);
            var steps = new RecordingProgress();

            using(var cancellation = new UpdateCancellation())
            {
                Assert.True(cancellation.TryCancel());
                await Assert.ThrowsAnyAsync<OperationCanceledException>(ReleaseFixtures.CreateInstaller(feed, release, install, steps, cancellation).RunAsync);
            }

            Assert.Equal(before, ReleaseFixtures.ListFolder(install.Folder));
            Assert.DoesNotContain(UpdateStage.Replacing, steps.Stages);
        }
    }

    [Fact]
    public async Task ReplacingTheFilesIsReportedOnceCancelNoLongerWorks()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, null);
            var steps = new RecordingProgress();

            using(var cancellation = new UpdateCancellation())
            {
                await ReleaseFixtures.CreateInstaller(feed, release, install, steps, cancellation).RunAsync();

                Assert.False(cancellation.TryCancel());
            }

            Assert.Equal(UpdateStage.Replacing, steps.Stages[steps.Stages.Count - 1]);
            Assert.Equal(UpdateStage.Downloading, steps.Stages[0]);
            Assert.Equal("new app", File.ReadAllText(Path.Combine(install.Folder, _appName)));
        }
    }

    [Fact]
    public async Task WhenAFileCannotBeReplacedTheOthersArePutBack()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, null);

            using(BlockReplacingTheLicense(install.Folder))
            {
                var before = ReleaseFixtures.ListFolder(install.Folder);
                var error = await Assert.ThrowsAsync<UpdateException>(ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync);
                Assert.Contains("left as it was", error.Message, StringComparison.Ordinal);

                // the lock file is the one thing the update leaves beside the program
                Assert.Equal(before.Append(ProgramLockFile.FileName).Order(StringComparer.Ordinal).ToList(), ReleaseFixtures.ListFolder(install.Folder));
            }

            Assert.Equal("old app", File.ReadAllText(Path.Combine(install.Folder, _appName)));
            Assert.Equal("old cli", File.ReadAllText(Path.Combine(install.Folder, _cliName)));
            Assert.Equal("old analyzer", File.ReadAllText(Path.Combine(install.Folder, "analyzers", "CF.Meaning.dll")));
            Assert.True(File.Exists(Path.Combine(install.Folder, "licenses", "Old.txt")));
        }
    }

    [Fact]
    public async Task ARunningProgramIsReplacedByRenamingIt()
    {
        // what makes the update work on Windows: a running program cannot be overwritten or deleted, but it can be
        // renamed. Linux and macOS rename (and delete) a running program too
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.CommandLine);
            var program = Path.Combine(install.Folder, _cliName);
            var start = StartSomethingLong(program);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "commitfence-cli-test.zip", PackageKind.CommandLine, null);

            using(var running = Process.Start(start) ?? throw new InvalidOperationException("the program did not start"))
            {
                try
                {
                    if(OperatingSystem.IsWindows())
                    {
                        var deleteError = Record.Exception(() => File.Delete(program));
                        Assert.True(deleteError is IOException || deleteError is UnauthorizedAccessException);
                    }

                    await ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync();

                    Assert.Equal("new cli", File.ReadAllText(program));
                    Assert.False(running.HasExited);

                    // on Windows the old program cannot be deleted while it runs: the clean-up leaves it for the next start
                    Assert.Equal(!OperatingSystem.IsWindows(), UpdateInstaller.TryCleanUp(install.Folder));
                }
                finally
                {
                    running.Kill(true);
                    await running.WaitForExitAsync(TestContext.Current.CancellationToken);
                }
            }

            // Windows (or a virus scanner) can hold the program a moment after it exits: the clean-up tries again
            await UpdateInstaller.CleanUpAsync(install.Folder, 10);
            Assert.False(File.Exists(program + UpdateInstaller.OldSuffix));
        }
    }

    /// <summary>
    /// Copies a system program that waits half a minute to <paramref name="program" />: cmd.exe on Windows (pinging
    /// itself), sleep on Linux and macOS.
    /// </summary>
    private static ProcessStartInfo StartSomethingLong(string program)
    {
        if(OperatingSystem.IsWindows())
        {
            File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), program, true);
            return new ProcessStartInfo(program, "/c ping -n 30 127.0.0.1 >nul") { UseShellExecute = false, CreateNoWindow = true };
        }

        File.Copy("/bin/sleep", program, true);
        File.SetUnixFileMode(program, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return new ProcessStartInfo(program, "30") { UseShellExecute = false, CreateNoWindow = true };
    }

    /// <summary>
    /// Makes replacing LICENSE fail: Windows refuses to rename a file open without sharing; on Linux and macOS a left-over
    /// "LICENSE.cf-old" folder that cannot be emptied (it is read-only), which the swap deletes first.
    /// </summary>
    private static IDisposable BlockReplacingTheLicense(string folder)
    {
        if(OperatingSystem.IsWindows())
            return new FileStream(Path.Combine(folder, "LICENSE"), FileMode.Open, FileAccess.Read, FileShare.None);

        return new ReadOnlyFolder(Path.Combine(folder, "LICENSE" + UpdateInstaller.OldSuffix));
    }
}