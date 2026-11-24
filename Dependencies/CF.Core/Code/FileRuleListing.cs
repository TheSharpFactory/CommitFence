namespace CF.Core.Code;

/// <summary>
/// The rules a language's tools take from the repository's own files, and what could not be read.
/// </summary>
public sealed class FileRuleListing
{
    /// <summary>
    /// Creates a listing.
    /// </summary>
    public FileRuleListing(string languageId, string? file, IReadOnlyList<FileRule> rules, IReadOnlyList<string> notes)
    {
        LanguageId = languageId;
        File = file;
        Rules = rules;
        Notes = notes;
    }

    /// <summary>
    /// The language whose tools read the file.
    /// </summary>
    public string LanguageId { get; }

    /// <summary>
    /// The name of the file the rules come from, or nothing when the repository has none.
    /// </summary>
    public string? File { get; }

    /// <summary>
    /// The rules, in the order of the file.
    /// </summary>
    public IReadOnlyList<FileRule> Rules { get; }

    /// <summary>
    /// Plain lines about what was not read or not found, each ready to print.
    /// </summary>
    public IReadOnlyList<string> Notes { get; }
}
