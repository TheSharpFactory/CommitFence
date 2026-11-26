using CF.Core.Code;

namespace CF.Core.Rules;

/// <summary>The comparisons available for each kind of property.</summary>
public static class RuleComparisons
{
    private static readonly IReadOnlyList<Comparison> _number = Array.AsReadOnly(new[] { Comparison.Is, Comparison.IsOneOf, Comparison.IsNot, Comparison.AtMost, Comparison.AtLeast });
    private static readonly IReadOnlyList<Comparison> _text = Array.AsReadOnly(new[] { Comparison.Is, Comparison.IsOneOf, Comparison.IsNot, Comparison.Matches });
    private static readonly IReadOnlyList<Comparison> _boolean = Array.AsReadOnly(new[] { Comparison.Is });

    /// <summary>The comparisons offered when writing a condition on a property of this type.</summary>
    public static IReadOnlyList<Comparison> For(PropertyValueType type)
    {
        return type switch
        {
            PropertyValueType.Number => _number,
            PropertyValueType.Text => _text,
            PropertyValueType.Boolean => _boolean,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    internal static string? ObjectKey(Comparison comparison)
    {
        return comparison switch
        {
            Comparison.IsNot => "not",
            Comparison.AtMost => "atMost",
            Comparison.AtLeast => "atLeast",
            Comparison.Matches => "matches",
            Comparison.Is or Comparison.IsOneOf => null,
            _ => null
        };
    }

    internal static Comparison? FromObjectKey(string key)
    {
        return key switch
        {
            "not" => Comparison.IsNot,
            "atMost" => Comparison.AtMost,
            "atLeast" => Comparison.AtLeast,
            "matches" => Comparison.Matches,
            _ => null
        };
    }
}
