using System.Text.Json;
using System.Text.Json.Serialization;

namespace CF.Core.Rules.Settings;

/// <summary>
/// The structure part of the settings file as the JSON serializer fills it in. Keys it does not know are caught in
/// UnknownKeys and refused afterwards, with their place.
/// </summary>
internal sealed class StructureDto
{
    [JsonPropertyName("modules")]
    public Dictionary<string, List<string>>? Modules { get; set; }

    [JsonPropertyName("directions")]
    public DirectionsDto? Directions { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownKeys { get; set; }
}