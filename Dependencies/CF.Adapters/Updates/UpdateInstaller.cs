using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CF.Core.Updates;

namespace CF.Adapters.Updates;

/// <summary>
/// Installs a release over the running program: downloads its package next to the program, checks it against the
/// release's SHA256SUMS.txt (signed with CommitFence's key), unpacks it, then swaps the files. The running program
/// cannot be overwritten (Windows), but it can be renamed, so every file the package replaces is first renamed to
/// "*.cf-old"; the new version deletes those when it starts. If a step fails, the files already swapped are put back
/// and the program is unchanged.
/// </summary>
internal sealed class UpdateInstaller
{
    public const string OldSuffix = ".cf-old";
    public const string StagingFolderName = ".commitfence-update";

    /// <summary>
    /// Held open, unshared, in the download folder while an update runs: another copy of the program (its clean-up
    /// when it starts, or its own update) leaves that folder alone.
    /// </summary>
    public const string LockFileName = ".lock";

    private static readonly TimeSpan _checksumsTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _downloadTimeout = TimeSpan.FromMinutes(30);

    private readonly HttpClient _http;
    private readonly ReleaseInfo _release;
    private readonly InstallLocation _location;
    private readonly Version _currentVersion;
    private readonly IProgress<UpdateProgress>? _progress;
    private readonly UpdateCancellation? _cancellation;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// <paramref name="progress" /> and <paramref name="cancellation" /> may be null (no progress, no Cancel).
    /// </summary>
    public UpdateInstaller(HttpClient http, ReleaseInfo release, InstallLocation location, Version currentVersion, IProgress<UpdateProgress>? progress, UpdateCancellation? cancellation)
    {
        _http = http;
        _release = release;
        _location = location;
        _currentVersion = currentVersion;
        _progress = progress;
        _cancellation = cancellation;
        _cancellationToken = cancellation != null ? cancellation.Token : CancellationToken.None;
    }

    /// <summary>
    /// The public key (PEM) SHA256SUMS.txt must be signed for: the release key built into the program (tests sign with
    /// their own).
    /// </summary>
    public string TrustedKey { get; set; } = UpdateSignature.ReleaseKey;

