using System.Text.Json.Serialization;
using CF.Core.Ports;

namespace CF.Adapters.Storage;

/// <summary>The JSON format for a repository's last judgment of layer directions.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, IgnoreReadOnlyProperties = true)]
[JsonSerializable(typeof(StructureJudgment))]
internal sealed partial class StructureJudgmentJsonContext : JsonSerializerContext
{
}
