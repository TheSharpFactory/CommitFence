using CF.Adapters.Updates;
using CF.Core.Updates;
using Xunit;

namespace CF.Tests;

/// <summary>
/// A review and an update never overlap: a review holds the program's lock shared while it runs, an update takes it alone before it
/// replaces the files, and each side gives up after a bounded time. The tests give a side no time at all to wait, so that none of
/// them waits on the clock.
/// </summary>
public sealed class ProgramLockTests
{
    private static readonly TimeSpan _long = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ProgramSwapExcludesReviews()
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(folder.Path, PackageKind.CommandLine);
            var program = Path.Combine(folder.Path, ReleaseFixtures.Program("commitfence"));
            var programLock = new FileProgramLock(install, _long);

            // reviews share the lock, so two run together; the swap needs it alone and gets it only when both have ended
            var first = programLock.HoldForReview();
            var second = programLock.HoldForReview();
            Assert.True(File.Exists(ProgramLockFile.PathIn(folder.Path)));
            Assert.Null(await ProgramLockFile.TryHoldAloneAsync(folder.Path, TimeSpan.Zero, TestContext.Current.CancellationToken));
            first.Dispose();
            Assert.Null(await ProgramLockFile.TryHoldAloneAsync(folder.Path, TimeSpan.Zero, TestContext.Current.CancellationToken));
            second.Dispose();
            var swapping = await ProgramLockFile.TryHoldAloneAsync(folder.Path, TimeSpan.Zero, TestContext.Current.CancellationToken);
            Assert.NotNull(swapping);

            // while the swap holds it a review gives up and says an update is why; after it, a review gets in
            var error = Assert.Throws<TimeoutException>(() => new FileProgramLock(install, TimeSpan.Zero).HoldForReview());
            Assert.Contains("update", error.Message, StringComparison.Ordinal);
            swapping.Dispose();
            using(new FileProgramLock(install, TimeSpan.Zero).HoldForReview())
            {
                Assert.True(File.Exists(ProgramLockFile.PathIn(folder.Path)));
            }

            // the lock is a file of its own: the program is as it was
            Assert.Equal("old cli", File.ReadAllText(program));
        }
    }

    [Theory]
    [InlineData("the wait runs out")]
    [InlineData("the update is cancelled")]
    public async Task WaitingInstallerCanExitWithoutChanges(string how)
    {
        using(var folder = new TestFolder())
        {
            var install = ReleaseFixtures.CreateInstall(Path.Combine(folder.Path, "program"), PackageKind.CommandLine);
            var feed = new LocalFeed(Path.Combine(folder.Path, "feed"));
            var release = ReleaseFixtures.PublishPackage(feed, "commitfence-cli-test.zip", PackageKind.CommandLine, null);
            var before = ReleaseFixtures.Snapshot(install.Folder);
            var steps = new RecordingProgress();
            var downloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // a review is running, so the update has to wait for it
            var review = new FileProgramLock(install, _long).HoldForReview();
            try
            {
                using(var cancellation = new UpdateCancellation())
                {
                    var progress = new ReactingProgress(value =>
                    {
                        steps.Report(value);
                        if(value.Percent >= 100)
                            downloaded.TrySetResult();
                    });
                    var installer = ReleaseFixtures.CreateInstaller(feed, release, install, progress, cancellation);
                    if(how == "the wait runs out")
                    {
                        installer.ReviewsWaitLimit = TimeSpan.Zero;
                        var error = await Assert.ThrowsAsync<UpdateException>(installer.RunAsync);
                        Assert.Contains("review is still running", error.Message, StringComparison.Ordinal);
                        Assert.EndsWith("Nothing was changed.", error.Message, StringComparison.Ordinal);
                    }
                    else
                    {
                        var update = installer.RunAsync();
                        await downloaded.Task;
                        Assert.True(cancellation.TryCancel());
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => update);
                    }
                }
            }
            finally
            {
                review.Dispose();
            }

            // neither ended in the swap; the program is as it was and the lock is free
            Assert.DoesNotContain(UpdateStage.Replacing, steps.Stages);
            var after = ReleaseFixtures.Snapshot(install.Folder);
            Assert.Equal(before.Keys.Append(ProgramLockFile.FileName).Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            Assert.All(before, file => Assert.Equal(file.Value, after[file.Key]));
            using(await ProgramLockFile.TryHoldAloneAsync(install.Folder, TimeSpan.Zero, TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("the lock was not free"))
            {
                Assert.False(Directory.Exists(Path.Combine(install.Folder, UpdateInstaller.StagingFolderName)));
            }
        }
    }
}
