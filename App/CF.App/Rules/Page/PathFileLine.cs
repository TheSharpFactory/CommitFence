namespace CF.App.Rules.Page;

/// <summary>
/// One file the path picker lists: its folders, then its name, which is drawn stronger.
/// </summary>
internal sealed class PathFileLine
{
    public PathFileLine(string path)
    {
        var cut = path.LastIndexOf('/') + 1;
        Folders = Breakable(path[..cut]);
        Name = path[cut..];
    }

    public string Folders { get; }

    public string Name { get; }

    /// <summary>
    /// The folders and the name as one text, which can break after any separator, the last one included.
    /// </summary>
    public string Full
    {
        get { return Folders + Name; }
    }

    /// <summary>
    /// A path breaks only after a separator: a zero width space after each one tells the text where it may.
    /// </summary>
    public static string Breakable(string pattern)
    {
        return pattern.Replace("/", "/​", StringComparison.Ordinal);
    }
}
