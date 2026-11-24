using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The default severity of a rule.
/// </summary>
internal sealed class SarifRuleConfigurationDto
{
    [JsonPropertyName("level")]
    public string? Level { get; set; }
}