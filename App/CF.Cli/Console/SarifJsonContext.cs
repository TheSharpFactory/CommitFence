using System.Text.Json.Serialization;

namespace CF.Cli.Console;

/// <summary>The source-generated serialization code for the SARIF log: nothing is found by reflection.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    NewLine = "\n")]
[JsonSerializable(typeof(SarifLog))]
internal sealed partial class SarifJsonContext : JsonSerializerContext
{
}
