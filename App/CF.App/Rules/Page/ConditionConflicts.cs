using System.Globalization;
using CF.Core.Rules;

namespace CF.App.Rules.Page;

/// <summary>
/// Finds conditions of one clause that no code can satisfy together: on one property, an interval that is empty, a value that is
/// required and excluded, two different values that must each be the one. Only what can be told from the words of the conditions
/// is judged; a condition that is not complete is left out.
/// </summary>
internal static class ConditionConflicts
{
    /// <summary>
    /// The groups of conditions, one group for each property, whose conditions can never all hold.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<ConditionViewModel>> Find(IEnumerable<ConditionViewModel> clause)
    {
        var groups = clause
            .Where(condition => condition.IsComplete && !condition.IsInvalid && condition.PropertyName != null)
            .GroupBy(condition => condition.PropertyName)
            .Where(group => (group.Count() > 1 || group.Any(condition => condition.IsRange)) && !CanHold(group.ToList()))
            .Select(group => (IReadOnlyList<ConditionViewModel>)group.ToList())
            .ToList();

        return groups;
    }

    private static bool CanHold(IReadOnlyList<ConditionViewModel> conditions)
    {
        HashSet<string>? allowed = null;
        var excluded = new HashSet<string>();
        var low = long.MinValue;
        var high = long.MaxValue;
        foreach(var condition in conditions)
        {
            if(condition.IsRange)
            {
                if(TryNumber(condition.Values, 0, out var lower) && TryNumber(condition.Values, 1, out var upper))
                {
                    low = Math.Max(low, lower);
                    high = Math.Min(high, upper);
                }

                continue;
            }

            switch(condition.Comparison)
            {
                case Comparison.Is:
                case Comparison.IsOneOf:
                    allowed = allowed == null ? [.. condition.Values] : [.. allowed.Intersect(condition.Values)];
                    break;
                case Comparison.IsNot:
                    excluded.UnionWith(condition.Values);
                    break;
                case Comparison.AtMost when TryNumber(condition.Values, 0, out var most):
                    high = Math.Min(high, most);
                    break;
                case Comparison.AtLeast when TryNumber(condition.Values, 0, out var least):
                    low = Math.Max(low, least);
                    break;
            }
        }

        if(allowed != null)
            return allowed.Any(value => !excluded.Contains(value) && Within(value, low, high));

        return low <= high && IntegersLeft(low, high, excluded);
    }

    private static bool Within(string value, long low, long high)
    {
        return !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || (number >= low && number <= high);
    }

    private static bool IntegersLeft(long low, long high, HashSet<string> excluded)
    {
        if(low == long.MinValue || high == long.MaxValue)
            return true;

        var inside = excluded.Count(value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= low && number <= high);
        return high - low + 1 > inside;
    }

    private static bool TryNumber(IReadOnlyList<string> values, int index, out long number)
    {
        number = 0;
        return values.Count > index && long.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
    }
}
