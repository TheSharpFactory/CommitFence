namespace CF.Core.Code;

/// <summary>
/// One property of one artifact, with the value the language's reader found.
/// </summary>
public sealed class Property
{
    /// <summary>
    /// Creates a property.
    /// </summary>
    public Property(string name, PropertyKind kind, PropertyValue value)
    {
        Name = name;
        Kind = kind;
        Value = value;
    }

    /// <summary>
    /// The name a rule uses for the property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether the value comes from syntax or from meaning.
    /// </summary>
    public PropertyKind Kind { get; }

    /// <summary>
    /// The value.
    /// </summary>
    public PropertyValue Value { get; }
}