using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using CF.Adapters.Updates;
using CF.Core.Ports;
using CF.Core.Updates;

namespace CF.Tests;

/// <summary>
/// A temporary folder that is deleted, with everything in it, when the test is done.
/// </summary>
internal sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Path = System.IO.Path.Combine(TestPaths.Temp, "cf-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if(Directory.Exists(Path))
            Directory.Delete(Path, true);
    }
}

/// <summary>
/// The key the test releases are signed with, as the release workflow signs the real ones (ECDSA P-256, SHA-256, DER).
/// </summary>
internal static class TestSigning
{
    private static readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public static string PublicKey
    {
        get { return _key.ExportSubjectPublicKeyInfoPem(); }
    }

    public static byte[] Sign(byte[] data)
    {
        return _key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    }
}

/// <summary>
/// A release feed in a folder: the answer GitHub would give, the downloads, the list of their hashes and its signature are
/// files, and requests to the feed's address are answered from them without any network. It records what was asked.
/// </summary>
internal sealed class LocalFeed : HttpMessageHandler
{
    public const string Address = "http://127.0.0.1:8123/";

    private readonly Lock _syncRoot = new Lock();

    public LocalFeed(string folder)
    {
        Folder = folder;
        Directory.CreateDirectory(folder);
    }

    public string Folder { get; }

    public static Uri FeedUri
    {
        get { return new Uri(Address + "latest.json"); }
    }

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public bool ThrowNetworkError { get; set; }

    public List<string> Requests { get; } = new List<string>();

    public List<string> UserAgents { get; } = new List<string>();

    /// <summary>
    /// The answer to this address starts, then the connection drops while it is read.
    /// </summary>
    public string? DropWhileReading { get; set; }

    /// <summary>
    /// The answer to this address never comes; only the request's cancellation ends the wait.
    /// </summary>
    public string? HangOn { get; set; }

    public void Write(string name, string text)
    {
        File.WriteAllText(System.IO.Path.Combine(Folder, name), text);
    }

    public void Write(string name, byte[] bytes)
    {
        File.WriteAllBytes(System.IO.Path.Combine(Folder, name), bytes);
    }

    public byte[] Read(string name)
    {
        return File.ReadAllBytes(System.IO.Path.Combine(Folder, name));
    }

    public HttpClient CreateClient()
    {
        return new HttpClient(this);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!.ToString();
        lock(_syncRoot)
        {
            Requests.Add(uri);
            UserAgents.Add(request.Headers.UserAgent.ToString());
        }

        if(ThrowNetworkError)
            throw new HttpRequestException("No such host is known.");

        if(string.Equals(uri, HangOn, StringComparison.Ordinal))
            await Task.Delay(Timeout.Infinite, cancellationToken);

        if(Status != HttpStatusCode.OK)
            return new HttpResponseMessage(Status) { ReasonPhrase = Status.ToString() };

        if(string.Equals(uri, DropWhileReading, StringComparison.Ordinal))
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new DroppedContent() };

        var path = System.IO.Path.Combine(Folder, uri.Substring(Address.Length));
        if(!uri.StartsWith(Address, StringComparison.Ordinal) || !File.Exists(path))
            return new HttpResponseMessage(HttpStatusCode.NotFound);

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(await File.ReadAllBytesAsync(path, cancellationToken)) };
    }
}

/// <summary>
/// An answer whose connection is lost after a few bytes, as HttpClient reports it (an IOException).
/// </summary>
internal sealed class DroppedContent : HttpContent
{
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await stream.WriteAsync(Encoding.UTF8.GetBytes("{\"tag_"));
        throw new IOException("The response ended prematurely.");
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}

/// <summary>
/// Keeps every stage reported, synchronously (Progress&lt;T&gt; would post them later).
/// </summary>
internal sealed class RecordingProgress : IProgress<UpdateProgress>
{
    private readonly Lock _syncRoot = new Lock();

    public List<UpdateStage> Stages { get; } = new List<UpdateStage>();

    public void Report(UpdateProgress value)
    {
        lock(_syncRoot)
        {
            Stages.Add(value.Stage);
        }
    }
}

