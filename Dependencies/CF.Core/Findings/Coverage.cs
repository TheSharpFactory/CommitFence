namespace CF.Core.Findings;

/// <summary>
/// What part of the change a review covered, and what became of it. It keeps an incomplete review from looking clean.
/// </summary>
public sealed class Coverage
{
    /// <summary>
    /// Creates a coverage entry.
    /// </summary>
    public Coverage(string what, CoverageStatus status, string reason)
        : this(what, status, reason, CoverageKind.File)
    {
    }

    /// <summary>
    /// Creates a coverage entry of the given kind.
    /// </summary>
    public Coverage(string what, CoverageStatus status, string reason, CoverageKind kind)
    {
        What = what;
        Status = status;
        Reason = reason;
        Kind = kind;
    }

    /// <summary>
    /// The part: a project and framework, or the files of a language.
    /// </summary>
    public string What { get; }

    /// <summary>
    /// Whether it was reviewed, failed or skipped.
    /// </summary>
    public CoverageStatus Status { get; }

    /// <summary>
    /// Why, when it was not reviewed; empty otherwise.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Whether the entry is about a source file or about a part of the build.
    /// </summary>
    public CoverageKind Kind { get; }
}