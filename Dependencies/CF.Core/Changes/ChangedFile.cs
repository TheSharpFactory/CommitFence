namespace CF.Core.Changes;

/// <summary>
/// A file a change touched, with the lines of it that changed.
/// </summary>
public sealed class ChangedFile
{
    /// <summary>
    /// Creates a changed file.
    /// </summary>
    public ChangedFile(string path, string? language, ChangeStatus status, IReadOnlyList<LineRange> changedLines, string? previousPath = null)
    {
        Path = path;
        PreviousPath = previousPath;
        Language = language;
        Status = status;
        ChangedLines = changedLines;
    }

    /// <summary>
    /// The path after the change, relative to the repository, written with forward slashes.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// For a renamed file, the path it had before the change; nothing otherwise. The place a file left matters as much
    /// as the place it went to: what was built from it there may no longer build.
    /// </summary>
    public string? PreviousPath { get; }

    /// <summary>
    /// The identifier of the language that owns the file, or nothing when no language does.
    /// </summary>
    public string? Language { get; }

    /// <summary>
    /// What happened to the file.
    /// </summary>
    public ChangeStatus Status { get; }

    /// <summary>
    /// The lines that changed, in the order of the file.
    /// </summary>
    public IReadOnlyList<LineRange> ChangedLines { get; }
}