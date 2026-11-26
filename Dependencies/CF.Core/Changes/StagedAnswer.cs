namespace CF.Core.Changes;

/// <summary>
/// What the index holds now, before a review of it: how many files are staged, how many have changes that are not staged, and the
/// plain reason when git cannot say.
/// </summary>
public sealed class StagedAnswer
{
    private StagedAnswer(IReadOnlyList<string> staged, int unstagedCount, bool stagedFileHasUnstagedChanges, string headCommit, string reason, string indexTree = "", string baseTree = "", IReadOnlyList<StagedFileSize>? sizes = null, IReadOnlyList<UnstagedFile>? unstagedFiles = null)
    {
        Sizes = sizes ?? [];
        UnstagedFiles = unstagedFiles ?? [];
        IndexTree = indexTree;
        BaseTree = baseTree;
        Staged = staged;
        UnstagedCount = unstagedCount;
        StagedFileHasUnstagedChanges = stagedFileHasUnstagedChanges;
        HeadCommit = headCommit;
        Reason = reason;
    }

    /// <summary>The staged files, from the top of the repository.</summary>
    public IReadOnlyList<string> Staged { get; }

    /// <summary>
    /// How many lines each staged file has, in the order of <see cref="Staged" />; empty when that was not read, which it is not for a
    /// large number of files.
    /// </summary>
    public IReadOnlyList<StagedFileSize> Sizes { get; }

    /// <summary>The first files that have changes that are not staged, each said to be new or changed; empty when they were not read.</summary>
    public IReadOnlyList<UnstagedFile> UnstagedFiles { get; }

    /// <summary>How many files have changes that are not staged.</summary>
    public int UnstagedCount { get; }

    /// <summary>Whether a staged file has further changes that are not staged.</summary>
    public bool StagedFileHasUnstagedChanges { get; }

    /// <summary>The short id of the last commit, which the staged changes are against; empty when the repository has no commit yet.</summary>
    public string HeadCommit { get; }

    /// <summary>Whether the repository has a commit: the staged changes are against it, or against nothing when it has none.</summary>
    public bool HasCommit
    {
        get { return HeadCommit.Length > 0; }
    }

    /// <summary>
    /// The tree git writes for the index now: it names the staged state, down to the content of each file, so two states with
    /// the same files and different content have different trees.
    /// </summary>
    public string IndexTree { get; }

    /// <summary>The tree of the last commit, which a review of the staged changes compares the index with.</summary>
    public string BaseTree { get; }

    /// <summary>Why git could not say, or empty.</summary>
    public string Reason { get; }

    /// <summary>Whether nothing is staged and git could say so.</summary>
    public bool NothingStaged
    {
        get { return Reason.Length == 0 && Staged.Count == 0; }
    }

    /// <summary>The answer when git could say.</summary>
    public static StagedAnswer Of(IReadOnlyList<string> staged, int unstagedCount, bool stagedFileHasUnstagedChanges, string headCommit, string indexTree = "", string baseTree = "", IReadOnlyList<StagedFileSize>? sizes = null, IReadOnlyList<UnstagedFile>? unstagedFiles = null)
    {
        return new StagedAnswer(staged, unstagedCount, stagedFileHasUnstagedChanges, headCommit, string.Empty, indexTree, baseTree, sizes, unstagedFiles);
    }

    /// <summary>The answer when git could not say.</summary>
    public static StagedAnswer Unknown(string reason)
    {
        return new StagedAnswer(new List<string>(), 0, false, string.Empty, reason);
    }
}

/// <summary>How many lines a staged file has.</summary>
/// <param name="Path">The path from the top of the repository.</param>
/// <param name="Lines">The number of lines of the staged copy.</param>
public sealed record StagedFileSize(string Path, int Lines);

/// <summary>A file with changes that are not staged.</summary>
/// <param name="Path">The path from the top of the repository.</param>
/// <param name="IsNew">Whether the last commit does not have the file: it is new, not changed.</param>
public sealed record UnstagedFile(string Path, bool IsNew);
