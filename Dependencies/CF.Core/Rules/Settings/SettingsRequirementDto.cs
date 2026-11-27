using System.Text.Json;
using System.Text.Json.Serialization;

namespace CF.Core.Rules.Settings;

/// <summary>The early version check reads just the last value of the requires key, whatever its type.</summary>
internal sealed class SettingsRequirementDto
{
    [JsonPropertyName("requires")]
    public JsonElement Requires { get; set; }
}