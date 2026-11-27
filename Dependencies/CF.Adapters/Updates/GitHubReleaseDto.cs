using System.Text.Json.Serialization;

namespace CF.Adapters.Updates;

/// <summary>The fields CommitFence uses from a GitHub release.</summary>
internal sealed record GitHubReleaseDto(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("html_url")] string? HtmlUrl,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("assets")] List<GitHubReleaseAssetDto?>? Assets);