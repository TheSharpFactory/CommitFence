using System.Text.Json;
using System.Text.Json.Serialization;

namespace CF.Core.Rules.Settings;

/// <summary>
/// The directions between modules as the JSON serializer fills them in: each one written as from -> to.
/// </summary>
internal sealed class DirectionsDto
{
    [JsonPropertyName("allow")]
    public List<string>? Allow { get; set; }

    [JsonPropertyName("forbid")]
    public List<string>? Forbid { get; set; }

    [JsonPropertyName("noCycles")]
    public bool? NoCycles { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownKeys { get; set; }
}
