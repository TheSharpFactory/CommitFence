using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Where a diagnostic is.
/// </summary>
internal sealed class SarifLocationDto
{
    [JsonPropertyName("physicalLocation")]
    public SarifPhysicalLocationDto? PhysicalLocation { get; set; }
}