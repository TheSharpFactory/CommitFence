using System.Text;
using CF.Core.Code;
using CF.Core.Findings;

namespace CF.Cli.Commands;

/// <summary>
/// Writes the rules a repository's own file sets as plain text for the terminal: a heading and a line for each rule in
/// a list, a few lines for one rule.
/// </summary>
internal static class FileRuleTextFormatter
{
    private const string _notUnderstood = "not understood";

    public static IReadOnlyList<string> FormatListing(FileRuleListing listing)
    {
        var lines = new List<string>();
        if(listing.File != null)
            lines.Add(FormatHeading(listing));

        var idWidth = listing.Rules.Count == 0 ? 0 : listing.Rules.Max(rule => rule.Id.Length);
        lines.AddRange(listing.Rules.Select(rule => FormatListLine(rule, idWidth)));
        lines.AddRange(listing.Notes);

        return lines;
    }

    public static string FormatDetail(FileRule rule)
    {
        var builder = new StringBuilder();
        builder.AppendLine(rule.Title == null ? rule.Id : $"{rule.Id}  {rule.Title}");
        AppendLine(builder, "setting", $"'{rule.Setting}'");
        if(rule.Severity != null)
            AppendLine(builder, "severity", FormatSeverity(rule.Severity.Value));

        AppendLine(builder, "status", FormatStatus(rule));
        AppendLine(builder, "file", $"{rule.File}, line {rule.Line}");
        AppendLine(builder, "source", $"set in {rule.File}; the product does not change that file");

        return builder.ToString().TrimEnd();
    }

    private static string FormatHeading(FileRuleListing listing)
    {
        return listing.Rules.Count == 0 ? $"{listing.File}: no rules set" : $"{listing.File}: {listing.Rules.Count} rules";
    }

    private static string FormatListLine(FileRule rule, int idWidth)
    {
        var title = rule.Title == null ? string.Empty : rule.Title + "  ";
        var setting = rule.State == FileRuleState.NotUnderstood ? $"setting '{rule.Setting}', " : string.Empty;

        return $"{rule.Id.PadRight(idWidth)}  {FormatState(rule),-14}  {FormatEffect(rule),-6}  {title}{setting}line {rule.Line}";
    }

    private static string FormatState(FileRule rule)
    {
        switch(rule.State)
        {
            case FileRuleState.On:
                return "on";
            case FileRuleState.Off:
                return "off";
            case FileRuleState.OwnDefault:
                return "default";
            default:
                return _notUnderstood;
        }
    }

    private static string FormatEffect(FileRule rule)
    {
        return rule.Severity == null ? string.Empty : rule.Severity == Severity.Block ? "blocks" : "warns";
    }

    private static string FormatSeverity(Severity severity)
    {
        return severity == Severity.Block ? "block" : "warn";
    }

    private static string FormatStatus(FileRule rule)
    {
        switch(rule.State)
        {
            case FileRuleState.On:
                return "on, applied by the build";
            case FileRuleState.Off:
                return "off, not applied by the build";
            case FileRuleState.OwnDefault:
                return "the rule's own default applies; what it does to a push is not known here";
            default:
                return "not understood by the build, so it has no effect the product can state";
        }
    }

    private static void AppendLine(StringBuilder builder, string label, string text)
    {
        builder.AppendLine($"  {label + ":",-9} {text}");
    }
}
