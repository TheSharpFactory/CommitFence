using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// The kept facts of one project and framework, as the analyzer wrote them.
/// </summary>
internal sealed class KeptPartDto
{
    [JsonPropertyName("part")]
    public string? Part { get; set; }

    [JsonPropertyName("facts")]
    public List<string>? Facts { get; set; }

    [JsonPropertyName("closing")]
    public string? Closing { get; set; }
}