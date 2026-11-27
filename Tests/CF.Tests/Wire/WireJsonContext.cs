using System.Text.Json.Serialization;

namespace CF.Tests.Wire;

/// <summary>The source-generated serialization code for the answers the tests write in place of GitHub and MSBuild.</summary>
[JsonSerializable(typeof(ReleaseWire))]
[JsonSerializable(typeof(MsBuildWire))]
internal sealed partial class WireJsonContext : JsonSerializerContext
{
}