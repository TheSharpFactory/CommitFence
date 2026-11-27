using System.Net.Http.Headers;
using System.Text.Json;
using CF.Core.Updates;

namespace CF.Adapters.Updates;

/// <summary>
/// Asks GitHub for the latest release of the product (drafts and pre-releases are never "latest") and compares its
/// version with this one. The request sends nothing but the product's name and version (the User-Agent GitHub
/// requires).
/// </summary>
internal sealed class UpdateChecker
{
    /// <summary>
    /// Replaces the GitHub address (tests point it at a local server).
    /// </summary>
    public const string FeedVariable = "COMMITFENCE_UPDATE_FEED";

    public const string ChecksumsFileName = "SHA256SUMS.txt";

    private const string _gitHubFeed = "https://api.github.com/repos/TheSharpFactory/CommitFence/releases/latest";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly Uri _feed;
    private readonly Version _currentVersion;
    private readonly string? _packageName;

    public UpdateChecker(HttpClient http, Uri feed, Version currentVersion, string? packageName)
    {
        _http = http;
        _feed = feed;
        _currentVersion = currentVersion;
        _packageName = packageName;
    }

    /// <summary>
    /// The GitHub API address of the latest release, or the one COMMITFENCE_UPDATE_FEED gives.
    /// </summary>
    public static Uri GetFeedUri()
    {
        var feed = Environment.GetEnvironmentVariable(FeedVariable);
        return new Uri(string.IsNullOrWhiteSpace(feed) ? _gitHubFeed : feed);
    }

    /// <summary>
    /// The name of the download for an OS and processor and a kind of folder (the release workflow makes these: a .zip
    /// on Windows and for the macOS app, a .tar.gz on Linux and for the macOS command line, which keep the programs
    /// executable), or
    /// null when there is none.
    /// </summary>
    public static string? GetPackageName(string runtimeIdentifier, PackageKind kind)
    {
        switch(runtimeIdentifier)
        {
            case "win-x64":
            case "win-arm64":
                return kind == PackageKind.App ? $"CommitFence-{runtimeIdentifier}.zip" : $"commitfence-cli-{runtimeIdentifier}.zip";
            case "osx-x64":
            case "osx-arm64":
                return kind == PackageKind.App ? $"CommitFence-{runtimeIdentifier}.zip" : $"commitfence-cli-{runtimeIdentifier}.tar.gz";
            case "linux-x64":
                return kind == PackageKind.App ? "CommitFence-linux-x64.tar.gz" : "commitfence-cli-linux-x64.tar.gz";
            default:
                return null;
        }
    }

    /// <summary>
    /// Reads "v1.2.0.3" or "1.2.0.3" as a four-part version ("1.2" is 1.2.0.0).
    /// </summary>
    public static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if(string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if(trimmed.StartsWith('v') || trimmed.StartsWith('V'))
            trimmed = trimmed.Substring(1);

        if(!Version.TryParse(trimmed, out var parsed))
            return false;

        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
        return true;
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            using(var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(_timeout);
                using(var request = CreateRequest(_feed, _currentVersion))
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                    using(var response = await _http.SendAsync(request, timeout.Token))
                    {
                        if(!response.IsSuccessStatusCode)
                            return Fail($"GitHub answered {(int)response.StatusCode} ({response.ReasonPhrase}).");

                        var json = await response.Content.ReadAsStringAsync(timeout.Token);
                        return ReadRelease(json, _currentVersion, _packageName);
                    }
                }
            }
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested)
        {
            return Fail("GitHub did not answer in time.");
        }
        catch(HttpRequestException exception)
        {
            // also a connection lost while the answer was read: SendAsync reads it in full and reports that as this
            return Fail($"GitHub could not be reached ({exception.Message}).");
        }
    }

    /// <summary>
    /// A GET that names the product and its version: GitHub refuses requests without a User-Agent.
    /// </summary>
    public static HttpRequestMessage CreateRequest(Uri uri, Version currentVersion)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CommitFence", currentVersion.ToString()));
        return request;
    }

    /// <summary>
    /// Reads GitHub's release JSON: tag_name, html_url, body and the downloads (assets).
    /// </summary>
    public static UpdateCheckResult ReadRelease(string json, Version currentVersion, string? packageName)
    {
        try
        {
            var answer = JsonSerializer.Deserialize(json, GitHubReleaseJsonContext.Default.GitHubReleaseDto);
            if(answer == null || !TryParseVersion(answer.TagName, out var version))
                return Fail("GitHub's answer has no release version.");

            var release = ReadAssets(answer, version, packageName);
            var status = version > currentVersion ? UpdateCheckStatus.Available : UpdateCheckStatus.UpToDate;
            return new UpdateCheckResult { Status = status, Release = release };
        }
        catch(JsonException)
        {
            return Fail("GitHub's answer could not be read.");
        }
    }

    private static ReleaseInfo ReadAssets(GitHubReleaseDto answer, Version version, string? packageName)
    {
        Uri? packageUri = null;
        Uri? checksumsUri = null;
        Uri? signatureUri = null;
        if(answer.Assets != null)
        {
            foreach(var asset in answer.Assets)
            {
                if(asset == null)
                    continue;

                if(packageName != null && string.Equals(asset.Name, packageName, StringComparison.Ordinal))
                    packageUri = GetUri(asset.BrowserDownloadUrl);
                else if(string.Equals(asset.Name, ChecksumsFileName, StringComparison.Ordinal))
                    checksumsUri = GetUri(asset.BrowserDownloadUrl);
                else if(string.Equals(asset.Name, UpdateSignature.SignatureFileName, StringComparison.Ordinal))
                    signatureUri = GetUri(asset.BrowserDownloadUrl);
            }
        }

        return new ReleaseInfo
        {
            Version = version,
            Notes = answer.Body,
            PageUri = GetUri(answer.HtmlUrl),
            PackageName = packageName,
            PackageUri = packageUri,
            ChecksumsUri = checksumsUri,
            SignatureUri = signatureUri
        };
    }

    /// <summary>
    /// An https address (http only for a local test server), or null.
    /// </summary>
    private static Uri? GetUri(string? text)
    {
        if(text == null || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return null;

        if(string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || uri.IsLoopback)
            return uri;

        return null;
    }

    private static UpdateCheckResult Fail(string problem)
    {
        return new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Problem = problem };
    }
}