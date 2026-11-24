namespace CF.Core.Code;

/// <summary>
/// One thing in a source file a rule can be attached to: a method, an if statement, a class and so on.
/// </summary>
public sealed class Artifact
{
    /// <summary>
    /// Creates an artifact. The type is the name of an artifact type of the language's catalog.
    /// </summary>
    public Artifact(string type, string language, string file, SourceSpan span, Artifact? parent, IReadOnlyList<Property> properties, ArtifactReach? reach = null)
    {
        Type = type;
        Language = language;
        File = file;
        Span = span;
        Parent = parent;
        Properties = properties;
        Reach = reach ?? ArtifactReach.OfSpan(span);
    }

    /// <summary>
    /// The name of the artifact type, such as method.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// The identifier of the language, such as csharp.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// The path of the file, relative to the repository, with forward slashes.
    /// </summary>
    public string File { get; }

    /// <summary>
    /// Where the artifact is in the file.
    /// </summary>
    public SourceSpan Span { get; }

    /// <summary>
    /// The artifact this one is immediately declared in, if any.
    /// </summary>
    public Artifact? Parent { get; }

    /// <summary>
    /// Every property of the artifact.
    /// </summary>
    public IReadOnlyList<Property> Properties { get; }

    /// <summary>
    /// What the artifact's properties were read from: its own span and, beside it, the spans they depend on. A review
    /// keeps a finding on the artifact when a change touched any of it.
    /// </summary>
    public ArtifactReach Reach { get; }

    /// <summary>
    /// The property with this name, or nothing when the artifact has none.
    /// </summary>
    public Property? FindProperty(string name)
    {
        foreach(var property in Properties)
        {
            if(property.Name == name)
                return property;
        }

        return null;
    }
}