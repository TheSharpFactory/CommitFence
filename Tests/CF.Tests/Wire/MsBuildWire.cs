using System.Text.Json.Serialization;

namespace CF.Tests.Wire;

/// <summary>
/// The answer of dotnet msbuild -getProperty / -getItem as the tests write it, with MSBuild's own key names, kept
/// apart from the product's reader so that a wrong key on either side shows.
/// </summary>
internal sealed record MsBuildWire(
    [property: JsonPropertyName("Properties")] Dictionary<string, string> Properties,
    [property: JsonPropertyName("Items"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, List<MsBuildItemWire>>? Items = null);