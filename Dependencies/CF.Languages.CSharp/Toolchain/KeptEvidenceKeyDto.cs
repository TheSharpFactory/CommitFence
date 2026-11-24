using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The small file beside a kept log: the key of what the compile read, and the checksum of the log it belongs to.
/// </summary>
internal sealed class KeptEvidenceKeyDto
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("logHash")]
    public string? LogHash { get; set; }
}