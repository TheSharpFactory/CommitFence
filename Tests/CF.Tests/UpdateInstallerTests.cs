using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CF.Adapters.Updates;
using CF.Core.Updates;
using Xunit;

namespace CF.Tests;

/// <summary>
/// Installing a release: the signature that makes a download one of ours, the lock against a second update, Cancel and the point
/// after which it no longer works, the swap that puts every file back when it fails, and replacing a program that is running.
/// </summary>
public sealed class UpdateInstallerTests
{
    private static readonly string _appName = ReleaseFixtures.Program("commitfenceapp");
    private static readonly string _cliName = ReleaseFixtures.Program("commitfence");

    [Fact]
    public void WorkflowSignatureVerifiesWithRuntimeVerifier()
    {
        // the release workflow signs with "openssl dgst -sha256 -sign"; this signature was made that way, with a throwaway key
        const string publicKey = "-----BEGIN PUBLIC KEY-----\n" +
            "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEKy5uyjzLcy+rwR0MA6mLiSWhxPpM\n" +
            "P590gTIWh+UEGCx4eP2pChuEpiUNLxd/6Gexb5KkByWek/qaB8xvmfd/sQ==\n" +
            "-----END PUBLIC KEY-----\n";
        var signature = Convert.FromBase64String("MEQCICyAAwCcCFPfm0ZjSCWBVxJsxO/SvoXlfHrrusV+RpIWAiBNjCQzUar/9vSHawkDWw4RI3gZCJ4RFWNQomCEKXV3Bw==");
        var checksums = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef  CommitFence-win-x64.zip\n");

        Assert.True(UpdateSignature.IsValid(checksums, signature, publicKey));

        // the same signature does not hold for other data, for a signature that is not one, or for none
        var changed = (byte[])checksums.Clone();
        changed[0] = (byte)'1';
        Assert.False(UpdateSignature.IsValid(changed, signature, publicKey));
        Assert.False(UpdateSignature.IsValid(checksums, Encoding.UTF8.GetBytes("not a signature"), publicKey));
        Assert.False(UpdateSignature.IsValid(checksums, null, publicKey));
    }

    [Fact]
    public void DefaultInstallerUsesReleaseVerificationKey()
    {
        // an installer that was not given a key trusts the one built into the program, and that is the key CommitFence releases with
        var installer = new UpdateInstaller(new HttpClient(), new ReleaseInfo { Version = new Version(1, 0) }, new InstallLocation(), new Version(1, 0), null, null);
        using(var key = ECDsa.Create())
        {
            key.ImportFromPem(installer.TrustedKey);
            Assert.Equal(256, key.KeySize);
            Assert.Equal("MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEdAF38ZXsmQZFsN2qojcLUpeG+RYX2kKxAV7mX8iIBP1Ak74cR9CP9eKFpDwNTwdimBkzkLpGyGBRZ67Q3EYlDg==", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
        }

        // so a manifest signed with the test key is not one of ours
        var manifest = Encoding.UTF8.GetBytes("x");
        Assert.False(UpdateSignature.IsValid(manifest, TestSigning.Sign(manifest), installer.TrustedKey));
    }

    [Fact]
    public async Task ConcurrentUpdaterLeavesActiveStagingAlone()
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

            // the first copy is updating: it holds the lock of the download folder
            using(new FileStream(Path.Combine(staging, UpdateInstaller.LockFileName), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Assert.True(UpdateInstaller.IsStagingInUse(staging));
                var before = ReleaseFixtures.Snapshot(install.Folder);

                // a second copy starts: its clean-up leaves everything alone (the old files are what the first would put back) ...
                Assert.True(UpdateInstaller.TryCleanUp(install.Folder));
                Assert.Equal(before, ReleaseFixtures.Snapshot(install.Folder));

                // ... and its own update stops without touching anything or asking the feed
                var error = await Assert.ThrowsAsync<UpdateException>(ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync);
                Assert.StartsWith("Another copy of the program is updating", error.Message, StringComparison.Ordinal);
                Assert.Equal(before, ReleaseFixtures.Snapshot(install.Folder));
                Assert.Empty(feed.Requests);
            }

            // once the first copy is done, what it left is cleaned up
            Assert.False(UpdateInstaller.IsStagingInUse(staging));
            Assert.True(UpdateInstaller.TryCleanUp(install.Folder));
            Assert.False(Directory.Exists(staging));
            Assert.False(File.Exists(Path.Combine(install.Folder, _appName + UpdateInstaller.OldSuffix)));
        }
    }

    [Fact]
    public async Task CancellationEndsAtReplacementBoundary()
    {
        // whichever of Cancel and the replacing comes first wins, and the other is refused
        using(var cancelledFirst = new UpdateCancellation())
        {
            Assert.True(cancelledFirst.TryCancel());
            Assert.False(cancelledFirst.TryBeginReplacing());
            Assert.True(cancelledFirst.IsCancelled);
        }

        using(var replacingFirst = new UpdateCancellation())
        {
            Assert.True(replacingFirst.TryBeginReplacing());
            Assert.False(replacingFirst.TryCancel());
            Assert.False(replacingFirst.Token.IsCancellationRequested);
        }

        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, null);
            var before = ReleaseFixtures.Snapshot(install.Folder);

