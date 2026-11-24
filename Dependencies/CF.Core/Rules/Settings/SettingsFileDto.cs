using System.Text.Json;
using System.Text.Json.Serialization;

namespace CF.Core.Rules.Settings;

/// <summary>
/// The settings file as the JSON serializer fills it in. Keys it does not know are caught in UnknownKeys and refused
/// afterwards, with their place.
/// </summary>
internal sealed class SettingsFileDto
{
    [JsonPropertyName("requires")]
    public string? Requires { get; set; }

    [JsonPropertyName("rules")]
    public List<RuleDto>? Rules { get; set; }

    [JsonPropertyName("structure")]
    public StructureDto? Structure { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownKeys { get; set; }
}