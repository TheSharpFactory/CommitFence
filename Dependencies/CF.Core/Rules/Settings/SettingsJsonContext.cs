using System.Text.Json.Serialization;

namespace CF.Core.Rules.Settings;

/// <summary>
/// The source-generated serialization code for the settings file: nothing is found by reflection when the program
/// starts.
/// </summary>
[JsonSerializable(typeof(SettingsFileDto))]
[JsonSerializable(typeof(SettingsRequirementDto))]
[JsonSerializable(typeof(string))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}