            // Cancel during the download: nothing is replaced
            var steps = new RecordingProgress();
            using(var cancellation = new UpdateCancellation())
            {
                var progress = new ReactingProgress(value =>
                {
                    steps.Report(value);
                    cancellation.TryCancel();
                });
                await Assert.ThrowsAnyAsync<OperationCanceledException>(ReleaseFixtures.CreateInstaller(feed, release, install, progress, cancellation).RunAsync);
            }

            Assert.DoesNotContain(UpdateStage.Replacing, steps.Stages);
            Assert.Equal(before, ReleaseFixtures.Snapshot(install.Folder));

            // Cancel pressed at the moment the files are reported as being replaced is refused, and the update goes through
            var refused = new List<bool>();
            using(var cancellation = new UpdateCancellation())
            {
                var progress = new ReactingProgress(value =>
                {
                    if(value.Stage == UpdateStage.Replacing)
                        refused.Add(!cancellation.TryCancel());
                });
                await ReleaseFixtures.CreateInstaller(feed, release, install, progress, cancellation).RunAsync();
            }

            Assert.Equal(new[] { true }, refused);
            Assert.Equal("new app", File.ReadAllText(Path.Combine(install.Folder, _appName)));
            Assert.Equal("new cli", File.ReadAllText(Path.Combine(install.Folder, _cliName)));
        }
    }

    [Fact]
    public async Task InterruptedSwapRestoresEveryReplacedFile()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.App);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "CommitFence-test.zip", PackageKind.App, null);
            var before = ReleaseFixtures.Snapshot(install.Folder);

            // the swap moves the entries of the package one by one: the move of "licenses" fails, after the others were done on any system
            var moved = new List<string>();
            var installer = ReleaseFixtures.CreateInstaller(feed, release, install, null, null);
            installer.FailsMoveOf = name =>
            {
                moved.Add(name);
                return name == "licenses";
            };

            var error = await Assert.ThrowsAsync<UpdateException>(installer.RunAsync);

            Assert.Contains("left as it was", error.Message, StringComparison.Ordinal);
            Assert.True(moved.Count > 1);
            Assert.Equal("licenses", moved[^1]);

            // every file that had been replaced is back, and nothing of the update is left
            var after = ReleaseFixtures.Snapshot(install.Folder);
            Assert.Equal(before.Keys.Append(ProgramLockFile.FileName).Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            Assert.All(before, file => Assert.Equal(file.Value, after[file.Key]));

            // the program is as it was, so the update can be tried again
            await ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync();
            Assert.Equal("new app", File.ReadAllText(Path.Combine(install.Folder, _appName)));
            Assert.Equal("new analyzer", File.ReadAllText(Path.Combine(install.Folder, "analyzers", "CF.Meaning.dll")));
        }
    }

    [Fact]
    public async Task RunningExecutableSurvivesItsOwnReplacement()
    {
        // what makes the update work on Windows: a running program cannot be overwritten or deleted, but it can be renamed
        if(!OperatingSystem.IsWindows())
            Assert.Skip("Renaming over a running program is what Windows forbids and the update works around; Linux and macOS let a running program be renamed or deleted, as the system guarantees.");

        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.CommandLine);
            var program = Path.Combine(install.Folder, _cliName);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "commitfence-cli-test.zip", PackageKind.CommandLine, null);

            // a disposable program that waits half a minute: a copy of cmd.exe pinging itself
            File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), program, true);
            var start = new ProcessStartInfo(program, "/c ping -n 30 127.0.0.1 >nul") { UseShellExecute = false, CreateNoWindow = true };
            using(var running = Process.Start(start) ?? throw new InvalidOperationException("the program did not start"))
            {
                try
                {
                    var deleteError = Record.Exception(() => File.Delete(program));
                    Assert.True(deleteError is IOException || deleteError is UnauthorizedAccessException);

                    await ReleaseFixtures.CreateInstaller(feed, release, install, null, null).RunAsync();

                    Assert.Equal("new cli", File.ReadAllText(program));
                    Assert.False(running.HasExited);

                    // the old program cannot be deleted while it runs: the clean-up leaves it for the next start
                    Assert.False(UpdateInstaller.TryCleanUp(install.Folder));
                    Assert.True(File.Exists(program + UpdateInstaller.OldSuffix));
                }
                finally
                {
                    running.Kill(true);
                    await running.WaitForExitAsync(TestContext.Current.CancellationToken);
                }
            }

            // once it has exited the old image can go
            await UpdateInstaller.CleanUpAsync(install.Folder, 10);
            Assert.False(File.Exists(program + UpdateInstaller.OldSuffix));
        }
    }
}
