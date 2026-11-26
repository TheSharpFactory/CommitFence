namespace CF.Core.Services;

/// <summary>The files of a language that a path pattern names, as the last commit holds them.</summary>
/// <param name="Files">The matching paths from the root of the repository, in the order git lists them.</param>
public sealed record PathMatches(IReadOnlyList<string> Files)
{
    /// <summary>How many files match.</summary>
    public int Count
    {
        get { return Files.Count; }
    }
}