/// <summary>
/// Reports each stage to a function the test gives, on the thread that installs and before the installer goes on: the test can
/// cancel at the very moment it chooses, with no waiting on time.
/// </summary>
internal sealed class ReactingProgress : IProgress<UpdateProgress>
{
    private readonly Action<UpdateProgress> _react;

    public ReactingProgress(Action<UpdateProgress> react)
    {
        _react = react;
    }

    public void Report(UpdateProgress value)
    {
        _react(value);
    }
}

/// <summary>
/// A data folder that is the one the test gives.
/// </summary>
internal sealed class FixedDataFolder : IDataFolder
{
    public FixedDataFolder(string rootFolder)
    {
        RootFolder = rootFolder;
    }

    public string RootFolder { get; }
}

/// <summary>
/// The real checker and the real installer over a feed in a folder, as <see cref="ReleaseUpdater" /> puts them together, with one
/// difference: the test names the package (its format too), where the updater takes the one for the system it runs on.
/// </summary>
internal sealed class FeedUpdater : IUpdater
{
    private readonly HttpClient _http;
    private readonly string _packageName;
    private readonly InstallLocation _location;
    private readonly string _trustedKey;

    public FeedUpdater(LocalFeed feed, string packageName, InstallLocation location, string? trustedKey = null)
    {
        _http = feed.CreateClient();
        _packageName = packageName;
        _location = location;
        _trustedKey = trustedKey ?? TestSigning.PublicKey;
    }

    public Version CurrentVersion { get; } = new Version(1, 0, 0, 0);

    public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        return new UpdateChecker(_http, LocalFeed.FeedUri, CurrentVersion, _packageName).CheckAsync(cancellationToken);
    }

    public string? FindProblem(ReleaseInfo release)
    {
        return release.CanInstall ? null : "No download the program can install by itself.";
    }

    public Task InstallAsync(ReleaseInfo release, IProgress<UpdateProgress>? progress, UpdateCancellation? cancellation)
    {
        return new UpdateInstaller(_http, release, _location, CurrentVersion, progress, cancellation) { TrustedKey = _trustedKey }.RunAsync();
    }

    public Task CleanUpAsync(int attempts)
    {
        return UpdateInstaller.CleanUpAsync(_location.Folder, attempts);
    }

    public ProcessStartInfo CreateStartInfo(string? argument)
    {
        return _location.CreateStartInfo(argument);
    }
}

/// <summary>
/// A clock that stands where the test puts it.
/// </summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; set; }
}

/// <summary>
/// The record of the last check, in memory.
/// </summary>
internal sealed class MemoryCheckRecord : IUpdateCheckRecord
{
    public DateTime? LastCheckUtc { get; set; }

    public void Save(DateTime checkedUtc)
    {
        LastCheckUtc = checkedUtc;
    }
}

/// <summary>
/// An updater that answers from what the test sets and records what it was asked; it touches no file and no network.
/// </summary>
internal sealed class FakeUpdater : IUpdater
{
    public Version CurrentVersion { get; set; } = new Version(1, 0, 0, 0);

    public UpdateCheckResult Result { get; set; } = new UpdateCheckResult { Status = UpdateCheckStatus.UpToDate, Release = new ReleaseInfo { Version = new Version(1, 0, 0, 0) } };

    public string? Problem { get; set; }

    public Exception? InstallFailure { get; set; }

    public int Checks { get; private set; }

    public int Installs { get; private set; }

    public int CleanUps { get; private set; }

    public string? StartArgument { get; private set; }

    /// <summary>
    /// When set, an install waits for it (or for the cancellation) before it ends.
    /// </summary>
    public TaskCompletionSource? HoldInstall { get; set; }

