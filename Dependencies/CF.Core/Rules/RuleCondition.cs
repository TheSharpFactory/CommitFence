using CF.Core.Code;

namespace CF.Core.Rules;

/// <summary>
/// One comparison of a catalogued property with the values a rule expects.
/// </summary>
public sealed class RuleCondition
{
    /// <summary>
    /// Creates a condition.
    /// </summary>
    public RuleCondition(string propertyName, Comparison comparison, IReadOnlyList<PropertyValue> expectedValues)
    {
        PropertyName = propertyName;
        Comparison = comparison;
        ExpectedValues = expectedValues;
    }

    /// <summary>
    /// The property that is compared.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// How it is compared.
    /// </summary>
    public Comparison Comparison { get; }

    /// <summary>
    /// The value, or the values, it is compared with.
    /// </summary>
    public IReadOnlyList<PropertyValue> ExpectedValues { get; }

    /// <summary>
    /// Whether the value of a property satisfies the condition.
    /// </summary>
    public bool IsMetBy(PropertyValue actual)
    {
        foreach(var expected in ExpectedValues)
        {
            if(expected.Equals(actual))
                return true;
        }

        return false;
    }
}