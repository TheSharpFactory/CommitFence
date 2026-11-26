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
        return $"{condition.PropertyName} {ComparisonWords(condition.Comparison, condition.IsList)} {values}";
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

    /// <summary>The words for a comparison in a rule sentence.</summary>
    public static string ComparisonWords(Comparison comparison, bool isList = false)
    {
        return comparison switch
        {
            Comparison.IsOneOf => "is one of",
            Comparison.IsNot => isList ? "is none of" : "is not",
            Comparison.AtMost => "is at most",
            Comparison.AtLeast => "is at least",
            Comparison.Matches => "matches",
            Comparison.Is => "is",
            _ => "is"
        };
    }

    private static string Spoken(RuleCondition condition)
    {
        var values = string.Join(", ", condition.ExpectedValues);
        var comparison = ComparisonWords(condition.Comparison, condition.IsList);
        return $"{RuleWords.Of(condition.PropertyName)} {comparison} {values}";
    }
}