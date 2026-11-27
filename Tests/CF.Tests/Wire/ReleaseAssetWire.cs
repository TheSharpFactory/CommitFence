using System.Text.Json.Serialization;

namespace CF.Tests.Wire;

/// <summary>A download of a release in GitHub's answer, with GitHub's own key names.</summary>
internal sealed record ReleaseAssetWire(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);