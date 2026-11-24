using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The mark of a diagnostic that a pragma or an attribute suppressed.
/// </summary>
internal sealed class SarifSuppressionDto
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
}