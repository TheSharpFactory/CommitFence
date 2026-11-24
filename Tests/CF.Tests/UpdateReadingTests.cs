using System.Net;
using CF.Adapters.Updates;
using CF.Core.Updates;
using Xunit;

namespace CF.Tests;

/// <summary>
/// Reading the release feed: which release is offered and whether the program may install it, which download each system takes, and
/// what a feed that does not answer looks like. No test touches the network: a feed in a temporary folder answers.
/// </summary>
public sealed class UpdateReadingTests
{
    private const string _package = "CommitFence-win-x64.zip";

    [Theory]
    [InlineData("a newer release", "v9.0.0.0", "1.2.0.3", true, true, "Available", true)]
    [InlineData("versions are numbers, not text", "v1.0.10.0", "1.0.9.0", true, true, "Available", true)]
    [InlineData("the same release", "v1.2.0.3", "1.2.0.3", true, true, "UpToDate", false)]
    [InlineData("an older release", "v1.1.8.8", "1.2.0.3", true, true, "UpToDate", false)]
    [InlineData("an older release, as text it would be newer", "v1.0.9.0", "1.0.10.0", true, true, "UpToDate", false)]
    [InlineData("no checksums", "v9.0.0.0", "1.0", false, true, "Available", false)]
    [InlineData("no signature", "v9.0.0.0", "1.0", true, false, "Available", false)]
    public void ReleaseSelectionRequiresCompatibleTrustedAssets(string why, string tag, string running, bool withChecksums, bool withSignature, string status, bool installable)
    {
        var json = ReleaseFixtures.ComposeReleaseJson(tag, _package, withChecksums);
        if(!withSignature)
            json = json.Replace("SHA256SUMS.txt.sig", "notes.txt", StringComparison.Ordinal);

        var result = UpdateChecker.ReadRelease(json, Version.Parse(running), _package);

        Assert.True(Enum.TryParse<UpdateCheckStatus>(status, out var expected), why);
        Assert.Equal(expected, result.Status);
        if(expected == UpdateCheckStatus.Available)
        {
            Assert.NotNull(result.Release);
            Assert.Equal(installable, result.Release.CanInstall);
            Assert.True(UpdateChecker.TryParseVersion(tag, out var version));
            Assert.Equal(version, result.Release.Version);
            Assert.Equal(new Uri(LocalFeed.Address + _package), result.Release.PackageUri);
            Assert.Equal("https://github.com/TheSharpFactory/CommitFence/releases/tag/" + tag, result.Release.PageUri?.ToString());
            Assert.Equal(withChecksums, result.Release.ChecksumsUri is not null);
            Assert.Equal(withChecksums && withSignature, result.Release.SignatureUri is not null);
        }

        // a feed that is not one, a release of another kind of folder, and a download that is not https are not taken
        Assert.Equal(UpdateCheckStatus.Failed, UpdateChecker.ReadRelease("{\"message\":\"Not Found\"}", new Version(1, 0), _package).Status);
        Assert.Equal(UpdateCheckStatus.Failed, UpdateChecker.ReadRelease("<html>", new Version(1, 0), _package).Status);
        Assert.Equal(UpdateCheckStatus.Failed, UpdateChecker.ReadRelease("[]", new Version(1, 0), _package).Status);
        var otherKind = UpdateChecker.ReadRelease(ReleaseFixtures.ComposeReleaseJson("v9.0.0.0", _package, true), new Version(1, 0), "commitfence-cli-win-x64.zip");
        Assert.Null(otherKind.Release?.PackageUri);
        Assert.False(otherKind.Release?.CanInstall);
        var plain = ReleaseFixtures.ComposeReleaseJson("v9.0.0.0", _package, true).Replace(LocalFeed.Address, "http://example.com/x/", StringComparison.Ordinal);
        var insecure = UpdateChecker.ReadRelease(plain, new Version(1, 0), _package);
        Assert.Null(insecure.Release?.PackageUri);
        Assert.Null(insecure.Release?.ChecksumsUri);
        Assert.Null(insecure.Release?.SignatureUri);
        Assert.False(UpdateChecker.TryParseVersion("latest", out _));
        Assert.False(UpdateChecker.TryParseVersion(null, out _));
    }

    [Theory]
    [InlineData("win-x64", true, "CommitFence-win-x64.zip")]
    [InlineData("win-arm64", true, "CommitFence-win-arm64.zip")]
    [InlineData("osx-arm64", true, "CommitFence-osx-arm64.zip")]
    [InlineData("osx-x64", true, "CommitFence-osx-x64.zip")]
    [InlineData("linux-x64", true, "CommitFence-linux-x64.tar.gz")]
    [InlineData("linux-arm64", true, null)]
    [InlineData("win-x64", false, "commitfence-cli-win-x64.zip")]
    [InlineData("win-arm64", false, "commitfence-cli-win-arm64.zip")]
    [InlineData("osx-arm64", false, "commitfence-cli-osx-arm64.tar.gz")]
    [InlineData("osx-x64", false, "commitfence-cli-osx-x64.tar.gz")]
    [InlineData("linux-x64", false, "commitfence-cli-linux-x64.tar.gz")]
    [InlineData("linux-arm64", false, null)]
    public void PlatformSelectsPublishedArchive(string runtime, bool app, string? expected)
    {
        Assert.Equal(expected, UpdateChecker.GetPackageName(runtime, app ? PackageKind.App : PackageKind.CommandLine));
    }

    [Theory]
    [InlineData("an error answer", HttpStatusCode.Forbidden, false, false, "403")]
    [InlineData("no network", HttpStatusCode.OK, true, false, "could not be reached")]
    [InlineData("the connection dropped while reading", HttpStatusCode.OK, false, true, null)]
    public async Task FeedFailureReturnsUnavailable(string why, HttpStatusCode status, bool noNetwork, bool dropped, string? problem)
    {
        using(var folder = new TestFolder())
        {
            var feed = new LocalFeed(folder.Path) { Status = status, ThrowNetworkError = noNetwork };
            if(dropped)
                feed.DropWhileReading = LocalFeed.FeedUri.ToString();

            var result = await new UpdateChecker(feed.CreateClient(), LocalFeed.FeedUri, new Version(1, 2, 0, 3), "a.zip").CheckAsync(TestContext.Current.CancellationToken);

            Assert.Equal(UpdateCheckStatus.Failed, result.Status);
            Assert.NotNull(result.Problem);
            if(problem is not null)
                Assert.Contains(problem, result.Problem, StringComparison.Ordinal);
            Assert.Null(result.Release);
            Assert.True(why.Length > 0);

            // the request that failed named the product and its version
            if(!noNetwork)
                Assert.Equal("CommitFence/1.2.0.3", feed.UserAgents[0]);
        }
    }
}
