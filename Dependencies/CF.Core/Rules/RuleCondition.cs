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
    public RuleCondition(string propertyName, Comparison comparison, IReadOnlyList<PropertyValue> expectedValues, bool isList = false)
    {
        PropertyName = propertyName;
        Comparison = comparison;
        ExpectedValues = expectedValues;
        IsList = isList || comparison == Comparison.IsOneOf || expectedValues.Count > 1;
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

    /// <summary>Whether the expected values are written as a list, even when there is only one.</summary>
    public bool IsList { get; }

    /// <summary>
    /// Whether the value of a property satisfies the condition.
    /// </summary>
    public bool IsMetBy(PropertyValue actual)
    {
        return Comparison switch
        {
            Comparison.Is or Comparison.IsOneOf => ExpectedValues.Contains(actual),
            Comparison.IsNot => !ExpectedValues.Contains(actual),
            Comparison.AtMost => actual.Type == PropertyValueType.Number && actual.Number <= ExpectedValues[0].Number,
            Comparison.AtLeast => actual.Type == PropertyValueType.Number && actual.Number >= ExpectedValues[0].Number,
            Comparison.Matches => actual.Type == PropertyValueType.Text && RuleWildcard.Matches(actual.Text, ExpectedValues[0].Text),
            _ => false
        };
    }
}