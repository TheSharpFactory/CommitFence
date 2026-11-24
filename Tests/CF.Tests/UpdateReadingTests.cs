using System.Net;
using CF.Adapters.Updates;
using CF.Core.Updates;
using Xunit;

namespace CF.Tests;

/// <summary>
/// Reading the release feed: the version, the downloads for each system and each kind of folder, the once-a-day rule and
/// the release text. No test touches the network: a feed in a temporary folder answers.
/// </summary>
public sealed class UpdateReadingTests
{
    private const string _pageBase = "https://github.com/TheSharpFactory/CommitFence/releases/tag/";

    [Fact]
    public void VersionsReadWithOrWithoutTheV()
    {
        Assert.True(UpdateChecker.TryParseVersion("v1.2.0.3", out var tagged));
        Assert.Equal(new Version(1, 2, 0, 3), tagged);
        Assert.True(UpdateChecker.TryParseVersion("1.2", out var shortVersion));
        Assert.Equal(new Version(1, 2, 0, 0), shortVersion);
        Assert.False(UpdateChecker.TryParseVersion("latest", out _));
        Assert.False(UpdateChecker.TryParseVersion(null, out _));
    }

    [Fact]
    public void VersionsAreComparedAsFourNumbersNotAsText()
    {
        var older = UpdateChecker.ReadRelease(ReleaseFixtures.ComposeReleaseJson("v1.0.9.0", "a.zip", true), new Version(1, 0, 10, 0), "a.zip");
        var newer = UpdateChecker.ReadRelease(ReleaseFixtures.ComposeReleaseJson("v1.0.10.0", "a.zip", true), new Version(1, 0, 9, 0), "a.zip");

        Assert.Equal(UpdateCheckStatus.UpToDate, older.Status);
        Assert.Equal(UpdateCheckStatus.Available, newer.Status);
    }

    [Fact]
    public void EachSystemAndKindOfFolderGetsItsOwnDownload()
    {
        Assert.Equal("CommitFence-win-x64.zip", UpdateChecker.GetPackageName("win-x64", PackageKind.App));
        Assert.Equal("CommitFence-win-arm64.zip", UpdateChecker.GetPackageName("win-arm64", PackageKind.App));
        Assert.Equal("CommitFence-osx-arm64.zip", UpdateChecker.GetPackageName("osx-arm64", PackageKind.App));
        Assert.Equal("CommitFence-osx-x64.zip", UpdateChecker.GetPackageName("osx-x64", PackageKind.App));
        Assert.Equal("CommitFence-linux-x64.tar.gz", UpdateChecker.GetPackageName("linux-x64", PackageKind.App));

        Assert.Equal("commitfence-cli-win-x64.zip", UpdateChecker.GetPackageName("win-x64", PackageKind.CommandLine));
        Assert.Equal("commitfence-cli-win-arm64.zip", UpdateChecker.GetPackageName("win-arm64", PackageKind.CommandLine));
        Assert.Equal("commitfence-cli-osx-arm64.tar.gz", UpdateChecker.GetPackageName("osx-arm64", PackageKind.CommandLine));
        Assert.Equal("commitfence-cli-osx-x64.tar.gz", UpdateChecker.GetPackageName("osx-x64", PackageKind.CommandLine));
        Assert.Equal("commitfence-cli-linux-x64.tar.gz", UpdateChecker.GetPackageName("linux-x64", PackageKind.CommandLine));

        Assert.Null(UpdateChecker.GetPackageName("linux-arm64", PackageKind.App));
        Assert.Null(UpdateChecker.GetPackageName("linux-arm64", PackageKind.CommandLine));
    }

    [Fact]
    public void ANewerReleaseIsAvailableWithItsDownloadAndChecksums()
    {
        var result = UpdateChecker.ReadRelease(ReleaseFixtures.ComposeReleaseJson("v9.0.0.0", "CommitFence-win-x64.zip", true), new Version(1, 2, 0, 3), "CommitFence-win-x64.zip");

        Assert.Equal(UpdateCheckStatus.Available, result.Status);
        Assert.NotNull(result.Release);
        Assert.Equal(new Version(9, 0, 0, 0), result.Release.Version);
        Assert.Equal(new Uri(LocalFeed.Address + "CommitFence-win-x64.zip"), result.Release.PackageUri);
        Assert.Equal(new Uri(LocalFeed.Address + "SHA256SUMS.txt"), result.Release.ChecksumsUri);
        Assert.Equal(new Uri(LocalFeed.Address + "SHA256SUMS.txt.sig"), result.Release.SignatureUri);
        Assert.Equal(_pageBase + "v9.0.0.0", result.Release.PageUri?.ToString());
        Assert.True(result.Release.CanInstall);
    }

