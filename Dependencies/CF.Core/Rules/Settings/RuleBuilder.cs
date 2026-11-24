using System.Text.Json;
using CF.Core.Code;
using CF.Core.Findings;

namespace CF.Core.Rules.Settings;

/// <summary>
/// Checks one rule of the settings file against the languages and their catalogs, and builds the rule. Anything wrong
/// is a settings error at the place of the key.
/// </summary>
internal sealed class RuleBuilder
{
    private readonly IReadOnlyList<ILanguage> _languages;
    private readonly JsonPositions _positions;
    private readonly ConditionReader _conditionReader;

    public RuleBuilder(IReadOnlyList<ILanguage> languages, JsonPositions positions)
    {
        _languages = languages;
        _positions = positions;
        _conditionReader = new ConditionReader(positions);
    }

    public RuleDefinition Build(RuleDto dto, string path)
    {
        RejectUnknownKeys(dto.UnknownKeys, path);
        var id = RequireText(dto.Id, "id", path);
        var languageId = RequireText(dto.Language, "language", path);
        var typeName = RequireText(dto.For, "for", path);
        var title = RequireText(dto.Title, "title", path);
        var severity = ReadSeverity(dto.Severity, path);
        var artifactType = FindArtifactType(languageId, typeName, path);
        var where = _conditionReader.Read(dto.Where, artifactType, path + ".where");
        var require = _conditionReader.Read(dto.Require, artifactType, path + ".require");
        var forbid = ReadForbid(dto.Forbid, require, path);
        var paths = dto.Paths ?? new List<string>();

        return new RuleDefinition(id, languageId, typeName, severity, title, where, require, forbid, paths, dto.Message, dto.Why, dto.Enabled ?? true);
    }

    private void RejectUnknownKeys(Dictionary<string, JsonElement>? unknownKeys, string path)
    {
        if(unknownKeys == null || unknownKeys.Count == 0)
            return;

        var key = unknownKeys.Keys.First();
        throw _positions.ErrorAt(JsonPositions.Join(path, key), $"The key '{key}' is not known in a rule.");
    }

    private string RequireText(string? value, string key, string path)
    {
        if(value == null)
            throw _positions.ErrorAt(path, $"The rule is missing the key '{key}'.");

        if(string.IsNullOrWhiteSpace(value))
            throw _positions.ErrorAt(JsonPositions.Join(path, key), $"The key '{key}' must not be empty.");

        return value;
    }

    private Severity ReadSeverity(string? text, string path)
    {
        var value = RequireText(text, "severity", path);
        if(value == "block")
            return Severity.Block;

        if(value == "warn")
            return Severity.Warn;

        throw _positions.ErrorAt(JsonPositions.Join(path, "severity"), $"'{value}' is not a severity. Use block or warn.");
    }

    private ArtifactType FindArtifactType(string languageId, string typeName, string path)
    {
        var language = _languages.FirstOrDefault(candidate => candidate.Id == languageId);
        if(language == null)
            throw _positions.ErrorAt(JsonPositions.Join(path, "language"), $"'{languageId}' is not a language the product has. Known languages: {string.Join(", ", _languages.Select(candidate => candidate.Id))}.");

        var artifactType = language.Catalog.FindArtifactType(typeName);
        if(artifactType == null)
            throw _positions.ErrorAt(JsonPositions.Join(path, "for"), $"'{typeName}' is not an artifact type of {languageId}. Known types: {string.Join(", ", language.Catalog.ArtifactTypes.Select(candidate => candidate.Name))}.");

        return artifactType;
    }

    private bool ReadForbid(bool? forbid, IReadOnlyList<RuleCondition> require, string path)
    {
        if(forbid == false)
            throw _positions.ErrorAt(JsonPositions.Join(path, "forbid"), "'forbid' can only be true; leave it out to use 'require'.");

        if(forbid == true && require.Count > 0)
            throw _positions.ErrorAt(JsonPositions.Join(path, "forbid"), "A rule has either 'require' or 'forbid', not both.");

        if(forbid == null && require.Count == 0)
            throw _positions.ErrorAt(path, "The rule needs either 'require' or 'forbid'.");

        return forbid == true;
    }
}