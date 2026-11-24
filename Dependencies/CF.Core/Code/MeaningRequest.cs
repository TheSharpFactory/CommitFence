namespace CF.Core.Code;

/// <summary>
/// What a language's meaning source is asked for the tree before a push: the tree, the private copy that holds it, a
/// new folder for the build's logs, and the parts of the build that answered for the tree after the push, which are the
/// parts to answer for here.
/// </summary>
public sealed class MeaningRequest
{
    /// <summary>
    /// Creates a request.
    /// </summary>
    public MeaningRequest(string tree, string treeFolder, string logFolder, IReadOnlyList<string> parts)
    {
        Tree = tree;
        TreeFolder = treeFolder;
        LogFolder = logFolder;
        Parts = parts;
    }

    /// <summary>
    /// The tree, as git names it.
    /// </summary>
    public string Tree { get; }

    /// <summary>
    /// The private copy, already moved to that tree.
    /// </summary>
    public string TreeFolder { get; }

    /// <summary>
    /// A new, empty folder for the logs of the build.
    /// </summary>
    public string LogFolder { get; }

    /// <summary>
    /// The keys of the parts to answer for.
    /// </summary>
    public IReadOnlyList<string> Parts { get; }
}