using CF.Core.Changes;
using CF.Core.Code;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// What a push is compared with: the commit the remote has, or, for a ref it does not have, the point where the commits
/// left everything the remote is known to hold.
/// </summary>
public sealed class PushRangeTests
{
    private static readonly string _zero = new string('0', 40);

    [Theory]
    [InlineData("KnownNonFastForwardTipUsesAdvertisedCommit")]
    [InlineData("NewTopicFromTwoKnownBranchesUsesTheCommonAncestorOfTheMeetingPoints")]
    [InlineData("UnknownAdvertisedTipUsesTheConservativeBaselineAndSaysSo")]
    [InlineData("FirstPushIsComparedWithTheEmptyTree")]
    [InlineData("DeletionHasNothingToCompare")]
    [InlineData("AlreadyKnownLightweightTagHasNothingToCompare")]
    [InlineData("NewAnnotatedTagIsComparedAsItsUnpublishedCommit")]
    public void RefSelectsTheConservativeComparisonRange(string scenario)
    {
        using(var folder = new TestFolder())
        {
            var git = new FakeGit(folder.Path);
            BuildHistory(git);
            var row = Describe(git, scenario);
            var languages = new List<ILanguage> { new TestLanguage() };
            var scope = ReviewScope.ForPush("origin", "https://example.invalid/origin", PushedRefs.FromRefs(new[] { row.Ref }));

            var changeSets = new ChangeResolver(git, languages).Resolve(git.Repository, scope);

            var changeSet = Assert.Single(changeSets);
            if(row.ExpectedBefore == null)
            {
                Assert.Empty(git.Diffs);
                Assert.Equal(string.Empty, changeSet.Tree);
                return;
            }

            var comparison = Assert.Single(git.Diffs);
            Assert.Equal(row.ExpectedBefore, comparison.Before);
            Assert.Equal(FakeGit.TreeIdOf(row.TipName), comparison.After);
            Assert.Equal(FakeGit.TreeIdOf(row.TipName), changeSet.Tree);
            Assert.Equal(row.ExpectedNote, changeSet.Note.Contains("is not in this clone", StringComparison.Ordinal));
            if(row.ExpectedNote)
            {
                Assert.Contains("refs/heads/topic", changeSet.Note, StringComparison.Ordinal);
                Assert.Contains(FakeGit.IdOf("ghost")[..10], changeSet.Note, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// main runs m1, m2, m3; the branch release leaves m1 with r1; the topic leaves m2 with t1, and the topic's own commits
    /// are t1 and t2.
    /// </summary>
    private static void BuildHistory(FakeGit git)
    {
        git.AddCommit("m1", Files("m1"));
        git.AddCommit("m2", Files("m2"), "m1");
        git.AddCommit("m3", Files("m3"), "m2");
        git.AddCommit("r1", Files("r1"), "m1");
        git.AddCommit("t1", Files("t1"), "m2");
        git.AddCommit("t2", Files("t2"), "t1");
        git.AddCommit("alternative", Files("alternative"), "m1");
        git.AddCommit("old-tip", Files("old-tip"), "m1");
        git.Name(FakeGit.IdOf("tag-object"), "t2");
    }

    private static Dictionary<string, string> Files(string name)
    {
        return new Dictionary<string, string> { ["state.txt"] = name + "\n" };
    }

    private static Row Describe(FakeGit git, string scenario)
    {
        switch(scenario)
        {
            case "KnownNonFastForwardTipUsesAdvertisedCommit":
                git.RemoteTips.Add(FakeGit.IdOf("old-tip"));
                return new Row(Ref("alternative", "old-tip"), "alternative", FakeGit.IdOf("old-tip"), false);
            case "NewTopicFromTwoKnownBranchesUsesTheCommonAncestorOfTheMeetingPoints":
                git.RemoteTips.AddRange(new[] { FakeGit.IdOf("m3"), FakeGit.IdOf("r1") });
                return new Row(Ref("t2", null), "t2", FakeGit.IdOf("m1"), false);
            case "UnknownAdvertisedTipUsesTheConservativeBaselineAndSaysSo":
                git.RemoteTips.Add(FakeGit.IdOf("m3"));
                return new Row(Ref("t2", "ghost"), "t2", FakeGit.IdOf("m2"), true);
            case "FirstPushIsComparedWithTheEmptyTree":
                return new Row(Ref("t2", null), "t2", FakeGit.EmptyTreeId, false);
            case "DeletionHasNothingToCompare":
                git.RemoteTips.Add(FakeGit.IdOf("m3"));
                return new Row(new PushedRef("(delete)", _zero, "refs/heads/topic", FakeGit.IdOf("m3")), "m3", null, false);
            case "AlreadyKnownLightweightTagHasNothingToCompare":
                git.RemoteTips.Add(FakeGit.IdOf("m3"));
                return new Row(new PushedRef("refs/tags/v1", FakeGit.IdOf("m2"), "refs/tags/v1", _zero), "m2", null, false);
            default:
                git.RemoteTips.Add(FakeGit.IdOf("m3"));
                return new Row(new PushedRef("refs/tags/v2", FakeGit.IdOf("tag-object"), "refs/tags/v2", _zero), "t2", FakeGit.IdOf("m2"), false);
        }
    }

    private static PushedRef Ref(string localCommit, string? remoteCommit)
    {
        return new PushedRef("refs/heads/topic", FakeGit.IdOf(localCommit), "refs/heads/topic", remoteCommit == null ? _zero : FakeGit.IdOf(remoteCommit));
    }

    /// <summary>
    /// The ref git gives the hook, the commit whose tree is reviewed, the commit or tree it is compared with (nothing when
    /// there is nothing to compare), and whether the review says that the remote's commit is not in the clone.
    /// </summary>
    private sealed record Row(PushedRef Ref, string TipName, string? ExpectedBefore, bool ExpectedNote);
}
