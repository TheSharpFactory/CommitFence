using CF.Adapters.Updates;
using CF.Core.Updates;
using Xunit;

namespace CF.Tests;

/// <summary>
/// A review and an update never overlap: a review holds the program's lock shared while it runs, an update takes it alone
/// before it replaces the files, and each side tries again for a bounded time.
/// </summary>
public sealed class ProgramLockTests
{
    private static readonly TimeSpan _long = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _short = TimeSpan.FromMilliseconds(300);

    [Fact]
    public void TwoReviewsHoldTheLockTogether()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(folder.Path, PackageKind.CommandLine);
            var programLock = new FileProgramLock(install, _short);

            using(programLock.HoldForReview())
            {
                using(programLock.HoldForReview())
                {
                    Assert.True(File.Exists(ProgramLockFile.PathIn(folder.Path)));
                }
            }
        }
    }

    [Fact]
    public async Task AReviewWaitsForAnUpdateToFinishReplacingTheFiles()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(folder.Path, PackageKind.CommandLine);
            var swapping = await ProgramLockFile.TryHoldAloneAsync(folder.Path, _long, TestContext.Current.CancellationToken);
            Assert.NotNull(swapping);
            var release = Task.Run(async () =>
            {
                await Task.Delay(400, TestContext.Current.CancellationToken);
                swapping.Dispose();
            }, TestContext.Current.CancellationToken);

            var started = DateTime.UtcNow;
            using(new FileProgramLock(install, _long).HoldForReview())
            {
                Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(300));
            }

            await release;
        }
    }

    [Fact]
    public async Task AReviewThatCannotHaveTheLockInTimeSaysSo()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(folder.Path, PackageKind.CommandLine);
            using(await ProgramLockFile.TryHoldAloneAsync(folder.Path, _long, TestContext.Current.CancellationToken))
            {
                var error = Assert.Throws<TimeoutException>(() => new FileProgramLock(install, _short).HoldForReview());

                Assert.Contains("update", error.Message, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void ACopyThatCannotBeUpdatedHoldsNothing()
    {
        using(var folder = new TestFolder())
        {
            var fromSource = new InstallLocation { Folder = folder.Path, Problem = "not a release" };

            using(new FileProgramLock(fromSource, _short).HoldForReview())
            {
                Assert.False(File.Exists(ProgramLockFile.PathIn(folder.Path)));
            }
        }
    }

    [Fact]
    public async Task AnUpdateWaitsForARunningReviewAndThenReplacesTheFiles()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.CommandLine);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "commitfence-cli-test.zip", PackageKind.CommandLine, null);
            var review = new FileProgramLock(install, _long).HoldForReview();
            var cliPath = Path.Combine(install.Folder, ReleaseFixtures.Program("commitfence"));
            var steps = new RecordingProgress();

            var update = ReleaseFixtures.CreateInstaller(feed, release, install, steps, null).RunAsync();
            await Task.Delay(700, TestContext.Current.CancellationToken);

            // the review is still running: nothing was replaced, and the update has not reached the swap
            Assert.False(update.IsCompleted);
            Assert.Equal("old cli", File.ReadAllText(cliPath));
            Assert.DoesNotContain(UpdateStage.Replacing, steps.Stages);

            review.Dispose();
            await update;

            Assert.Equal("new cli", File.ReadAllText(cliPath));
        }
    }

    [Fact]
    public async Task AnUpdateThatTheReviewsOutlastLeavesTheProgramAsItWas()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.CommandLine);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "commitfence-cli-test.zip", PackageKind.CommandLine, null);
            var before = ReleaseFixtures.ListFolder(install.Folder);

            using(new FileProgramLock(install, _long).HoldForReview())
            {
                var installer = ReleaseFixtures.CreateInstaller(feed, release, install, null, null);
                installer.ReviewsWaitLimit = _short;

                var error = await Assert.ThrowsAsync<UpdateException>(installer.RunAsync);

                Assert.Contains("review is still running", error.Message, StringComparison.Ordinal);
                Assert.EndsWith("Nothing was changed.", error.Message, StringComparison.Ordinal);
            }

            // the lock file is the only thing that appeared beside the program
            var after = ReleaseFixtures.ListFolder(install.Folder);
            Assert.Equal(before.Append(ProgramLockFile.FileName).Order(StringComparer.Ordinal).ToList(), after);
            Assert.Equal("old cli", File.ReadAllText(Path.Combine(install.Folder, ReleaseFixtures.Program("commitfence"))));
        }
    }

    [Fact]
    public async Task CancelEndsTheWaitForARunningReview()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.CommandLine);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "commitfence-cli-test.zip", PackageKind.CommandLine, null);

            using(new FileProgramLock(install, _long).HoldForReview())
            {
                using(var cancellation = new UpdateCancellation())
                {
                    var update = ReleaseFixtures.CreateInstaller(feed, release, install, null, cancellation).RunAsync();
                    await Task.Delay(500, TestContext.Current.CancellationToken);
                    Assert.True(cancellation.TryCancel());

                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => update);
                }
            }

            Assert.Equal("old cli", File.ReadAllText(Path.Combine(install.Folder, ReleaseFixtures.Program("commitfence"))));
        }
    }
}