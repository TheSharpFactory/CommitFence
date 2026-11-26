namespace CF.Core.Rules;

/// <summary>
/// Tells which conditions on one property the settings format can hold in one clause: one condition, or "is at least"
/// together with "is at most" as a range. Anything else would lose a condition when written.
/// </summary>
public static class ClauseShape
{
    /// <summary>
    /// The sentence that refuses the clause, or nothing when the settings format can hold it. The other clause is the one
    /// a condition can be moved to.
    /// </summary>
    public static string? Refusal(IEnumerable<ConditionDraft> clause, string otherClause)
    {
        foreach(var group in clause.GroupBy(condition => condition.PropertyName, StringComparer.Ordinal))
        {
            if(!CanBeWritten(group.Select(condition => condition.Comparison).ToList()))
                return $"A clause can hold one condition per property, or \"is at least\" with \"is at most\" as a range. Join the other conditions on {group.Key} into one, or move one to {otherClause}.";
        }

        return null;
    }

    private static bool CanBeWritten(List<Comparison> comparisons)
    {
        if(comparisons.Count <= 1)
            return true;

        return comparisons.Count == 2 && comparisons.Contains(Comparison.AtLeast) && comparisons.Contains(Comparison.AtMost);
    }
}