    public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        Checks++;
        return Task.FromResult(Result);
    }

    public string? FindProblem(ReleaseInfo release)
    {
        return Problem;
    }

    public async Task InstallAsync(ReleaseInfo release, IProgress<UpdateProgress>? progress, UpdateCancellation? cancellation)
    {
        Installs++;
        progress?.Report(new UpdateProgress { Stage = UpdateStage.Downloading, Percent = 40 });
        if(HoldInstall != null)
            await HoldInstall.Task.WaitAsync(cancellation?.Token ?? CancellationToken.None);

        if(InstallFailure != null)
            throw InstallFailure;

        if(cancellation != null && !cancellation.TryBeginReplacing())
            throw new OperationCanceledException(cancellation.Token);

        progress?.Report(new UpdateProgress { Stage = UpdateStage.Replacing });
    }

    public Task CleanUpAsync(int attempts)
    {
        CleanUps++;
        return Task.CompletedTask;
    }

    public ProcessStartInfo CreateStartInfo(string? argument)
    {
        StartArgument = argument;
        var startInfo = new ProcessStartInfo("commitfenceapp");
        if(argument != null)
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }

    public static UpdateCheckResult Available(string version, string? notes = null, bool withPage = true)
    {
        var release = new ReleaseInfo
        {
            Version = Version.Parse(version),
            Notes = notes,
            PageUri = withPage ? new Uri("https://github.com/TheSharpFactory/CommitFence/releases/tag/v" + version) : null,
            PackageName = "CommitFence-win-x64.zip",
            PackageUri = new Uri("https://example.com/a.zip"),
            ChecksumsUri = new Uri("https://example.com/SHA256SUMS.txt"),
            SignatureUri = new Uri("https://example.com/SHA256SUMS.txt.sig")
        };
        return new UpdateCheckResult { Status = UpdateCheckStatus.Available, Release = release };
    }
}

/// <summary>
/// What the update tests build: an installed program in a folder, a release in a feed, and the packages in between.
/// </summary>
internal static class ReleaseFixtures
{
    public const string NewVersionTag = "v9.0.0.0";

    public static string Program(string name)
    {
        return OperatingSystem.IsWindows() ? name + ".exe" : name;
    }

    /// <summary>
    /// An installed release in a folder: the command line, the analyzers, the licences, a file of the user's, and, for the
    /// full download, the app.
    /// </summary>
    public static InstallLocation CreateInstall(string folder, PackageKind kind)
    {
        Directory.CreateDirectory(Path.Combine(folder, "licenses"));
        Directory.CreateDirectory(Path.Combine(folder, "analyzers"));
        File.WriteAllText(Path.Combine(folder, Program("commitfence")), "old cli");
        File.WriteAllText(Path.Combine(folder, "analyzers", "CF.Meaning.dll"), "old analyzer");
        File.WriteAllText(Path.Combine(folder, "LICENSE"), "old license");
        File.WriteAllText(Path.Combine(folder, "licenses", "Old.txt"), "old");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "mine");
        if(kind == PackageKind.App)
            File.WriteAllText(Path.Combine(folder, Program("commitfenceapp")), "old app");

