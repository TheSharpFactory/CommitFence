using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The file, as a file URI.
/// </summary>
internal sealed class SarifArtifactLocationDto
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
}