using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// One run of the compiler: its results and the rules that explain them.
/// </summary>
internal sealed class SarifRunDto
{
    [JsonPropertyName("results")]
    public List<SarifResultDto>? Results { get; set; }

    [JsonPropertyName("tool")]
    public SarifToolDto? Tool { get; set; }
}