        var running = Program(kind == PackageKind.App ? "commitfenceapp" : "commitfence");
        return new InstallLocation { Folder = folder, Executable = Path.Combine(folder, running), Kind = kind };
    }

    /// <summary>
    /// The text of GitHub's answer for the latest release, with the downloads of every kind and system.
    /// </summary>
    public static string ComposeReleaseJson(string tag, string packageName, bool withChecksums, string body = "Better things.")
    {
        var assets = new StringBuilder();
        assets.Append($"{{\"name\":\"{packageName}\",\"browser_download_url\":\"{LocalFeed.Address}{packageName}\"}}");
        assets.Append($",{{\"name\":\"other-download.zip\",\"browser_download_url\":\"{LocalFeed.Address}other-download.zip\"}}");
        if(withChecksums)
        {
            assets.Append($",{{\"name\":\"SHA256SUMS.txt\",\"browser_download_url\":\"{LocalFeed.Address}SHA256SUMS.txt\"}}");
            assets.Append($",{{\"name\":\"SHA256SUMS.txt.sig\",\"browser_download_url\":\"{LocalFeed.Address}SHA256SUMS.txt.sig\"}}");
        }

        var bodyJson = System.Text.Json.JsonSerializer.Serialize(body);
        return $"{{\"tag_name\":\"{tag}\",\"html_url\":\"https://github.com/TheSharpFactory/CommitFence/releases/tag/{tag}\",\"body\":{bodyJson},\"assets\":[{assets}]}}";
    }

    /// <summary>
    /// Builds a package (.zip or .tar.gz by its name) with the new files, puts it, its SHA256SUMS.txt and that file's
    /// signature (by the test key) in the feed; a wrong hash makes a damaged download. The release GitHub would describe
    /// is written as latest.json, and what the program reads from it is returned.
    /// </summary>
    public static ReleaseInfo PublishPackage(LocalFeed feed, string packageName, PackageKind kind, string? wrongHash)
    {
        var source = Path.Combine(feed.Folder, "package-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(source, "licenses"));
        Directory.CreateDirectory(Path.Combine(source, "analyzers"));
        File.WriteAllText(Path.Combine(source, Program("commitfence")), "new cli");
        File.WriteAllText(Path.Combine(source, "analyzers", "CF.Meaning.dll"), "new analyzer");
        File.WriteAllText(Path.Combine(source, "LICENSE"), "new license");
        File.WriteAllText(Path.Combine(source, "THIRD-PARTY-NOTICES.md"), "new notices");
        File.WriteAllText(Path.Combine(source, "licenses", "New.txt"), "new");
        if(kind == PackageKind.App)
            File.WriteAllText(Path.Combine(source, Program("commitfenceapp")), "new app");

        var package = Path.Combine(feed.Folder, "package-" + Guid.NewGuid().ToString("N"));
        Pack(source, package, packageName);
        Directory.Delete(source, true);

        var bytes = File.ReadAllBytes(package);
        File.Delete(package);
        var hash = wrongHash ?? Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        feed.Write(packageName, bytes);
        var checksums = Encoding.UTF8.GetBytes($"{new string('c', 64)}  other-download.zip\n{hash}  {packageName}\n");
        feed.Write("SHA256SUMS.txt", checksums);
        feed.Write("SHA256SUMS.txt.sig", TestSigning.Sign(checksums));
        feed.Write("latest.json", ComposeReleaseJson(NewVersionTag, packageName, true));

        return new ReleaseInfo
        {
            Version = new Version(9, 0, 0, 0),
            PackageName = packageName,
            PackageUri = new Uri(LocalFeed.Address + packageName),
            ChecksumsUri = new Uri(LocalFeed.Address + "SHA256SUMS.txt"),
            SignatureUri = new Uri(LocalFeed.Address + "SHA256SUMS.txt.sig")
        };
    }

    public static void Pack(string source, string package, string packageName)
    {
        if(packageName.EndsWith(".tar.gz", StringComparison.Ordinal))
        {
            using(var file = File.Create(package))
            {
                using(var gzip = new GZipStream(file, CompressionLevel.Fastest))
                {
                    TarFile.CreateFromDirectory(source, gzip, false);
                }
            }
        }
        else
        {
            ZipFile.CreateFromDirectory(source, package);
        }
    }

    public static UpdateInstaller CreateInstaller(HttpMessageHandler handler, ReleaseInfo release, InstallLocation install, IProgress<UpdateProgress>? progress, UpdateCancellation? cancellation)
    {
        return new UpdateInstaller(new HttpClient(handler), release, install, new Version(1, 0), progress, cancellation) { TrustedKey = TestSigning.PublicKey };
    }

    /// <summary>
    /// Every file under the folder, by its path from the folder with forward slashes, with its bytes: what a folder holds, to
    /// compare before and after.
    /// </summary>
    public static Dictionary<string, string> Snapshot(string folder)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach(var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            files[Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/')] = Convert.ToHexString(HashOf(file));

        return files;
    }

    private static byte[] HashOf(string file)
    {
        // a file that another copy holds exclusively (its lock) cannot be read: it counts as one of its own
        try
        {
            using(var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                return SHA256.HashData(stream);
        }
        catch(IOException)
        {
            return Encoding.UTF8.GetBytes("held open");
        }
    }

    public static List<string> ListFolder(string folder)
    {
        var names = new List<string>();
        foreach(var entry in Directory.EnumerateFileSystemEntries(folder))
            names.Add(Path.GetFileName(entry));

        names.Sort(StringComparer.Ordinal);
        return names;
    }
}