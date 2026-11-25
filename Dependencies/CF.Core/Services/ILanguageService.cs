namespace CF.Core.Services;

/// <summary>Tells which languages this build can review and what each has in a repository.</summary>
public interface ILanguageService
{
    /// <summary>
    /// Every language plug-in of this build, in the order they are installed, with how many of its files the last commit of
    /// the branch checked out under the folder holds.
    /// </summary>
    IReadOnlyList<LanguageReport> Show(string folder);
}
