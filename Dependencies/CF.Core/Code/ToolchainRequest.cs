using CF.Core.Changes;

namespace CF.Core.Code;

/// <summary>
/// What a toolchain is asked to check: the private copy, where this review's logs go and the changed files it concerns.
/// </summary>
public sealed class ToolchainRequest
{
    /// <summary>
    /// Creates a request.
    /// </summary>
    public ToolchainRequest(string tree, string treeFolder, string logFolder, IReadOnlyList<ChangedFile> changedFiles, bool isWholeReview)
    {
        Tree = tree;
        TreeFolder = treeFolder;
        LogFolder = logFolder;
        ChangedFiles = changedFiles;
        IsWholeReview = isWholeReview;
    }

    /// <summary>
    /// The tree under review, as git names it: what the private copy holds.
    /// </summary>
    public string Tree { get; }

    /// <summary>
    /// The private copy of the tree under review, outside the developer's working folder.
    /// </summary>
    public string TreeFolder { get; }

    /// <summary>
    /// A new, empty folder chosen by this review, in the product's data folder, for the logs of the tools.
    /// </summary>
    public string LogFolder { get; }

    /// <summary>
    /// The changed files the toolchain concerns, deleted ones included.
    /// </summary>
    public IReadOnlyList<ChangedFile> ChangedFiles { get; }

    /// <summary>
    /// Whether the files are the whole of a folder read against nothing: then only the projects that own them are under
    /// review (what they need is built first), and nothing found in a project is "old".
    /// </summary>
    public bool IsWholeReview { get; }
}