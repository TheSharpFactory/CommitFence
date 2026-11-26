namespace CF.Core.Rules;

/// <summary>
/// One condition as a person gives it, before it is checked: a property name and the values as typed. The service turns
/// the values into numbers or booleans where the property asks for them.
/// </summary>
public sealed class ConditionDraft
{
    /// <summary>
    /// Creates a draft. With isOneOf the property may have any of the values; otherwise it must be the one value.
    /// </summary>
    public ConditionDraft(string propertyName, IReadOnlyList<string> values, bool isOneOf)
        : this(propertyName, values, isOneOf ? Comparison.IsOneOf : Comparison.Is, isOneOf)
    {
    }

    /// <summary>Creates a draft with an explicit comparison and list form.</summary>
    public ConditionDraft(string propertyName, IReadOnlyList<string> values, Comparison comparison, bool isList = false)
    {
        PropertyName = propertyName;
        Values = values;
        Comparison = comparison;
        IsList = isList;
    }

    /// <summary>
    /// The property that is compared.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// The values, as typed.
    /// </summary>
    public IReadOnlyList<string> Values { get; }

    /// <summary>
    /// Whether the property may have any of the values.
    /// </summary>
    public bool IsOneOf
    {
        get { return Comparison == Comparison.IsOneOf; }
    }

    /// <summary>How the property is compared.</summary>
    public Comparison Comparison { get; }

    /// <summary>Whether to write the expected values as a list.</summary>
    public bool IsList { get; }
}