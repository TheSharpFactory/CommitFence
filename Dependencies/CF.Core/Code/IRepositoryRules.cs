namespace CF.Core.Code;

/// <summary>
/// The rules a language's tools take from the repository's own files, such as the .editorconfig of C#.
/// </summary>
/// <remarks>
/// The product lists them and never writes them.
/// </remarks>
public interface IRepositoryRules
{
    /// <summary>
    /// The rules the files of the repository folder set. Throws a <see cref="RepositoryRulesException" /> when a file
    /// exists and cannot be read.
    /// </summary>
    FileRuleListing Read(string repositoryFolder);
}
