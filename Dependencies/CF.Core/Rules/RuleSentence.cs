namespace CF.Core.Rules;

/// <summary>
/// A rule said in words, as the command line says it: the conditions one by one, and the whole rule as one sentence.
/// </summary>
public static class RuleSentence
{
    /// <summary>
    /// One condition: the property, then "is" or "is one of", then the values.
    /// </summary>
    public static string Condition(RuleCondition condition)
    {
        var values = string.Join(", ", condition.ExpectedValues);
        if(condition.Comparison == Comparison.IsOneOf)
            return $"{condition.PropertyName} is one of {values}";

        return $"{condition.PropertyName} is {values}";
    }

    /// <summary>
    /// The whole rule in the words a person reads, as the app shows it: For if statement, where statements in body is 1, require
    /// braces is false. A rule that forbids says that no such artifact may exist.
    /// </summary>
    public static string Of(RuleDefinition rule)
    {
        var parts = new List<string> { "For " + RuleWords.Of(rule.ArtifactType) };
        if(rule.Where.Count > 0)
            parts.Add("where " + string.Join(" and ", rule.Where.Select(Spoken)));

        if(rule.Forbid)
            parts.Add("no such artifact may exist");
        else if(rule.Require.Count > 0)
            parts.Add("require " + string.Join(" and ", rule.Require.Select(Spoken)));

        return string.Join(", ", parts) + ".";
    }

    private static string Spoken(RuleCondition condition)
    {
        var values = string.Join(", ", condition.ExpectedValues);
        var comparison = condition.Comparison == Comparison.IsOneOf ? "is one of" : "is";
        return $"{RuleWords.Of(condition.PropertyName)} {comparison} {values}";
    }
}