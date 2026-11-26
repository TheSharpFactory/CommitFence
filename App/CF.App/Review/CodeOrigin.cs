namespace CF.App.Review;

/// <summary>
/// What a review was of: the push, the staged changes or a folder.
/// </summary>
internal enum ReviewKind
{
    Push,
    Staged,
    Folder
}

/// <summary>
/// Where the code of a finding comes from and how to open the copy on disk: what kind of review it was, the commit the lines are
/// of (a push or a folder), the folder of the repository, the editor that is found and the way to open a file at a line.
/// </summary>
internal sealed class CodeOrigin
{
    public CodeOrigin(ReviewKind kind, string commit, string repositoryRoot, Func<string?> editorName, Action<string, int> openSource)
    {
        Kind = kind;
        Commit = commit;
        RepositoryRoot = repositoryRoot;
        EditorName = editorName;
        OpenSource = openSource;
    }

    public ReviewKind Kind { get; }

    /// <summary>
    /// The short id of the commit the reviewed lines are of; empty for the staged changes or when it is not known.
    /// </summary>
    public string Commit { get; }

    public string RepositoryRoot { get; }

    /// <summary>
    /// The name of the editor that opens a file at a line, or nothing when none was found.
    /// </summary>
    public Func<string?> EditorName { get; }

    public Action<string, int> OpenSource { get; }
}
