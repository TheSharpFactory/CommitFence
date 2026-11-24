namespace CF.Core.Code;

/// <summary>
/// The artifact types a language has, with their properties. Rules are checked against it.
/// </summary>
public interface IArtifactCatalog
{
    /// <summary>
    /// Every artifact type of the language.
    /// </summary>
    IReadOnlyList<ArtifactType> ArtifactTypes { get; }

    /// <summary>
    /// The artifact type with this name, or nothing.
    /// </summary>
    ArtifactType? FindArtifactType(string name);
}