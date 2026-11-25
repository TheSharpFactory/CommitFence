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
        foreach(var rule in listing.Rules)
        {
            lines.Add(FormatListLine(rule, idWidth));
            lines.AddRange(rule.ExceptionLines().Select(line => new string(' ', idWidth + 2) + char.ToLowerInvariant(line[0]) + line.Substring(1)));
        }

        lines.AddRange(listing.Notes);

        return lines;
    }

    public static string FormatDetail(FileRule rule)
    {
        var builder = new StringBuilder();
        builder.AppendLine(rule.Title == null ? rule.Id : $"{rule.Id}  {rule.Title}");
        if(rule.Main != null)
            AppendLine(builder, "setting", $"'{rule.Setting}'");
        if(rule.Severity != null)
            AppendLine(builder, "severity", FormatSeverity(rule.Severity.Value));

        AppendLine(builder, "status", FormatStatus(rule));
        AppendLine(builder, "file", rule.Line == null ? rule.File : $"{rule.File}, line {rule.Line}");
        AppendLine(builder, "source", $"set in {rule.File}; the product does not change that file");

        if(rule.Exceptions.Count > 0)
        {
            var count = rule.Places.Count();
            var scope = rule.Main == null ? ", for some files only" : string.Empty;
            var order = count > 1 ? "; a later line overrides an earlier one" : string.Empty;
            AppendLine(builder, "lines", $"{count} {(count == 1 ? "line sets" : "lines set")} this rule{scope}{order}");
            foreach(var place in rule.Places)
                builder.AppendLine($"            {place.Effect.ToLowerInvariant()} for {rule.Scope(place)}, line {place.Line} ('{place.Setting}')");
        }

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

        var location = rule.Line == null ? string.Empty : $"line {rule.Line}";
        return $"{rule.Id.PadRight(idWidth)}  {FormatState(rule),-14}  {FormatEffect(rule),-6}  {title}{setting}{location}".TrimEnd();
    }

    private static string FormatState(FileRule rule)
    {
        if(rule.Main == null)
            return "not set";

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
        if(rule.Exceptions.Count > 0)
        {
            var effect = rule.Main?.Effect.ToLowerInvariant() ?? "not set";
            var applied = BuildStatus(rule);
            return $"{effect} for all {rule.Language} files, {rule.ExceptionCount}, {applied}";
        }

        switch(rule.State)
        {
            case FileRuleState.On:
                return "on, applied by the build";
            case FileRuleState.Off:
                return rule.Main?.IsOffByStrictness == true ? "off under the current strictness" : "off, not applied by the build";
            case FileRuleState.OwnDefault:
                return "the rule's own default applies; what it does to a push is not known here";
            default:
                return "not understood by the build, so it has no effect the product can state";
        }
    }

    private static string BuildStatus(FileRule rule)
    {
        if(rule.Main == null)
            return "applied by the build where set";

        if(rule.Main.IsOffByStrictness)
            return "set by the current strictness";

        return rule.State == FileRuleState.Off ? "not applied by the build" : "applied by the build";
    }

    private static void AppendLine(StringBuilder builder, string label, string text)
    {
        builder.AppendLine($"  {label + ":",-9} {text}");
    }
}