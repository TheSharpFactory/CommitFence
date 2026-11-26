using CF.Core.Rules;

namespace CF.App.Rules.Page;

/// <summary>
/// One condition as the page holds it: a property, a comparison and its values. A range, "at least" with "at most" on one property,
/// is one of these with both bounds as its two values.
/// </summary>
internal sealed record ConditionShape(string PropertyName, Comparison Comparison, bool IsList, IReadOnlyList<string> Values, bool IsRange);

/// <summary>
/// Reads the conditions of a clause the way the settings file holds them: a range is one object with "atLeast" and "atMost", so the
/// page draws it as one token, and the two conditions the rule keeps come back out of it when the page is written.
/// </summary>
internal static class ConditionRanges
{
    /// <summary>
    /// The conditions of a clause as the page holds them: the two bounds of a property are one range, standing where the first of
    /// them stood.
    /// </summary>
    public static IReadOnlyList<ConditionShape> Merge(IReadOnlyList<RuleCondition> conditions)
    {
        var shapes = new List<ConditionShape>();
        var used = new HashSet<RuleCondition>();
        foreach(var condition in conditions)
        {
            if(used.Contains(condition))
                continue;

            var other = Partner(conditions, condition, used);
            if(other == null)
            {
                shapes.Add(new ConditionShape(condition.PropertyName, condition.Comparison, condition.IsList, Values(condition), false));
                continue;
            }

            used.Add(other);
            var lower = condition.Comparison == Comparison.AtLeast ? condition : other;
            var upper = condition.Comparison == Comparison.AtLeast ? other : condition;
            shapes.Add(new ConditionShape(condition.PropertyName, Comparison.AtLeast, false, [.. Values(lower), .. Values(upper)], true));
        }

        return shapes;
    }

    /// <summary>
    /// The conditions a page condition stands for: one, or the two bounds of a range.
    /// </summary>
    public static IEnumerable<ConditionDraft> Drafts(string propertyName, Comparison comparison, bool isList, IReadOnlyList<string> values, bool isRange)
    {
        if(!isRange)
        {
            yield return new ConditionDraft(propertyName, values, comparison, isList);
            yield break;
        }

        yield return new ConditionDraft(propertyName, [values[0]], Comparison.AtLeast);
        yield return new ConditionDraft(propertyName, [values[1]], Comparison.AtMost);
    }

    private static RuleCondition? Partner(IReadOnlyList<RuleCondition> conditions, RuleCondition condition, HashSet<RuleCondition> used)
    {
        if(condition.Comparison is not (Comparison.AtLeast or Comparison.AtMost))
            return null;

        var wanted = condition.Comparison == Comparison.AtLeast ? Comparison.AtMost : Comparison.AtLeast;
        return conditions.FirstOrDefault(other => other != condition && !used.Contains(other) && other.PropertyName == condition.PropertyName && other.Comparison == wanted);
    }

    private static List<string> Values(RuleCondition condition)
    {
        return condition.ExpectedValues.Select(value => value.ToString()).ToList();
    }
}
