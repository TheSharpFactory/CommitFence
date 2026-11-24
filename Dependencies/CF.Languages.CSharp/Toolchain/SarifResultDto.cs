using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// One diagnostic. Its level is the severity after the .editorconfig and the build settings were applied. A suppressed
/// diagnostic carries a suppression.
/// </summary>
internal sealed class SarifResultDto
{
    [JsonPropertyName("ruleId")]
    public string? RuleId { get; set; }

    [JsonPropertyName("level")]
    public string? Level { get; set; }

    [JsonPropertyName("message")]
    public SarifMessageDto? Message { get; set; }

    [JsonPropertyName("locations")]
    public List<SarifLocationDto>? Locations { get; set; }

    [JsonPropertyName("suppressions")]
    public List<SarifSuppressionDto>? Suppressions { get; set; }
}