using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// A rule the log refers to: its identifier, the severity it has without the .editorconfig, and its tags.
/// </summary>
internal sealed class SarifRuleDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("defaultConfiguration")]
    public SarifRuleConfigurationDto? DefaultConfiguration { get; set; }

    [JsonPropertyName("properties")]
    public SarifRulePropertiesDto? Properties { get; set; }
}