using System.Diagnostics;
using CF.Adapters.Updates;
using CF.Core.Ports;
using CF.Core.Setup;

namespace CF.Adapters.Machine;

/// <summary>Reads executable paths and versions without starting a configured program. The running program's own version is given.</summary>
public sealed class DoctorMachine(IFileSystem files, string ownVersion) : IDoctorMachine
{
    private const UnixFileMode _executeBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <inheritdoc />
    public string ProgramPath { get; } = Path.Combine(AppContext.BaseDirectory, InstallLocation.CommandLineName + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));

    /// <inheritdoc />
    public string? FindOnPath(string name)
    {
        var extensions = OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD" : string.Empty;
        var suffixes = extensions.Split(';', StringSplitOptions.TrimEntries);
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach(var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var folder = entry.Trim('"');
            if(!Path.IsPathFullyQualified(folder))
                continue;

            foreach(var suffix in suffixes)
            {
                var candidate = Path.Combine(folder, name + suffix);
                if(IsExecutable(candidate))
                    return candidate;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public bool SamePath(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(files.ResolveRealPath(left), files.ResolveRealPath(right), comparison);
    }

    /// <inheritdoc />
    public string? ReadVersion(string path)
    {
        var running = Environment.ProcessPath;
        if(running != null && SamePath(path, running) && SamePath(running, ProgramPath))
            return ProductVersion.TryParseOwn(ownVersion)?.ToString();

        var version = FileVersionInfo.GetVersionInfo(path);
        if(version.ProductName == "CommitFence" || version.ProductName == InstallLocation.CommandLineName || version.ProductName == "CF.Meaning")
            return version.ProductVersion;

        return null;
    }

    /// <inheritdoc />
    public bool IsExecutable(string path)
    {
        return files.FileExists(path) && (OperatingSystem.IsWindows() || (File.GetUnixFileMode(path) & _executeBits) != 0);
    }
}
