using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The rules the log refers to.
/// </summary>
internal sealed class SarifDriverDto
{
    [JsonPropertyName("rules")]
    public List<SarifRuleDto>? Rules { get; set; }
}