using System.Text.Json.Serialization;

namespace CF.Tests.Wire;

/// <summary>
/// GitHub's answer for a release as the tests write it, with GitHub's own key names, kept apart from the product's
/// reader so that a wrong key on either side shows.
/// </summary>
internal sealed record ReleaseWire(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("assets")] List<ReleaseAssetWire> Assets);