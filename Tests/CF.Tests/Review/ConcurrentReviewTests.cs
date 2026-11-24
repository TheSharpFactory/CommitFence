using CF.Adapters.Storage;
using CF.Core.Changes;
using CF.Core.Review;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// Two reviews of one repository at the same time share one private copy; the lock keeps the second out until the first
/// has read the last file of its tree.
/// </summary>
public sealed class ConcurrentReviewTests
{
    private static readonly string[] _expectedEvents = { "move:A", "asked:B", "read:// tree A", "move:B", "read:// tree B" };

    [Fact(Timeout = 120000)]
    public async Task ConcurrentReviewsKeepTheirOwnTrees()
    {
        var events = new List<string>();
        var syncRoot = new Lock();
        var firstIsReading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondIsWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var letFirstGo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceReads = 0;
        Thread? secondThread = null;

        void Record(string text)
        {
            lock(syncRoot)
            {
                events.Add(text);
            }
        }

        using(var harness = new ReviewHarness())
        {
            var settings = ReviewSources.Settings(ReviewSources.Team010);
            harness.Fake.AddCommit("base", new Dictionary<string, string> { ["commitfence.json"] = settings, ["src/Core.cs"] = "// tree base\n" });
            harness.Fake.AddCommit("headA", new Dictionary<string, string> { ["commitfence.json"] = settings, ["src/Core.cs"] = "// tree A\n" + ReviewSources.Class("Core", ReviewSources.SignBad) }, "base");
            harness.Fake.AddCommit("headB", new Dictionary<string, string> { ["commitfence.json"] = settings, ["src/Core.cs"] = "// tree B\n" + ReviewSources.Class("Core", ReviewSources.SignOk) }, "base");
            harness.FileSystem = new ObservedFileSystem(
                path =>
                {
                    if(!path.EndsWith("Core.cs", StringComparison.Ordinal) || Interlocked.Increment(ref sourceReads) != 1)
                        return;

                    // the first review has its tree in the private copy and is about to read it: it waits here
                    firstIsReading.SetResult();
                    letFirstGo.Task.GetAwaiter().GetResult();
                },
                (path, text) =>
                {
                    if(path.EndsWith("Core.cs", StringComparison.Ordinal))
                        Record("read:" + text[..9].Trim());
                });
            ((FakeSnapshots)harness.Snapshots).Moved = tree => Record("move:" + (tree == FakeGit.TreeIdOf("headA") ? "A" : "B"));
            harness.ReviewLock = new ProbedReviewLock(new FileReviewLock(new FolderOfTest(Path.Combine(harness.Scratch, "locks"))), count =>
            {
                if(count != 2)
                    return;

                secondThread = Thread.CurrentThread;
                Record("asked:B");
                secondIsWaiting.SetResult();
            });

            var first = Task.Run(() => harness.Service().ReviewChanges(harness.RepositoryFolder, ReviewScope.ForRange("base", "headA"), null));
            await firstIsReading.Task;
            var second = Task.Run(() => harness.Service().ReviewChanges(harness.RepositoryFolder, ReviewScope.ForRange("base", "headB"), null));
            await secondIsWaiting.Task;

            // The second review has asked for the lock. It is held back when its thread sleeps in the lock's wait; a lock
            // that lets it through lets it run to its end. Either way the first review is released only after that, so a
            // lock that does not exclude cannot pass by being slow.
            while(!second.IsCompleted && !secondThread!.ThreadState.HasFlag(ThreadState.WaitSleepJoin))
                Thread.Yield();

            letFirstGo.SetResult();
            var firstResult = await first;
            var secondResult = await second;

            Assert.Equal(_expectedEvents, events);
            Assert.Equal(ReviewOutcome.Blocked, firstResult.Outcome);
            Assert.Equal("src/Core.cs", Assert.Single(firstResult.Findings).File);
            Assert.Equal(ReviewOutcome.Clean, secondResult.Outcome);
        }
    }
}
