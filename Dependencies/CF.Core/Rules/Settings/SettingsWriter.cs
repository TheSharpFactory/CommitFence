using System.Text;
using System.Text.Json;
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

        if(settings.ConfiguredStrictness != null)
            parts.Add(_indent + "\"strictness\": " + WriteStrictness(settings.ConfiguredStrictness));

        parts.Add(WriteRules(settings.Rules));
        if(settings.Structure != StructureRules.None)
            parts.Add(_indent + "\"structure\": " + WriteStructure(settings.Structure));

        return "{" + _newline + string.Join("," + _newline, parts) + _newline + "}" + _newline;
    }

    private static string WriteStrictness(Strictness strictness)
    {
        if(strictness.Preset != StrictnessPreset.Custom)
            return JsonText.Quote(strictness.Preset.ToString().ToLowerInvariant());

        return $"{{ \"error\": {WriteEffect(strictness.Error)}, \"warning\": {WriteEffect(strictness.Warning)}, \"suggestion\": {WriteEffect(strictness.Suggestion)} }}";
    }

    private static string WriteEffect(Severity? effect)
    {
        return JsonText.Quote(effect?.ToString().ToLowerInvariant() ?? "off");
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

    public string ReplaceStructure(string settingsText, StructureRules structure)
    {
        var bytes = Encoding.UTF8.GetBytes(settingsText);
        var reader = new Utf8JsonReader(bytes);
        reader.Read();
        var insertAt = (int)reader.BytesConsumed;
        var hasProperties = false;
        while(reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isStructure = reader.ValueTextEquals("structure"u8);
            reader.Read();
            var start = (int)reader.TokenStartIndex;
            reader.Skip();
            insertAt = (int)reader.BytesConsumed;
            if(isStructure)
                return Encoding.UTF8.GetString(bytes.AsSpan(0, start)) + WriteStructure(structure) + Encoding.UTF8.GetString(bytes.AsSpan(insertAt));

            hasProperties = true;
        }

        var separator = hasProperties ? "," : string.Empty;
        var suffix = Encoding.UTF8.GetString(bytes.AsSpan(insertAt));
        if(!suffix.StartsWith(_newline, StringComparison.Ordinal))
            suffix = _newline + suffix;

        return Encoding.UTF8.GetString(bytes.AsSpan(0, insertAt)) + separator + _newline + _indent
            + "\"structure\": " + WriteStructure(structure) + suffix;
    }

    public string WriteStructure(StructureRules structure)
    {
        var fields = new List<string>
        {
            "\"modules\": " + WriteObject(structure.Modules.Select(WriteModule), _indent + _indent),
            "\"directions\": " + WriteDirections(structure.Directions)
        };
        if(structure.References != null)
            fields.Add("\"references\": " + JsonText.InlineArray(structure.References.Select(rule => rule.GetRawText())));

        if(structure.Layout != null)
            fields.Add("\"layout\": " + JsonText.InlineArray(structure.Layout.Select(rule => rule.GetRawText())));

        return WriteObject(fields, _indent);
    }

    private string WriteObject(IEnumerable<string> fields, string indent)
    {
        var lines = string.Join("," + _newline, fields.Select(field => indent + _indent + field));
        return lines.Length == 0 ? "{}" : "{" + _newline + lines + _newline + indent + "}";
    }

    private string WriteModule(ModuleDefinition module)
    {
        var selectors = module.Selectors.Count == 0 ? "[]" : JsonText.InlineArray(module.Selectors.Select(selector => JsonText.Quote(selector.ToString())));
        return JsonText.Quote(module.Name) + ": " + selectors;
    }

    private string WriteDirections(LayerDirections directions)
    {
        var lists = new List<string>();
        if(directions.HasAllowList)
            lists.Add(WritePairs("allow", directions.Allow));

        if(directions.HasForbidList)
            lists.Add(WritePairs("forbid", directions.Forbid));

        if(directions.NoCycles is { } noCycles)
            lists.Add("\"noCycles\": " + (noCycles ? "true" : "false"));

        return WriteObject(lists, _indent + _indent);
    }

    private string WritePairs(string key, IReadOnlyList<ModulePair> pairs)
    {
        if(pairs.Count == 0)
            return $"\"{key}\": []";

        var lines = pairs.Select(pair => _indent + _indent + _indent + _indent + JsonText.Quote(pair.ToString()));
        return $"\"{key}\": [" + _newline + string.Join("," + _newline, lines) + _newline + _indent + _indent + _indent + "]";
    }
}