    [Fact]
    public void OnlyTheDownloadOfThisKindOfFolderIsTaken()
    {
        var json = ReleaseFixtures.ComposeReleaseJson("v9.0.0.0", "CommitFence-win-x64.zip", true);

        var commandLine = UpdateChecker.ReadRelease(json, new Version(1, 0), "commitfence-cli-win-x64.zip");

        Assert.Null(commandLine.Release?.PackageUri);
        Assert.False(commandLine.Release?.CanInstall);
    }

    [Fact]
    public void TheSameOrAnOlderReleaseIsUpToDate()
    {
        Assert.Equal(UpdateCheckStatus.UpToDate, UpdateChecker.ReadRelease(ReleaseFixtures.ComposeReleaseJson("v1.2.0.3", "a.zip", true), new Version(1, 2, 0, 3), "a.zip").Status);
        Assert.Equal(UpdateCheckStatus.UpToDate, UpdateChecker.ReadRelease(ReleaseFixtures.ComposeReleaseJson("v1.1.8.8", "a.zip", true), new Version(1, 2, 0, 3), "a.zip").Status);
    }

    [Fact]
    public void AReleaseWithoutChecksumsTheirSignatureOrThisSystemsDownloadCannotBeInstalledByTheProgram()
    {
        var noChecksums = UpdateChecker.ReadRelease(ReleaseFixtures.ComposeReleaseJson("v9.0.0.0", "CommitFence-win-x64.zip", false), new Version(1, 0), "CommitFence-win-x64.zip");
        Assert.Equal(UpdateCheckStatus.Available, noChecksums.Status);
        Assert.False(noChecksums.Release?.CanInstall);

        // an unsigned release (one made before signing, or by someone without the key) is offered as a page only
        var unsignedJson = ReleaseFixtures.ComposeReleaseJson("v9.0.0.0", "CommitFence-win-x64.zip", true).Replace("SHA256SUMS.txt.sig", "notes.txt", StringComparison.Ordinal);
        var unsigned = UpdateChecker.ReadRelease(unsignedJson, new Version(1, 0), "CommitFence-win-x64.zip");
        Assert.NotNull(unsigned.Release?.ChecksumsUri);
        Assert.Null(unsigned.Release?.SignatureUri);
        Assert.False(unsigned.Release?.CanInstall);

        var noPackage = UpdateChecker.ReadRelease(ReleaseFixtures.ComposeReleaseJson("v9.0.0.0", "CommitFence-win-x64.zip", true), new Version(1, 0), null);
        Assert.Null(noPackage.Release?.PackageUri);
        Assert.False(noPackage.Release?.CanInstall);
    }

    [Fact]
    public void OnlyHttpsDownloadsAreTaken()
    {
        var json = ReleaseFixtures.ComposeReleaseJson("v9.0.0.0", "a.zip", true).Replace(LocalFeed.Address, "http://example.com/x/", StringComparison.Ordinal);

        var result = UpdateChecker.ReadRelease(json, new Version(1, 0), "a.zip");

        Assert.Null(result.Release?.PackageUri);
        Assert.Null(result.Release?.ChecksumsUri);
        Assert.Null(result.Release?.SignatureUri);
    }

    [Fact]
    public void AnAnswerThatIsNotAReleaseFails()
    {
        Assert.Equal(UpdateCheckStatus.Failed, UpdateChecker.ReadRelease("{\"message\":\"Not Found\"}", new Version(1, 0), "a.zip").Status);
        Assert.Equal(UpdateCheckStatus.Failed, UpdateChecker.ReadRelease("<html>", new Version(1, 0), "a.zip").Status);
        Assert.Equal(UpdateCheckStatus.Failed, UpdateChecker.ReadRelease("[]", new Version(1, 0), "a.zip").Status);
    }