    /// <summary>
    /// How long the update waits for the reviews that are running to end before it replaces the files.
    /// </summary>
    public TimeSpan ReviewsWaitLimit { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// For tests: answers true for the name of a file whose move into place must fail. Nothing sets it in the program.
    /// </summary>
    internal Func<string, bool>? FailsMoveOf { get; set; }

    /// <summary>
    /// Runs every step; throws UpdateException with a message for the user when one fails.
    /// </summary>
    public async Task RunAsync()
    {
        var staging = Path.Combine(_location.Folder, StagingFolderName);

        // outside the try: when another copy is updating, its download folder must not be deleted here
        var stagingLock = CreateStaging(staging);
        try
        {
            var package = Path.Combine(staging, _release.PackageName ?? string.Empty);
            await DownloadAsync(package);
            var unpacked = Path.Combine(staging, "package");
            Unpack(package, unpacked);
            CheckPackage(unpacked);

            // no review runs while the files are replaced; Cancel still works while this waits
            using(await HoldProgramAloneAsync())
            {
                // the point of no return: Cancel works until here, never after (the swap is quick, and stopping it
                // halfway would only mean putting files back)
                if(_cancellation != null && !_cancellation.TryBeginReplacing())
                    throw new OperationCanceledException(_cancellationToken);

                _progress?.Report(new UpdateProgress { Stage = UpdateStage.Replacing });
                Swap(unpacked);
            }
        }
        finally
        {
            stagingLock.Dispose();
            DeleteQuietly(staging);
        }
    }

    /// <summary>
    /// Takes the program's lock alone: a review that is running holds it shared, and the update waits for it.
    /// </summary>
    private async Task<FileStream> HoldProgramAloneAsync()
    {
        try
        {
            var held = await ProgramLockFile.TryHoldAloneAsync(_location.Folder, ReviewsWaitLimit, _cancellationToken);
            if(held == null)
                throw new UpdateException("A review is still running, so the program was not replaced. Try again when it has ended. Nothing was changed.");

            return held;
        }
        catch(UnauthorizedAccessException exception)
        {
            throw new UpdateException($"The program cannot write to {_location.Folder}, so it cannot update itself there.", exception);
        }
    }

    /// <summary>
    /// Downloads SHA256SUMS.txt, its signature and the package, and checks the signature and the package's SHA-256.
    /// A lost connection or the time limit, while asking or while reading, is an UpdateException; Cancel is an
    /// OperationCanceledException.
    /// </summary>
    private async Task DownloadAsync(string package)
    {
        try
        {
            var expectedHash = await DownloadExpectedHashAsync();
            var actualHash = await DownloadPackageAsync(package);
            if(!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                throw new UpdateException($"The download of version {_release.Version} is damaged (its SHA-256 does not match the release's). Nothing was changed.");
        }
        catch(HttpRequestException exception)
        {
            throw new UpdateException($"The download failed ({exception.Message}). Nothing was changed.", exception);
        }
        catch(IOException exception)
        {
            // a connection lost while reading (HttpIOException), or the download could not be saved
            throw new UpdateException($"The download failed ({exception.Message}). Nothing was changed.", exception);
        }
        catch(OperationCanceledException exception) when(!_cancellationToken.IsCancellationRequested)
        {
            throw new UpdateException("The download took too long. Nothing was changed.", exception);
        }
    }

    /// <summary>
    /// A fresh download folder, and its lock (held until the update ends).
    /// </summary>
    private FileStream CreateStaging(string staging)
    {
        if(IsStagingInUse(staging))
            throw new UpdateException("Another copy of the program is updating this folder right now. Wait for it to finish.");

        try
        {
            DeleteEntry(staging);
            Directory.CreateDirectory(staging);
            return new FileStream(Path.Combine(staging, LockFileName), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            throw new UpdateException($"The program cannot write to {_location.Folder}, so it cannot update itself there.", exception);
        }
    }

    /// <summary>
    /// True while an update (of this copy or another) holds the download folder's lock. On Linux and macOS .NET
    /// takes an advisory lock for FileShare.None, so this works there too.
    /// </summary>
    public static bool IsStagingInUse(string staging)
    {
        var lockFile = Path.Combine(staging, LockFileName);
        if(!File.Exists(lockFile))
            return false;

        try
        {
            using(new FileStream(lockFile, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                return false;
            }
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>
    /// The package's SHA-256 from SHA256SUMS.txt (lines of "hash  file name", as sha256sum writes them), once its
    /// signature (SHA256SUMS.txt.sig) shows CommitFence's key signed it.
    /// </summary>
    private async Task<string> DownloadExpectedHashAsync()
    {
        using(var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken))
        {
            timeout.CancelAfter(_checksumsTimeout);
            var checksums = await DownloadSmallFileAsync(_release.ChecksumsUri, timeout.Token);
            var signature = await DownloadSmallFileAsync(_release.SignatureUri, timeout.Token);
            if(!UpdateSignature.IsValid(checksums, signature, TrustedKey))
                throw new UpdateException($"The download of version {_release.Version} is not signed by CommitFence, so it was not installed. Nothing was changed.");

            var hash = FindHash(Encoding.UTF8.GetString(checksums), _release.PackageName ?? string.Empty);
            if(hash == null)
                throw new UpdateException($"The release has no checksum for {_release.PackageName}. Nothing was changed.");

            return hash;
        }
    }

    private async Task<byte[]> DownloadSmallFileAsync(Uri? uri, CancellationToken cancellationToken)
    {
        if(uri == null)
            throw new UpdateException($"Version {_release.Version} has no checksum list the program can check. Nothing was changed.");

        using(var request = UpdateChecker.CreateRequest(uri, _currentVersion))
        {
            using(var response = await SendAsync(request, cancellationToken))
            {
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
        }
    }

    public static string? FindHash(string? checksums, string fileName)
    {
        if(checksums == null)
            return null;

        foreach(var line in checksums.Split('\n'))
        {
            var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if(parts.Length != 2)
                continue;

            // "*name" is sha256sum's binary mode
            var name = parts[1].TrimStart('*');
            if(string.Equals(name, fileName, StringComparison.Ordinal) && parts[0].Length == 64)
                return parts[0];
        }

        return null;
    }

    /// <summary>
    /// Saves the package and returns its SHA-256 (hex), reporting the percentage done.
    /// </summary>
    private async Task<string> DownloadPackageAsync(string package)
    {
        using(var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken))
        {
            timeout.CancelAfter(_downloadTimeout);
            using(var request = UpdateChecker.CreateRequest(_release.PackageUri ?? throw new UpdateException($"Version {_release.Version} has no download for this system. Nothing was changed."), _currentVersion))
            {
                using(var response = await SendAsync(request, timeout.Token))
                {
                    var length = response.Content.Headers.ContentLength;
                    using(var source = await response.Content.ReadAsStreamAsync(timeout.Token))
                    {
                        using(var target = new FileStream(package, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            return await SaveAsync(source, target, length, timeout.Token);
                        }
                    }
                }
            }
        }
    }

    private async Task<string> SaveAsync(Stream source, Stream target, long? length, CancellationToken cancellationToken)
    {
        using(var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            var buffer = new byte[81920];
            var total = 0L;
            var lastPercent = -1;
            int read;
            while((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                hash.AppendData(buffer, 0, read);
                total += read;

                var percent = length > 0 ? (int)(total * 100 / length.Value) : -1;
                if(percent != lastPercent)
                {
                    lastPercent = percent;
                    _progress?.Report(new UpdateProgress { Stage = UpdateStage.Downloading, Percent = percent });
                }
            }

            return Convert.ToHexString(hash.GetHashAndReset());
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if(!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new UpdateException($"The download failed: {request.RequestUri} answered {status}. Nothing was changed.");
        }

        return response;
    }

    /// <summary>
    /// A .zip (Windows, the macOS app) or a .tar.gz (Linux, the macOS command line).
    /// </summary>
    private static void Unpack(string package, string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            if(package.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                using(var file = File.OpenRead(package))
                {
                    using(var gzip = new GZipStream(file, CompressionMode.Decompress))
                    {
                        TarFile.ExtractToDirectory(gzip, folder, false);
                    }
                }
            }
            else
            {
                ZipFile.ExtractToDirectory(package, folder);
            }
        }
        catch(Exception exception) when(exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException)
        {
            throw new UpdateException($"The download could not be unpacked ({exception.Message}). Nothing was changed.", exception);
        }
    }

    /// <summary>
    /// The package must have the program at the same place as the running one; on Linux and macOS the programs are
    /// made executable (a .zip does not always keep that).
    /// </summary>
    private void CheckPackage(string unpacked)
    {
        var program = Path.Combine(unpacked, _location.RelativeExecutable);
        if(!File.Exists(program))
            throw new UpdateException($"The download has no {_location.RelativeExecutable}. Nothing was changed.");

        if(OperatingSystem.IsWindows())
            return;

        MakeExecutable(program);
        foreach(var folder in new[] { Path.GetDirectoryName(program) ?? unpacked, unpacked })
        {
            foreach(var name in new[] { InstallLocation.CommandLineName, InstallLocation.AppName })
            {
                var other = Path.Combine(folder, name);
                if(File.Exists(other))
                    MakeExecutable(other);
            }
        }
    }

    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private static void MakeExecutable(string file)
    {
        var mode = File.GetUnixFileMode(file);
        File.SetUnixFileMode(file, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    /// <summary>
    /// Renames each file or folder the package replaces to "*.cf-old" and moves the new one in, the programs and the
    /// analyzers folder together. On a failure the moves done so far are undone in reverse.
    /// </summary>
    private void Swap(string unpacked)
    {
        var done = new List<SwapStep>();
        try
        {
            // the list is taken first: the loop moves the entries out of the folder
            foreach(var entry in Directory.GetFileSystemEntries(unpacked))
            {
                var name = Path.GetFileName(entry);
                var step = new SwapStep(entry, Path.Combine(_location.Folder, name));

                DeleteEntry(step.OldPath);
                if(DoesEntryExist(step.TargetPath))
                {
                    MoveEntry(step.TargetPath, step.OldPath);
                    step.MovedTarget = true;
                }

                done.Add(step);
                if(FailsMoveOf != null && FailsMoveOf(name))
                    throw new IOException($"{name} could not be moved.");

                MoveEntry(step.NewPath, step.TargetPath);
                step.MovedNew = true;
            }
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            if(Undo(done))
                throw new UpdateException($"The files in {_location.Folder} could not be replaced ({exception.Message}). The program was left as it was.", exception);

            throw new UpdateException($"The files in {_location.Folder} could not be replaced ({exception.Message}), and some of the old ones could not be put back. Download version {_release.Version} from its release page and unpack it there again.", exception);
        }
    }

    /// <summary>
    /// Puts back what the swap moved, last first. Returns false when something could not be put back.
    /// </summary>
    private static bool Undo(List<SwapStep> done)
    {
        var restored = true;
        for(var i = done.Count - 1; i >= 0; i--)
        {
            var step = done[i];
            try
            {
                if(step.MovedNew)
                    MoveEntry(step.TargetPath, step.NewPath);

                if(step.MovedTarget)
                    MoveEntry(step.OldPath, step.TargetPath);
            }
            catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
            {
                restored = false;
            }
        }

        return restored;
    }

    /// <summary>
    /// Deletes what an earlier update left in the program's folder: the "*.cf-old" files and folders and the download
    /// folder. A program that has not exited yet cannot be deleted (Windows): tries again a few times, then leaves it
    /// for the next start.
    /// </summary>
    public static async Task CleanUpAsync(string folder, int attempts)
    {
        if(string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;

        for(var attempt = 0; attempt < attempts; attempt++)
        {
            if(TryCleanUp(folder))
                return;

            if(attempt + 1 < attempts)
                await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    /// <summary>
    /// One try at the clean-up; false when something could not be deleted yet. While an update runs it leaves
    /// everything alone: the old files are what it would put back.
    /// </summary>
    public static bool TryCleanUp(string folder)
    {
        if(IsStagingInUse(Path.Combine(folder, StagingFolderName)))
            return true;

        var clean = true;
        try
        {
            // the list is taken first: the loop deletes entries from the folder
            foreach(var entry in Directory.GetFileSystemEntries(folder))
            {
                var name = Path.GetFileName(entry);
                var isStaging = string.Equals(name, StagingFolderName, StringComparison.Ordinal);
                if(!name.EndsWith(OldSuffix, StringComparison.Ordinal) && !isStaging)
                    continue;

                try
                {
                    DeleteEntry(entry);
                }
                catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
                {
                    clean = false;
                }
            }
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            // a folder that cannot be listed has nothing the program can clean; the next start looks again
            return false;
        }

        return clean;
    }

    private static bool DoesEntryExist(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    private static void MoveEntry(string from, string to)
    {
        if(Directory.Exists(from))
            Directory.Move(from, to);
        else
            File.Move(from, to);
    }

    private static void DeleteEntry(string path)
    {
        if(Directory.Exists(path))
            Directory.Delete(path, true);
        else if(File.Exists(path))
            File.Delete(path);
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            DeleteEntry(path);
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            // what is left is deleted by the next start
        }
    }

    /// <summary>
    /// One file or folder of the package: where it is, what it replaces, and how far the swap got.
    /// </summary>
    private sealed class SwapStep
    {
        public SwapStep(string newPath, string targetPath)
        {
            NewPath = newPath;
            TargetPath = targetPath;
            OldPath = targetPath + OldSuffix;
        }

        public string NewPath { get; }

        public string TargetPath { get; }

        public string OldPath { get; }

        public bool MovedTarget { get; set; }

        public bool MovedNew { get; set; }
    }
}