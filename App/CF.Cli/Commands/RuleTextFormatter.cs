using System.Text;
using CF.Core.Findings;
using CF.Core.Rules;

namespace CF.Cli.Commands;

/// <summary>
/// Writes rules as plain text for the terminal: one line for a list, a few lines for one rule.
/// </summary>
internal static class RuleTextFormatter
{
    public static string FormatListLine(RuleDefinition rule, int idWidth)
    {
        return $"{rule.Id.PadRight(idWidth)}  {FormatSeverity(rule.Severity),-5}  {rule.Language}  {rule.ArtifactType}  {rule.Title}{(rule.Enabled ? string.Empty : "  (off)")}";
    }

    public static string FormatDetail(RuleDefinition rule)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{rule.Id}  {rule.Title}");
        AppendLine(builder, "severity", FormatSeverity(rule.Severity));
        if(!rule.Enabled)
            AppendLine(builder, "status", "off, not evaluated in a review");

        AppendLine(builder, "language", rule.Language);
        AppendLine(builder, "for", rule.ArtifactType);
        AppendConditions(builder, "where", rule.Where);
        AppendConditions(builder, "require", rule.Require);
        if(rule.Forbid)
            AppendLine(builder, "forbid", "no such artifact may exist");

        if(rule.Paths.Count > 0)
            AppendLine(builder, "paths", string.Join(", ", rule.Paths));

        if(rule.Message != null)
            AppendLine(builder, "message", rule.Message);

        if(rule.Why != null)
            AppendLine(builder, "why", rule.Why);

        return builder.ToString().TrimEnd();
    }

    private static string FormatSeverity(Severity severity)
    {
        return severity == Severity.Block ? "block" : "warn";
    }

    private static void AppendConditions(StringBuilder builder, string label, IReadOnlyList<RuleCondition> conditions)
    {
        if(conditions.Count == 0)
            return;

        AppendLine(builder, label, FormatCondition(conditions[0]));
        for(var index = 1; index < conditions.Count; index++)
            AppendLine(builder, string.Empty, FormatCondition(conditions[index]));
    }

    private static string FormatCondition(RuleCondition condition)
    {
        var values = string.Join(", ", condition.ExpectedValues);
        if(condition.Comparison == Comparison.IsOneOf)
            return $"{condition.PropertyName} is one of {values}";

        return $"{condition.PropertyName} is {values}";
    }

    private static void AppendLine(StringBuilder builder, string label, string text)
    {
        var heading = label.Length == 0 ? string.Empty : label + ":";
        builder.AppendLine($"  {heading,-9} {text}");
    }
}