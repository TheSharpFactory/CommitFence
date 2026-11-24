namespace CF.Core.Code;

/// <summary>
/// Describes one property a rule may ask about: its name, where it comes from, what it holds.
/// </summary>
public sealed class PropertyDescriptor
{
    /// <summary>
    /// Creates a descriptor. When the allowed values are not empty, a text property takes only those.
    /// </summary>
    public PropertyDescriptor(string name, PropertyKind kind, PropertyValueType valueType, string description, IReadOnlyList<string> allowedValues)
    {
        Name = name;
        Kind = kind;
        ValueType = valueType;
        Description = description;
        AllowedValues = allowedValues;
    }

    /// <summary>
    /// The name a rule uses.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether the value comes from syntax or from meaning.
    /// </summary>
    public PropertyKind Kind { get; }

    /// <summary>
    /// The kind of value the property holds.
    /// </summary>
    public PropertyValueType ValueType { get; }

    /// <summary>
    /// What the property means, in a sentence.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// The texts a text property may hold; empty when any text will do.
    /// </summary>
    public IReadOnlyList<string> AllowedValues { get; }
}