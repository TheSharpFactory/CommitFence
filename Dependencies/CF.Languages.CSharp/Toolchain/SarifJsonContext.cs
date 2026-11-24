using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The source-generated serialization code for the compiler's log: nothing is found by reflection.
/// </summary>
[JsonSerializable(typeof(SarifLogDto))]
internal sealed partial class SarifJsonContext : JsonSerializerContext
{
}