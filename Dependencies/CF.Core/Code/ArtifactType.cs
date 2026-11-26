namespace CF.Core.Code;

/// <summary>
/// Describes one kind of artifact of a language and the properties a rule may ask about.
/// </summary>
public sealed class ArtifactType
{
    /// <summary>
    /// Creates a descriptor of an artifact type.
    /// </summary>
    public ArtifactType(string name, string description, IReadOnlyList<PropertyDescriptor> properties)
        : this(name, description, properties, false)
    {
    }

    /// <summary>
    /// Creates a descriptor of an artifact type. An artifact type that only meaning knows, such as a usage, is found
    /// only in a review that has the build's answers.
    /// </summary>
    public ArtifactType(string name, string description, IReadOnlyList<PropertyDescriptor> properties, bool knownOnlyFromMeaning)
    {
        Name = name;
        Description = description;
        Properties = properties;
        KnownOnlyFromMeaning = knownOnlyFromMeaning;
    }

    /// <summary>
    /// The name a rule uses in its for key, such as method.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// What the artifact is, in a sentence.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// The properties a rule may ask about.
    /// </summary>
    public IReadOnlyList<PropertyDescriptor> Properties { get; }

    /// <summary>
    /// Whether the artifacts of this type exist only in what the build's answers say, so that no rule for them can run
    /// without the build.
    /// </summary>
    public bool KnownOnlyFromMeaning { get; }

    /// <summary>The first CommitFence version that accepts rules for this artifact, when a floor is needed.</summary>
    public string? RuleVersion { get; init; }

    /// <summary>
    /// The descriptor of the property with this name, or nothing.
    /// </summary>
    public PropertyDescriptor? FindProperty(string name)
    {
        foreach(var property in Properties)
        {
            if(property.Name == name)
                return property;
        }

        return null;
    }
}