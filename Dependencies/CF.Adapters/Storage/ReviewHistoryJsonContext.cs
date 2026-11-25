using System.Text.Json.Serialization;
using CF.Core.Ports;

namespace CF.Adapters.Storage;

/// <summary>The on-disk format of one line of review history.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ReviewRecord))]
internal sealed partial class ReviewHistoryJsonContext : JsonSerializerContext
{
}
