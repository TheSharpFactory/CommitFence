using CF.Core.Setup;

namespace CF.Core.Code;

/// <summary>
/// A language the product can review. Each language is a plug-in; nothing outside it knows the language.
/// </summary>
public interface ILanguage
{
    /// <summary>
    /// The identifier rules use for the language, such as csharp.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The name of the language, as shown to people.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The artifact types of the language and their properties.
    /// </summary>
    IArtifactCatalog Catalog { get; }

    /// <summary>
    /// The reader that turns a source file of the language into artifacts.
    /// </summary>
    ICodeReader Reader { get; }

    /// <summary>
    /// The language's own tools, run on the private copy of the tree under review, or nothing when the language has none.
    /// </summary>
    IToolchain? Toolchain { get; }

    /// <summary>The tool that can fix a finding of this language, when one is available.</summary>
    IFixer? Fixer
    {
        get { return null; }
    }

    /// <summary>
    /// Where the language's meaning comes from, or nothing when the language has none.
    /// </summary>
    IMeaningSource? Meaning { get; }

    /// <summary>
    /// The rules the language's tools take from the repository's own files, or nothing when the language has none.
    /// </summary>
    IRepositoryRules? RepositoryRules { get; }

    /// <summary>
    /// Whether the file at this path is a source file of the language, judged by its name alone.
    /// </summary>
    bool OwnsFile(string path);

    /// <summary>
    /// The endings of the file names the language owns, such as .cs, for the people who read what a build covers.
    /// </summary>
    IReadOnlyList<string> FileEndings
    {
        get { return []; }
    }

    /// <summary>
    /// One sentence about what a full review does with the language's files, or nothing when there is nothing to add.
    /// </summary>
    string? FullReviewNote
    {
        get { return null; }
    }

    /// <summary>Reads the source exemptions for one file, including forms handled by the language's tools.</summary>
    IReadOnlyList<SourceExemption> ReadExemptions(CodeFile file)
    {
        return [];
    }

    /// <summary>The language's readiness checks, in order.</summary>
    IReadOnlyList<IDoctorCheck> DoctorChecks
    {
        get { return []; }
    }
}