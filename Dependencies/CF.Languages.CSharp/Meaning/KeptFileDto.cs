using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// A file of kept answers: the folder of the private copy the facts were written in (their file paths start with it),
/// and the facts of each project and framework.
/// </summary>
internal sealed class KeptFileDto
{
    [JsonPropertyName("treeFolder")]
    public string? TreeFolder { get; set; }

    [JsonPropertyName("parts")]
    public List<KeptPartDto>? Parts { get; set; }
}