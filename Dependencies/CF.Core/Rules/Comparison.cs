namespace CF.Core.Rules;

/// <summary>
/// How a rule compares a property with what it expects. More comparisons are added here, one at a time.
/// </summary>
public enum Comparison
{
    /// <summary>
    /// The property has exactly this value.
    /// </summary>
    Is,

    /// <summary>
    /// The property has one of these values.
    /// </summary>
    IsOneOf,

    /// <summary>The property differs from every listed value.</summary>
    IsNot,

    /// <summary>The property is at or below the upper bound.</summary>
    AtMost,

    /// <summary>The property is at or above the lower bound.</summary>
    AtLeast,

    /// <summary>The whole property value matches a wildcard pattern.</summary>
    Matches
}