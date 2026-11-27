namespace CF.Core.Rules;

/// <summary>Checks IDs when creating a rule. Existing settings keep the IDs they already use.</summary>
public static class RuleId
{
    /// <summary>Finds a saved rule whose ID would conflict with a new one.</summary>
    public static RuleDefinition? FindTaken(string id, IReadOnlyList<RuleDefinition> rules)
    {
        return rules.FirstOrDefault(rule => string.Equals(id, rule.Id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns the reason a new ID is refused, or null if it is available.</summary>
    public static string? NewRuleProblem(string id, IReadOnlyList<RuleDefinition> rules)
    {
        if(id.Length == 0)
            return "the rule needs an ID.";

        if(id.Any(char.IsWhiteSpace))
            return "an ID has no spaces.";

        if(!id.All(letter => char.IsAsciiLetterOrDigit(letter) || letter is '-' or '_' or '.'))
            return "an ID has only letters, digits, -, _ and .";

        var taken = FindTaken(id, rules);
        return taken == null ? null : $"{taken.Id} is already the ID of “{taken.Title}”.";
    }
}
