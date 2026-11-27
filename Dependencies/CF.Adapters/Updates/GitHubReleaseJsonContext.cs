using System.Text.Json.Serialization;

namespace CF.Adapters.Updates;

/// <summary>The source-generated serialization code for GitHub releases.</summary>
[JsonSerializable(typeof(GitHubReleaseDto))]
internal sealed partial class GitHubReleaseJsonContext : JsonSerializerContext
{
}