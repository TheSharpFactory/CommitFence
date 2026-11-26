namespace CF.Core.Changes;

/// <summary>
/// What one ref, one staged state or one range changed: the tree under review and the files that differ from the tree
/// before it.
/// </summary>
public sealed class ChangeSet
{
    private ChangeSet(string label, string beforeTree, string tree, IReadOnlyList<ChangedFile> files, IReadOnlyList<string> ruleFilesChanged, string note, string? wholeFolder, bool hasUncommittedWork = false, string commit = "")
    {
        Label = label;
        BeforeTree = beforeTree;
        Tree = tree;
        Files = files;
        RuleFilesChanged = ruleFilesChanged;
        Note = note;
        WholeFolder = wholeFolder;
        HasUncommittedWork = hasUncommittedWork;
        Commit = commit;
    }

    /// <summary>
    /// What this is, such as the ref: used when the result says what was looked at.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// The tree under review, as git names it; empty when there is nothing to review.
    /// </summary>
    public string Tree { get; }

    /// <summary>
    /// The files that changed.
    /// </summary>
    public IReadOnlyList<ChangedFile> Files { get; }

    /// <summary>
    /// The paths of rule files (.editorconfig, commitfence.json) the change touched.
    /// </summary>
    public IReadOnlyList<string> RuleFilesChanged { get; }

    /// <summary>
    /// The tree before the change, as git names it; the empty tree when the change starts from nothing, and empty when
    /// there is nothing to review.
    /// </summary>
    public string BeforeTree { get; }

    /// <summary>
    /// A plain sentence on what was looked at, shown in the summary.
    /// </summary>
    public string Note { get; }

    /// <summary>
    /// Whether this is the whole of a folder read against nothing: every file under it is an added file and every line
    /// is touched, so nothing is "already known" and nothing is changed.
    /// </summary>
    /// <remarks>
    /// The filters of what a change touched need no branch for it, because every line is touched. What does read it is
    /// what must say something else or look elsewhere: the words, which violations are in scope, the projects that
    /// depend on the files.
    /// </remarks>
    public bool IsWholeReview
    {
        get { return WholeFolder != null; }
    }

    /// <summary>The reviewed folder relative to the repository, empty for the root and null for a change.</summary>
    public string? WholeFolder { get; }

    /// <summary>
    /// The full id of the last commit as it was when this change set was taken, for a staged review (the commit the index is
    /// compared with) and a whole review (the commit read); empty for the others and when the repository has no commit.
    /// </summary>
    public string Commit { get; }

    /// <summary>
    /// Whether the working folder holds changes that are not committed and so are not part of this review.
    /// </summary>
    public bool HasUncommittedWork { get; }

    /// <summary>
    /// What this change set calls its files in a sentence: the files of a change are "changed files", the files of a
    /// whole review are just "files".
    /// </summary>
    public string NameOfFiles(int count)
    {
        var noun = count == 1 ? "file" : "files";
        return IsWholeReview ? noun : "changed " + noun;
    }

    /// <summary>
    /// What this change set calls the tree under review, as opposed to the tree before it.
    /// </summary>
    public string NameOfTreeAfter()
    {
        return IsWholeReview ? "the tree under review" : "the tree after the push";
    }

    /// <summary>
    /// The same change set with one more sentence on what was looked at.
    /// </summary>
    public ChangeSet WithNote(string sentence)
    {
        return new ChangeSet(Label, BeforeTree, Tree, Files, RuleFilesChanged, Note + " " + sentence, WholeFolder, HasUncommittedWork, Commit);
    }

    /// <summary>
    /// The same change set, marked as left alone by the working folder's changes that are not committed.
    /// </summary>
    public ChangeSet WithUncommittedWork(string sentence)
    {
        return new ChangeSet(Label, BeforeTree, Tree, Files, RuleFilesChanged, Note + " " + sentence, WholeFolder, true, Commit);
    }

    /// <summary>
    /// The same change set, naming the commit it was taken against.
    /// </summary>
    public ChangeSet WithCommit(string commit)
    {
        return new ChangeSet(Label, BeforeTree, Tree, Files, RuleFilesChanged, Note, WholeFolder, HasUncommittedWork, commit);
    }

    /// <summary>
    /// What a push, the staged content or a range changed: the files that differ between the tree before and the tree.
    /// </summary>
    public static ChangeSet ForChanges(string label, string beforeTree, string tree, IReadOnlyList<ChangedFile> files, IReadOnlyList<string> ruleFilesChanged, string note)
    {
        return new ChangeSet(label, beforeTree, tree, files, ruleFilesChanged, note, null);
    }

    /// <summary>
    /// The whole of a folder of the tree, read against nothing: each file is an added file with every line touched.
    /// Nothing changed, so no rule file changed.
    /// </summary>
    public static ChangeSet ForWholeReview(string label, string beforeTree, string tree, IReadOnlyList<ChangedFile> files, string note, string folder = "")
    {
        return new ChangeSet(label, beforeTree, tree, files, new List<string>(), note, folder);
    }

    /// <summary>
    /// A change set that has nothing to review, with the reason.
    /// </summary>
    public static ChangeSet Nothing(string label, string reason)
    {
        return ForChanges(label, string.Empty, string.Empty, new List<ChangedFile>(), new List<string>(), $"{label}: {reason}.");
    }
}
