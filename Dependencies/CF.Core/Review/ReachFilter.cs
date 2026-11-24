using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;

namespace CF.Core.Review;

/// <summary>
/// Keeps the findings on artifacts the change touched: code nobody touched is never
/// reported.
/// </summary>
/// <remarks>
/// A finding the build gives with no reach (no place in a file) is kept when it names no file (it belongs to a project
/// under review), when it is a compile error (the project does not build, wherever the error lies), or when it names a
/// file of the change. A rule finding with no place on a file the change does not hold is an old breach: it is dropped.
/// </remarks>
internal static class ReachFilter
{
    public static IReadOnlyList<Finding> Keep(IReadOnlyList<Finding> findings, IReadOnlyList<LineRange> changedLines)
    {
        return findings.Where(finding => IsTouched(finding, changedLines)).ToList();
    }

    public static IReadOnlyList<Finding> Keep(IReadOnlyList<Finding> findings, IReadOnlyList<ChangedFile> changedFiles)
    {
        return findings.Where(finding => IsKept(finding, changedFiles)).ToList();
    }

    /// <summary>
    /// Whether the path is one of the files of the change set, by the rule of this class for how a compiler names a
    /// file. The structure review needs it to keep the violations of a whole review to the files it reviews.
    /// </summary>
    public static bool Holds(ChangeSet changeSet, string path)
    {
        return FindFile(path, changeSet.Files) != null;
    }

    private static bool IsKept(Finding finding, IReadOnlyList<ChangedFile> changedFiles)
    {
        var file = FindFile(finding.File, changedFiles);
        if(finding.Reach == null)
            return finding.File == null || file != null || IsCompileError(finding);

        return file != null && IsTouched(finding, file.ChangedLines);
    }

    private static bool IsCompileError(Finding finding)
    {
        return finding.Source == FindingSource.Compiler && finding.Severity == Severity.Block;
    }

    private static ChangedFile? FindFile(string? path, IReadOnlyList<ChangedFile> changedFiles)
    {
        return FindExactly(path, changedFiles) ?? FindByTheOnlySpellingThatDiffersInCase(path, changedFiles);
    }

    private static ChangedFile? FindExactly(string? path, IReadOnlyList<ChangedFile> changedFiles)
    {
        return changedFiles.FirstOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.Ordinal));
    }

    /// <summary>
    /// A compiler can name a file in another case than git does (a project that lists <c>program.cs</c> for git's
    /// <c>Program.cs</c>).
    /// </summary>
    /// <remarks>
    /// Such a finding is still matched, but only when one changed file answers to the name: two files that differ by
    /// case alone never share their changed lines.
    /// </remarks>
    private static ChangedFile? FindByTheOnlySpellingThatDiffersInCase(string? path, IReadOnlyList<ChangedFile> changedFiles)
    {
        var candidates = changedFiles.Where(candidate => string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase)).ToList();
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static bool IsTouched(Finding finding, IReadOnlyList<LineRange> changedLines)
    {
        if(finding.Reach == null)
            return true;

        return changedLines.Any(range => Touches(range, finding.Reach));
    }

    private static bool Touches(LineRange range, ArtifactReach reach)
    {
        if(range.Touches(reach.Own.StartLine, reach.Own.EndLine))
            return true;

        if(reach.DependsOn.Any(span => range.Touches(span.StartLine, span.EndLine)))
            return true;

        return reach.Documentation != null && range.TouchesDocumentation(reach.Documentation.StartLine, reach.Documentation.EndLine);
    }
}