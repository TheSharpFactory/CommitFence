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
}