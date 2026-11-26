using CF.Core.Code;
using CF.Core.Rules;

namespace CF.Core.Review;

/// <summary>
/// A line of text that is the same for two rules exactly when they are defined the same way, so that findings of the
/// same rule ID from two different trees are told apart when the trees define the rule differently.
/// </summary>
internal static class RuleFingerprint
{
    private const char _separator = '\u001f';

    public static string Of(ReviewedRule? rule)
    {
        if(rule == null)
            return string.Empty;

        return Of(rule.Team) + _separator + Of(rule.File);
    }

    private static string Of(RuleDefinition? rule)
    {
        if(rule == null)
            return string.Empty;

        var parts = new List<string>
        {
            rule.Id, rule.Language, rule.ArtifactType, rule.Severity.ToString(), rule.Title, rule.Forbid.ToString(), rule.Enabled.ToString(),
            rule.Message ?? string.Empty, rule.Why ?? string.Empty, string.Join(",", rule.Paths),
            Of(rule.Where), Of(rule.Require)
        };
        return string.Join(_separator, parts);
    }

    private static string Of(IReadOnlyList<RuleCondition> conditions)
    {
        return string.Join(";", conditions.Select(condition =>
            $"{condition.PropertyName} {condition.Comparison} {string.Join(",", condition.ExpectedValues.Select(value => value.ToString()))}"));
    }

    private static string Of(FileRule? rule)
    {
        if(rule == null)
            return string.Empty;

        var places = rule.Places.Select(place => $"{place.Pattern}={place.Setting}");
        return rule.Id + _separator + rule.Title + _separator + string.Join(";", places);
    }
}
