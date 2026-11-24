using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The tags of a rule: Compiler marks the compiler's own diagnostics.
/// </summary>
internal sealed class SarifRulePropertiesDto
{
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}