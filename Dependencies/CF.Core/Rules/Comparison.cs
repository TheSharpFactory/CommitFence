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
    IsOneOf
}