using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The compiler's log, as the JSON serializer fills it in: only what the review needs. Everything else in the log is
/// skipped.
/// </summary>
internal sealed class SarifLogDto
{
    [JsonPropertyName("runs")]
    public List<SarifRunDto>? Runs { get; set; }
}