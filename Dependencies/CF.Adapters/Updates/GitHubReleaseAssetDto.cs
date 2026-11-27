using System.Text.Json.Serialization;

namespace CF.Adapters.Updates;

/// <summary>A file attached to a GitHub release.</summary>
internal sealed record GitHubReleaseAssetDto(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl);