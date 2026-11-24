using System.Diagnostics;
using System.Runtime.InteropServices;
using CF.Core.Ports;
using CF.Core.Updates;

namespace CF.Adapters.Updates;

/// <summary>
/// Updates the program from the latest release on GitHub: the check, the download with its signature and checksum, the
/// swap of the files and the clean-up after it.
/// </summary>
public sealed class ReleaseUpdater : IUpdater
{
    private static readonly HttpClient _sharedHttp = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

    private readonly HttpClient _http;
    private readonly Uri _feed;
    private readonly InstallLocation _location;
    private readonly string _trustedKey;

    /// <summary>
    /// Creates the updater of the running program, which is in the version the text names. The feed is GitHub's, or the
    /// address the variable COMMITFENCE_UPDATE_FEED gives.
    /// </summary>
    public ReleaseUpdater(string productVersion)
        : this(_sharedHttp, UpdateChecker.GetFeedUri(), ParseVersion(productVersion), InstallLocation.Find(Environment.ProcessPath, InstallLocation.CurrentPlatform), UpdateSignature.ReleaseKey)
    {
    }

    internal ReleaseUpdater(HttpClient http, Uri feed, Version currentVersion, InstallLocation location, string trustedKey)
    {
        _http = http;
        _feed = feed;
        CurrentVersion = currentVersion;
        _location = location;
        _trustedKey = trustedKey;
    }

    /// <inheritdoc />
    public Version CurrentVersion { get; }

    /// <inheritdoc />
    public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        var packageName = UpdateChecker.GetPackageName(RuntimeInformation.RuntimeIdentifier, _location.Kind);
        return new UpdateChecker(_http, _feed, CurrentVersion, packageName).CheckAsync(cancellationToken);
    }

    /// <inheritdoc />
    public string? FindProblem(ReleaseInfo release)
    {
        if(_location.Problem != null)
            return _location.Problem;

        return release.CanInstall ? null : $"Version {release.Version} has no download the program can install by itself on this system.";
    }

    /// <inheritdoc />
    public Task InstallAsync(ReleaseInfo release, IProgress<UpdateProgress>? progress, UpdateCancellation? cancellation)
    {
        var installer = new UpdateInstaller(_http, release, _location, CurrentVersion, progress, cancellation) { TrustedKey = _trustedKey };
        return installer.RunAsync();
    }

    /// <inheritdoc />
    public Task CleanUpAsync(int attempts)
    {
        if(_location.Problem != null)
            return Task.CompletedTask;

        return UpdateInstaller.CleanUpAsync(_location.Folder, attempts);
    }

    /// <inheritdoc />
    public ProcessStartInfo CreateStartInfo(string? argument)
    {
        return _location.CreateStartInfo(argument);
    }

    private static Version ParseVersion(string text)
    {
        return UpdateChecker.TryParseVersion(text, out var version) ? version : new Version(0, 0, 0, 0);
    }
}