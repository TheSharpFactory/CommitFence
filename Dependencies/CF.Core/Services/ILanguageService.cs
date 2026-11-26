namespace CF.Core.Services;

/// <summary>Tells which languages this build can review and what each has in a repository.</summary>
public interface ILanguageService
{
    /// <summary>
    /// Every language plug-in of this build, in the order they are installed, with how many of its files the last commit of
    /// the branch checked out under the folder holds.
    /// </summary>
    IReadOnlyList<LanguageReport> Show(string folder);

    /// <summary>
    /// The files of one language that a path pattern of a rule names (<c>*</c> inside a folder, <c>**</c> across folders, <c>?</c> for
    /// one character), among the files the last commit holds. Nothing when that cannot be told (not a repository, no commit, git
    /// cannot be run, no such language).
    /// </summary>
    PathMatches? FindFiles(string folder, string languageId, string pattern);
}
