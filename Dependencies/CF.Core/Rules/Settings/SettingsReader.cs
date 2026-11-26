using System.Text;
using System.Text.Json;
using CF.Core.Code;
using CF.Core.Setup;

namespace CF.Core.Rules.Settings;

/// <summary>
/// Reads the text of commitfence.json strictly: an unknown key, a key given twice, a wrong type or value, or broken
/// JSON is refused with its line and column. It reads text and touches no file.
/// </summary>
public sealed class SettingsReader
{
    private readonly IReadOnlyList<ILanguage> _languages;

    /// <summary>
    /// Creates a reader that checks rules against the catalogs of these languages.
    /// </summary>
    public SettingsReader(IReadOnlyList<ILanguage> languages)
    {
        _languages = languages;
    }

    /// <summary>
    /// The settings the text holds. Throws a <see cref="SettingsException" /> when the text is wrong.
    /// </summary>
    public RuleSettings Read(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var positions = new SettingsScanner(bytes).Scan();
        positions.RejectNulls();
        var file = Deserialize(bytes, positions);
        RejectUnknownKeys(file, positions);

        return BuildSettings(file, positions);
    }

    /// <summary>
    /// The value of the requires key, read leniently: nothing else in the text is interpreted, so a file written for a
    /// newer version still gives its answer.
    /// </summary>
    /// <remarks>
    /// Returns nothing when the text is broken, has no such key, or the value is not a version; the strict read says
    /// what is wrong.
    /// </remarks>
    public static string? ReadRequires(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if(document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("requires", out var value) || value.ValueKind != JsonValueKind.String)
                return null;

            var requires = value.GetString();
            return requires != null && VersionRequirement.IsValid(requires) ? requires : null;
        }
        catch(JsonException)
        {
            return null;
        }
    }

    internal string? HigherFeatureVersion(RuleSettings settings)
    {
        var minimum = settings.Requires == null ? null : ProductVersion.TryParse(settings.Requires);
        string? raised = null;
        foreach(var required in settings.Rules.SelectMany(FeatureVersions))
        {
            var version = ProductVersion.TryParse(required)!;
            if(minimum != null && minimum.CompareTo(version) >= 0)
                continue;

            minimum = version;
            raised = required;
        }

        return raised;
    }

    private IEnumerable<string> FeatureVersions(RuleDefinition rule)
    {
        var artifact = _languages.FirstOrDefault(language => language.Id == rule.Language)?.Catalog.FindArtifactType(rule.ArtifactType);
        if(artifact?.RuleVersion is { } artifactVersion)
            yield return artifactVersion;

        foreach(var condition in rule.Where.Concat(rule.Require))
        {
            if(RuleComparisons.ObjectKey(condition.Comparison) != null)
                yield return "1.0.0.6";

            if(artifact?.FindProperty(condition.PropertyName)?.RuleVersion is { } propertyVersion)
                yield return propertyVersion;
        }
    }

    private static SettingsFileDto Deserialize(byte[] bytes, JsonPositions positions)
    {
        try
        {
            var file = JsonSerializer.Deserialize(bytes, SettingsJsonContext.Default.SettingsFileDto);
            if(file == null)
                throw positions.ErrorAt(string.Empty, "The file must hold one JSON object.");

            return file;
        }
        catch(JsonException exception)
        {
            throw WrongType(exception, positions);
        }
    }

    private static SettingsException WrongType(JsonException exception, JsonPositions positions)
    {
        var path = (exception.Path ?? "$").TrimStart('$').TrimStart('.');
        if(path.Length == 0)
            return positions.ErrorAt(path, "The file must hold one JSON object.");

        var message = $"The value of '{path}' has the wrong type.";
        var error = positions.ErrorAt(path, message);
        if(error.Line != null)
            return error;

        return positions.ErrorAtLine(exception.LineNumber ?? 0, exception.BytePositionInLine ?? 0, message);
    }

    private static void RejectUnknownKeys(SettingsFileDto file, JsonPositions positions)
    {
        if(file.UnknownKeys == null || file.UnknownKeys.Count == 0)
            return;

        var key = file.UnknownKeys.Keys.First();
        throw positions.ErrorAt(JsonPositions.Join(string.Empty, key), $"The key '{key}' is not known in the settings file.");
    }

    private RuleSettings BuildSettings(SettingsFileDto file, JsonPositions positions)
    {
        if(file.Requires != null && !VersionRequirement.IsValid(file.Requires))
            throw positions.ErrorAt("requires", $"The value of 'requires' must be a version of one to four numbers separated by dots, such as 1.0.0.0, not '{file.Requires}'.");

        var builder = new RuleBuilder(_languages, positions);
        var rules = new List<RuleDefinition>();
        var dtos = file.Rules ?? new List<RuleDto>();
        for(var index = 0; index < dtos.Count; index++)
        {
            var path = "rules[" + index + "]";
            var dto = (RuleDto?)dtos[index];
            if(dto == null)
                throw positions.ErrorAt(path, "A rule must be a JSON object.");

            var rule = builder.Build(dto, path);
            RejectDuplicateId(rules, rule, path, positions);
            rules.Add(rule);
        }

        var structure = new StructureReader(positions).Read(file.Structure, "structure");

        return new RuleSettings(file.Requires, rules, structure, new StrictnessReader(positions).Read(file.Strictness));
    }

    private static void RejectDuplicateId(List<RuleDefinition> earlier, RuleDefinition rule, string path, JsonPositions positions)
    {
        if(earlier.Any(candidate => candidate.Id == rule.Id))
            throw positions.ErrorAt(path + ".id", $"The rule id '{rule.Id}' is already used by an earlier rule.");
    }
}