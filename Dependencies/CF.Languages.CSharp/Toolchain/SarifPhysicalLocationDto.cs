using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// A file and a stretch of it.
/// </summary>
internal sealed class SarifPhysicalLocationDto
{
    [JsonPropertyName("artifactLocation")]
    public SarifArtifactLocationDto? ArtifactLocation { get; set; }

    [JsonPropertyName("region")]
    public SarifRegionDto? Region { get; set; }
}