    [Fact]
    public void TheFeedCanBePointedElsewhere()
    {
        var previous = Environment.GetEnvironmentVariable(UpdateChecker.FeedVariable);
        try
        {
            Environment.SetEnvironmentVariable(UpdateChecker.FeedVariable, null);
            Assert.Equal("https://api.github.com/repos/TheSharpFactory/CommitFence/releases/latest", UpdateChecker.GetFeedUri().ToString());
            Environment.SetEnvironmentVariable(UpdateChecker.FeedVariable, "http://localhost:8123/latest.json");
            Assert.Equal("http://localhost:8123/latest.json", UpdateChecker.GetFeedUri().ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(UpdateChecker.FeedVariable, previous);
        }

        Assert.Equal("COMMITFENCE_UPDATE_FEED", UpdateChecker.FeedVariable);
    }

    [Fact]
    public async Task TheRequestNamesTheProductAndItsVersion()
    {
        using(var folder = new TestFolder())
        {
            var feed = new LocalFeed(folder.Path);
            feed.Write("latest.json", ReleaseFixtures.ComposeReleaseJson("v9.0.0.0", "a.zip", true));
            var checker = new UpdateChecker(feed.CreateClient(), LocalFeed.FeedUri, new Version(1, 2, 0, 3), "a.zip");

            var result = await checker.CheckAsync(CancellationToken.None);

            Assert.Equal(UpdateCheckStatus.Available, result.Status);
            Assert.Equal("CommitFence/1.2.0.3", feed.UserAgents[0]);
        }
    }

    [Fact]
    public async Task AnErrorAnswerOrNoNetworkFailsQuietly()
    {
        using(var folder = new TestFolder())
        {
            var forbidden = new LocalFeed(folder.Path) { Status = HttpStatusCode.Forbidden };
            var refused = await new UpdateChecker(forbidden.CreateClient(), LocalFeed.FeedUri, new Version(1, 0), "a.zip").CheckAsync(CancellationToken.None);
            Assert.Equal(UpdateCheckStatus.Failed, refused.Status);
            Assert.Contains("403", refused.Problem, StringComparison.Ordinal);

            var offline = new LocalFeed(folder.Path) { ThrowNetworkError = true };
            var noNetwork = await new UpdateChecker(offline.CreateClient(), LocalFeed.FeedUri, new Version(1, 0), "a.zip").CheckAsync(CancellationToken.None);
            Assert.Equal(UpdateCheckStatus.Failed, noNetwork.Status);
            Assert.Contains("could not be reached", noNetwork.Problem, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AConnectionLostWhileReadingTheAnswerFailsQuietly()
    {
        using(var folder = new TestFolder())
        {
            var feed = new LocalFeed(folder.Path) { DropWhileReading = LocalFeed.FeedUri.ToString() };

            var result = await new UpdateChecker(feed.CreateClient(), LocalFeed.FeedUri, new Version(1, 0), "a.zip").CheckAsync(CancellationToken.None);

            Assert.Equal(UpdateCheckStatus.Failed, result.Status);
            Assert.NotNull(result.Problem);
        }
    }

    [Fact]
    public void ACheckIsDueOnceADay()
    {
        var now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(UpdateSchedule.IsCheckDue(null, now));
        Assert.False(UpdateSchedule.IsCheckDue(now.AddHours(-23), now));
        Assert.True(UpdateSchedule.IsCheckDue(now.AddHours(-24), now));
        Assert.True(UpdateSchedule.IsCheckDue(now.AddDays(-3), now));

        // the clock went back
        Assert.True(UpdateSchedule.IsCheckDue(now.AddHours(5), now));
    }

    [Fact]
    public void TheReleaseTextIsShortenedToPlainText()
    {
        var markdown = "The first release: a **desktop app** and a `commitfence` command line.\r\n\r\n### What it does\r\n\r\n- Reviews a push.\r\n- Blocks a bad one.\r\n\r\n### Downloads\r\n\r\n| Download | What |\r\n|---|---|\r\n";

        var text = ReleaseNotes.Summarize(markdown, 700);

        Assert.Equal("The first release: a desktop app and a commitfence command line.\n\nWhat it does\n• Reviews a push.\n• Blocks a bad one.", text);
        Assert.Null(ReleaseNotes.Summarize("  ", 700));

        var cut = ReleaseNotes.Summarize("First paragraph here.\n\nSecond paragraph here.\n\nThird paragraph here.", 40);
        Assert.Equal("First paragraph here.\n…", cut);
    }

    [Fact]
    public void TheLinesTheChangelogWasWrappedOverAreJoinedAgain()
    {
        var markdown = "**Check your hook:** the line changed\r\nin this version.\r\n\r\n### What's new\r\n\r\n- **Automatic updates.** The program looks\r\n  for a newer release.\r\n- The command line comes\r\n  with the app. See [the docs](https://example.com).\r\n\r\n---\r\n";

        Assert.Equal("Check your hook: the line changed in this version.\n\nWhat's new\n• Automatic updates. The program looks for a newer release.\n• The command line comes with the app. See the docs.", ReleaseNotes.Summarize(markdown, 1500));
    }

    [Fact]
    public void TheReleaseTextIsLaidOutLineByLine()
    {
        var lines = ReleaseNotes.Lines("Intro.\n\n### Fixes\n\n- One.\n- Two.\n\n### Downloads\n\n| a | b |", 1500);

        Assert.Equal(4, lines.Count);
        Assert.True(lines[0].IsParagraph);
        Assert.True(lines[1].IsHeading);
        Assert.Equal("Fixes", lines[1].Text);
        Assert.True(lines[2].IsListItem);
        Assert.Equal("One.", lines[2].Text);
        Assert.Empty(ReleaseNotes.Lines(null, 100));
    }
}