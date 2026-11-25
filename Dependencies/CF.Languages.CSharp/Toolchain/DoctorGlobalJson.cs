using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>The SDK options Doctor needs to explain the selection made by dotnet.</summary>
internal sealed record DoctorGlobalJson(DoctorSdkOptions? Sdk);

/// <summary>The version and roll-forward policy, as written by the repository.</summary>
internal sealed record DoctorSdkOptions(string? Version, string? RollForward);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(DoctorGlobalJson))]
internal sealed partial class DoctorGlobalJsonContext : JsonSerializerContext;
