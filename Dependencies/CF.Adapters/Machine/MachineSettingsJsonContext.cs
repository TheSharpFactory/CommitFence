using System.Text.Json.Serialization;

namespace CF.Adapters.Machine;

/// <summary>
/// The source-generated serialization code for the app's file about this machine: nothing is found by reflection.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(MachineSettingsDto))]
internal sealed partial class MachineSettingsJsonContext : JsonSerializerContext
{
}
