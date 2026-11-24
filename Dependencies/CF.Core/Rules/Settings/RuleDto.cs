using System.Text.Json;
using System.Text.Json.Serialization;

namespace CF.Core.Rules.Settings;

/// <summary>
/// One rule as the JSON serializer fills it in, before it is checked against the catalog.
/// </summary>
internal sealed class RuleDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("for")]
    public string? For { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("where")]
    public Dictionary<string, JsonElement>? Where { get; set; }

    [JsonPropertyName("require")]
    public Dictionary<string, JsonElement>? Require { get; set; }

    [JsonPropertyName("forbid")]
    public bool? Forbid { get; set; }

    [JsonPropertyName("paths")]
    public List<string>? Paths { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("why")]
    public string? Why { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownKeys { get; set; }
}