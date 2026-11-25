namespace CF.Core.Services;

/// <summary>Finds the lines that set a rule aside in the last commit of a repository.</summary>
public interface IExemptionService
{
    /// <summary>
    /// Every exemption the language plug-ins read in the files of the last commit of the branch checked out in the
    /// repository the folder belongs to: each with its file, line, rule, form and reason, and the lines around it from the
    /// same commit when asked for them (<paramref name="withLines"/>); without them each entry's lines are empty. The working
    /// folder's files are never read. A repository with no commit gives a scan that says so.
    /// </summary>
    /// <param name="folder">Any folder of the repository's working tree.</param>
    /// <param name="withLines">Whether to cut the lines around each exemption: the screen shows them, the command does not.</param>
    /// <param name="cancellationToken">Stops the scan.</param>
    /// <exception cref="ExemptionScanException">The folder is not in a repository, or git cannot be asked.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    ExemptionScan Scan(string folder, bool withLines, CancellationToken cancellationToken);
}
