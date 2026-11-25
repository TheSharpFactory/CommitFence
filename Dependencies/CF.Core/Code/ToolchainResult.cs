using CF.Core.Findings;

namespace CF.Core.Code;

/// <summary>
/// What a toolchain found and what it covered.
/// </summary>
public sealed class ToolchainResult
{
    /// <summary>
    /// Creates a result.
    /// </summary>
    public ToolchainResult(IReadOnlyList<Finding> findings, IReadOnlyList<Coverage> coverage, IReadOnlyList<string> notes, MeaningAnswers meaning, IReadOnlyList<FileReading> readings)
    {
        Findings = findings;
        Coverage = coverage;
        Notes = notes;
        Meaning = meaning;
        Readings = readings;
    }

    /// <summary>
    /// The findings, before the review keeps only those the change touched.
    /// </summary>
    public IReadOnlyList<Finding> Findings { get; }

    /// <summary>Whether this run invoked a build.</summary>
    public bool RanBuild { get; init; }

    /// <summary>
    /// What was checked and what could not be, one entry for each part of the build.
    /// </summary>
    public IReadOnlyList<Coverage> Coverage { get; }

    /// <summary>
    /// Plain sentences on what was looked at, shown in the summary.
    /// </summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>
    /// What the build said about the tree: what the declarations are and what the names refer to. Answers with gaps
    /// when any part of the build did not answer.
    /// </summary>
    public MeaningAnswers Meaning { get; }

    /// <summary>
    /// For each changed source file, every way the build compiles it, with the conditional symbols of that way. A file
    /// the build does not compile has none.
    /// </summary>
    public IReadOnlyList<FileReading> Readings { get; }
}