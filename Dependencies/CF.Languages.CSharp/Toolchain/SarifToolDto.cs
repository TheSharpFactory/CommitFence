using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The tool that wrote the log.
/// </summary>
internal sealed class SarifToolDto
{
    [JsonPropertyName("driver")]
    public SarifDriverDto? Driver { get; set; }
}