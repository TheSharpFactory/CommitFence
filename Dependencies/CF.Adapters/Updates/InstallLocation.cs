using System.Diagnostics;

namespace CF.Adapters.Updates;

/// <summary>
/// Where the running program was unpacked, and whether an update can replace it there. A release download is
/// commitfence(.exe), commitfenceapp(.exe), the folder analyzers and the licence files in one folder, or only
/// commitfence and what goes with it; on macOS the app is CommitFence.app, which holds both programs.
/// </summary>
internal sealed class InstallLocation
{
    public const string CommandLineName = "commitfence";
    public const string AppName = "commitfenceapp";
    public const string BundleName = "CommitFence.app";

    private const string _notReleaseProblem = "This copy of the program is not a release download, so it cannot update itself.";

    /// <summary>
    /// The folder the download's files and folders go to (on macOS the folder that holds the .app).
    /// </summary>
    public string Folder { get; init; } = string.Empty;

    /// <summary>
    /// The running program; the same path in the new download is the program started after the update.
    /// </summary>
    public string Executable { get; init; } = string.Empty;

    /// <summary>
    /// The .app bundle on macOS (started with "open"); null elsewhere.
    /// </summary>
    public string? Bundle { get; init; }

    /// <summary>
    /// Why the program cannot update itself here, for the user; null when it can.
    /// </summary>
    public string? Problem { get; init; }

    /// <summary>
    /// Which download this folder takes: the full one where the app is, the command line's where only it is.
    /// </summary>
    public PackageKind Kind { get; init; } = PackageKind.CommandLine;

    /// <summary>
    /// The system the program runs on (each has its own release layout).
    /// </summary>
    public static AppPlatform CurrentPlatform
    {
        get
        {
            if(OperatingSystem.IsWindows())
                return AppPlatform.Windows;

            return OperatingSystem.IsMacOS() ? AppPlatform.MacOS : AppPlatform.Linux;
        }
    }

    /// <summary>
    /// The running program's path relative to <see cref="Folder" />, which is where it is in the download too.
    /// </summary>
    public string RelativeExecutable
    {
        get { return Path.GetRelativePath(Folder, Executable); }
    }

    /// <summary>
    /// Finds the release layout around the running program. A build run from the source (commitfence.dll next to the
    /// program) and a macOS app run from its temporary read-only copy cannot be updated in place.
    /// </summary>
    public static InstallLocation Find(string? processPath, AppPlatform platform)
    {
        if(string.IsNullOrWhiteSpace(processPath))
            return new InstallLocation { Problem = "The program could not find where it runs from." };

        var folder = Path.GetDirectoryName(processPath) ?? string.Empty;
        var fileName = Path.GetFileName(processPath);

        // on macOS both programs are inside the bundle; a command line of its own (the tar.gz) is a plain file
        if(platform == AppPlatform.MacOS && (string.Equals(fileName, AppName, StringComparison.Ordinal) || IsInsideBundle(folder)))
            return FindBundle(processPath, folder, fileName);

        var extension = platform == AppPlatform.Windows ? ".exe" : string.Empty;
        var isApp = string.Equals(fileName, AppName + extension, StringComparison.OrdinalIgnoreCase);
        var isCommandLine = string.Equals(fileName, CommandLineName + extension, StringComparison.OrdinalIgnoreCase);
        if((!isApp && !isCommandLine) || File.Exists(Path.Combine(folder, Path.GetFileNameWithoutExtension(fileName) + ".dll")))
            return new InstallLocation { Executable = processPath, Problem = _notReleaseProblem };

        var kind = isApp || HoldsApp(folder, platform) ? PackageKind.App : PackageKind.CommandLine;
        return new InstallLocation { Folder = folder, Executable = processPath, Kind = kind };
    }

    private static bool HoldsApp(string folder, AppPlatform platform)
    {
        var extension = platform == AppPlatform.Windows ? ".exe" : string.Empty;
        return platform != AppPlatform.MacOS && File.Exists(Path.Combine(folder, AppName + extension));
    }

    private static bool IsInsideBundle(string folder)
    {
        return string.Equals(Path.GetFileName(folder), "MacOS", StringComparison.Ordinal);
    }

    private static InstallLocation FindBundle(string processPath, string folder, string fileName)
    {
        // CommitFence.app/Contents/MacOS/commitfenceapp, and commitfence beside it
        var contents = Path.GetDirectoryName(folder);
        var bundle = contents == null ? null : Path.GetDirectoryName(contents);
        var isReleaseLayout = (string.Equals(fileName, AppName, StringComparison.Ordinal) || string.Equals(fileName, CommandLineName, StringComparison.Ordinal))
            && string.Equals(Path.GetFileName(folder), "MacOS", StringComparison.Ordinal)
            && string.Equals(Path.GetFileName(contents), "Contents", StringComparison.Ordinal)
            && string.Equals(Path.GetFileName(bundle), BundleName, StringComparison.Ordinal);

        if(!isReleaseLayout || bundle == null)
            return new InstallLocation { Executable = processPath, Problem = _notReleaseProblem };

        // macOS runs an app opened straight from a download from a temporary read-only copy
        if(processPath.Contains("/AppTranslocation/", StringComparison.Ordinal))
        {
            return new InstallLocation
            {
                Executable = processPath,
                Problem = "macOS is running the app from a temporary copy. Move CommitFence.app to Applications (or any folder), open it from there and update again."
            };
        }

        return new InstallLocation { Folder = Path.GetDirectoryName(bundle) ?? string.Empty, Executable = processPath, Bundle = bundle, Kind = PackageKind.App };
    }

    /// <summary>
    /// Starts the program from this location: the program, or the bundle through "open" on macOS. The folder the app
    /// worked on, if any, is its argument again.
    /// </summary>
    public ProcessStartInfo CreateStartInfo(string? argument)
    {
        var startInfo = Bundle != null ? new ProcessStartInfo("open") : new ProcessStartInfo(Executable);
        startInfo.UseShellExecute = false;
        startInfo.WorkingDirectory = Folder;
        if(Bundle != null)
        {
            startInfo.ArgumentList.Add("-n");
            startInfo.ArgumentList.Add(Bundle);
        }

        if(!string.IsNullOrWhiteSpace(argument))
        {
            if(Bundle != null)
                startInfo.ArgumentList.Add("--args");

            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}