using System.Text.Json.Serialization;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>The source-generated serialization code for MSBuild evaluation answers.</summary>
[JsonSerializable(typeof(MsBuildAnswerDto))]
internal sealed partial class MsBuildAnswerJsonContext : JsonSerializerContext
{
}