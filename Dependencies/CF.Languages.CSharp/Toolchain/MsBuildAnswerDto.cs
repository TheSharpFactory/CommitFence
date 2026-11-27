using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>The properties and item groups MSBuild returns for an evaluation.</summary>
internal sealed class MsBuildAnswerDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, List<MsBuildItemDto>>? Items { get; init; }

    public required Dictionary<string, string> Properties { get; init; }
}