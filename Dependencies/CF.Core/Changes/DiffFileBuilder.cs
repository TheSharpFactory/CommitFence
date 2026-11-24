namespace CF.Core.Changes;

/// <summary>
/// Collects what the diff says about one file while it is being read.
/// </summary>
internal sealed class DiffFileBuilder
{
    public DiffFileBuilder(string path)
    {
        Path = path;
    }

    public string Path { get; set; }

    public string? OldPath { get; set; }

    /// <summary>
    /// For a renamed file, the path it had before.
    /// </summary>
    public string? RenamedFrom { get; set; }

    public ChangeStatus Status { get; set; } = ChangeStatus.Modified;

    public List<LineRange> ChangedLines { get; } = new List<LineRange>();

    /// <summary>
    /// Whether git wrote no lines for the file because it takes it for binary.
    /// </summary>
    public bool IsWithoutLines { get; set; }

    /// <summary>
    /// The lines of the current hunk that the hunk header announced and that have not been read yet. While any are
    /// left, a line is content, never a header.
    /// </summary>
    public int OldLinesLeft { get; set; }

    public int NewLinesLeft { get; set; }

    public bool InHunk
    {
        get { return OldLinesLeft > 0 || NewLinesLeft > 0; }
    }
}