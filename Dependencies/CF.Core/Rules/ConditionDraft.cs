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
    {
        PropertyName = propertyName;
        Values = values;
        IsOneOf = isOneOf;
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
    public bool IsOneOf { get; }
}