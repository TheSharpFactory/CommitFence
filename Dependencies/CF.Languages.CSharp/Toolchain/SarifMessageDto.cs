using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The text of a diagnostic.
/// </summary>
internal sealed class SarifMessageDto
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}