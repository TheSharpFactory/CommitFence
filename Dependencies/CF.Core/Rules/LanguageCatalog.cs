using CF.Core.Code;

namespace CF.Core.Rules;

/// <summary>
/// What a language offers to rules: its artifact types with their properties, value types and whether they need the
/// build. A form or a picker is filled from it.
/// </summary>
public sealed class LanguageCatalog
{
    /// <summary>
    /// Creates the catalog of one language.
    /// </summary>
    public LanguageCatalog(string id, string name, IReadOnlyList<ArtifactType> artifactTypes)
    {
        Id = id;
        Name = name;
        ArtifactTypes = artifactTypes;
    }

    /// <summary>
    /// The identifier rules use, such as csharp.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The name shown to people.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The artifact types, each with its properties.
    /// </summary>
    public IReadOnlyList<ArtifactType> ArtifactTypes { get; }
}