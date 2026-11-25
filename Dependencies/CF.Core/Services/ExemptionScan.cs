namespace CF.Core.Services;

/// <summary>What a scan of the last commit of a folder's repository found.</summary>
public sealed class ExemptionScan
{
    private const int _shortIdLength = 7;

    private ExemptionScan(string? branch, string? commit, string? workFolder, IReadOnlyList<ExemptionEntry> exemptions)
    {
        Branch = branch;
        Commit = commit;
        WorkFolder = workFolder;
        Exemptions = exemptions;
    }

    /// <summary>The branch that is checked out, or null when HEAD is detached.</summary>
    public string? Branch { get; }

    /// <summary>The short id of the last commit, or null when the repository has no commit yet.</summary>
    public string? Commit { get; }

    /// <summary>
    /// The top folder of the working tree the scan read, which the paths of the exemptions are relative to; null when the scan
    /// was not made from a working tree.
    /// </summary>
    public string? WorkFolder { get; }

    /// <summary>Every exemption the language plug-ins read in the files of the last commit, by file and line.</summary>
    public IReadOnlyList<ExemptionEntry> Exemptions { get; }

    /// <summary>Whether the repository has no commit yet: an answer of its own, not an error.</summary>
    public bool HasNoCommit
    {
        get { return Commit == null; }
    }

    /// <summary>"In the last commit of main, 4f2a91c" (without the branch's name when HEAD is detached).</summary>
    public string HeadLine
    {
        get { return Branch == null ? $"In the last commit, {Commit}" : $"In the last commit of {Branch}, {Commit}"; }
    }

    /// <summary>"Exemptions are read from the last commit, and main has none."</summary>
    public string NoCommitSentence
    {
        get { return $"Exemptions are read from the last commit, and {Branch ?? "this repository"} has none."; }
    }

    /// <summary>A scan of a commit.</summary>
    public static ExemptionScan Of(string? branch, string commitId, IReadOnlyList<ExemptionEntry> exemptions, string? workFolder = null)
    {
        return new ExemptionScan(branch, commitId.Length > _shortIdLength ? commitId[.._shortIdLength] : commitId, workFolder, exemptions);
    }

    /// <summary>A repository with no commit.</summary>
    public static ExemptionScan NoCommit(string? branch)
    {
        return new ExemptionScan(branch, null, null, []);
    }
}
