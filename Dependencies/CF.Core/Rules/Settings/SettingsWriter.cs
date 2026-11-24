using System.Text;
using CF.Core.Findings;
using CF.Core.Structure;

namespace CF.Core.Rules.Settings;

/// <summary>
/// Writes the settings as the text of commitfence.json in one stable layout: four spaces of indent, a conditions object
/// or a short list on one line, everything else one item to a line.
/// </summary>
/// <remarks>
/// The same settings always give the same text.
/// </remarks>
internal sealed class SettingsWriter
{
    private const string _indent = "    ";

    private readonly string _newline;

    public SettingsWriter(string newline)
    {
        _newline = newline;
    }

    public string Write(RuleSettings settings)
    {
        var parts = new List<string>();
        if(settings.Requires != null)
            parts.Add(_indent + "\"requires\": " + JsonText.Quote(settings.Requires));

        parts.Add(WriteRules(settings.Rules));
        if(settings.Structure.Modules.Count > 0)
            parts.Add(WriteStructure(settings.Structure));

        return "{" + _newline + string.Join("," + _newline, parts) + _newline + "}" + _newline;
    }

    private string WriteRules(IReadOnlyList<RuleDefinition> rules)
    {
        if(rules.Count == 0)
            return _indent + "\"rules\": []";

        var items = rules.Select(WriteRule);
        return _indent + "\"rules\": [" + _newline + string.Join("," + _newline, items) + _newline + _indent + "]";
    }

    private string WriteRule(RuleDefinition rule)
    {
        var inner = _indent + _indent + _indent;
        var lines = RuleLines(rule).Select(line => inner + line);

        return _indent + _indent + "{" + _newline + string.Join("," + _newline, lines) + _newline + _indent + _indent + "}";
    }

    private static List<string> RuleLines(RuleDefinition rule)
    {
        var lines = new List<string>
        {
            "\"id\": " + JsonText.Quote(rule.Id),
            "\"language\": " + JsonText.Quote(rule.Language),
            "\"for\": " + JsonText.Quote(rule.ArtifactType),
            "\"severity\": " + JsonText.Quote(rule.Severity == Severity.Block ? "block" : "warn")
        };

        if(!rule.Enabled)
            lines.Add("\"enabled\": false");

        lines.Add("\"title\": " + JsonText.Quote(rule.Title));
        AddConditions(lines, "where", rule.Where);
        AddConditions(lines, "require", rule.Require);
        if(rule.Forbid)
            lines.Add("\"forbid\": true");

        if(rule.Paths.Count > 0)
            lines.Add("\"paths\": " + JsonText.InlineArray(rule.Paths.Select(JsonText.Quote)));

        if(rule.Message != null)
            lines.Add("\"message\": " + JsonText.Quote(rule.Message));

        if(rule.Why != null)
            lines.Add("\"why\": " + JsonText.Quote(rule.Why));

        return lines;
    }

    private static void AddConditions(List<string> lines, string key, IReadOnlyList<RuleCondition> conditions)
    {
        if(conditions.Count == 0)
            return;

        var entries = conditions.Select(WriteCondition);
        lines.Add($"\"{key}\": {{ {string.Join(", ", entries)} }}");
    }

    private static string WriteCondition(RuleCondition condition)
    {
        var name = JsonText.Quote(condition.PropertyName);
        if(condition.Comparison == Comparison.IsOneOf)
            return name + ": [" + string.Join(", ", condition.ExpectedValues.Select(JsonText.Value)) + "]";

        return name + ": " + JsonText.Value(condition.ExpectedValues[0]);
    }

    private string WriteStructure(StructureRules structure)
    {
        var builder = new StringBuilder();
        builder.Append(_indent + "\"structure\": {" + _newline);
        builder.Append(_indent + _indent + "\"modules\": {" + _newline);
        builder.Append(string.Join("," + _newline, structure.Modules.Select(WriteModule)) + _newline);
        builder.Append(_indent + _indent + "}," + _newline);
        builder.Append(_indent + _indent + "\"directions\": {" + _newline);
        builder.Append(WriteDirections(structure.Directions) + _newline);
        builder.Append(_indent + _indent + "}" + _newline);
        builder.Append(_indent + "}");

        return builder.ToString();
    }

    private string WriteModule(ModuleDefinition module)
    {
        var selectors = JsonText.InlineArray(module.Selectors.Select(selector => JsonText.Quote(selector.ToString())));
        return _indent + _indent + _indent + JsonText.Quote(module.Name) + ": " + selectors;
    }

    private string WriteDirections(LayerDirections directions)
    {
        var lists = new List<string>();
        if(directions.Allow.Count > 0)
            lists.Add(WritePairs("allow", directions.Allow));

        if(directions.Forbid.Count > 0)
            lists.Add(WritePairs("forbid", directions.Forbid));

        return string.Join("," + _newline, lists);
    }

    private string WritePairs(string key, IReadOnlyList<ModulePair> pairs)
    {
        var lines = pairs.Select(pair => _indent + _indent + _indent + _indent + JsonText.Quote(pair.ToString()));
        return _indent + _indent + _indent + $"\"{key}\": [" + _newline + string.Join("," + _newline, lines) + _newline + _indent + _indent + _indent + "]";
    }
}