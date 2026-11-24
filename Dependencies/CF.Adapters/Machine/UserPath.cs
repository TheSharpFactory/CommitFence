using System.Runtime.Versioning;
using CF.Core.Ports;
using Microsoft.Win32;

namespace CF.Adapters.Machine;

/// <summary>
/// The user's own PATH.
/// </summary>
/// <remarks>
/// On Windows it is the Path value under HKEY_CURRENT_USER\Environment, which needs no administrator rights; it is
/// never the machine's PATH. When the variable COMMITFENCE_USER_PATH_FILE names a file, the PATH is that file's text
/// instead on every system, which is how checks redirect it. On other systems without it the PATH belongs to a shell
/// profile and cannot be changed.
/// </remarks>
public sealed class UserPath : IUserPath
{
    private const string _redirectVariable = "COMMITFENCE_USER_PATH_FILE";
    private const string _environmentKey = "Environment";
    private const string _pathValue = "Path";

    /// <inheritdoc />
    public bool CanChange
    {
        get { return RedirectFile() != null || OperatingSystem.IsWindows(); }
    }

    /// <inheritdoc />
    public bool Contains(string folder)
    {
        var wanted = Normalize(folder);
        return ReadEntries().Any(entry => string.Equals(Normalize(entry), wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public void Add(string folder)
    {
        var current = ReadText().TrimEnd(Path.PathSeparator);
        var changed = current.Length == 0 ? folder : current + Path.PathSeparator + folder;
        WriteText(changed);
    }

    private static string? RedirectFile()
    {
        var file = Environment.GetEnvironmentVariable(_redirectVariable);
        return string.IsNullOrWhiteSpace(file) ? null : Path.GetFullPath(file);
    }

    private static string Normalize(string folder)
    {
        return folder.Trim().Replace('\\', '/').TrimEnd('/');
    }

    private static IEnumerable<string> ReadEntries()
    {
        return ReadText().Split(Path.PathSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ReadText()
    {
        var file = RedirectFile();
        if(file != null)
            return File.Exists(file) ? File.ReadAllText(file).Trim() : string.Empty;

        return ReadRegistry();
    }

    private static void WriteText(string text)
    {
        var file = RedirectFile();
        if(file == null)
            WriteRegistry(text);
        else
            File.WriteAllText(file, text);
    }

    private static string ReadRegistry()
    {
        if(!OperatingSystem.IsWindows())
            return string.Empty;

        using(var key = Registry.CurrentUser.OpenSubKey(_environmentKey))
        {
            return key?.GetValue(_pathValue, string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? string.Empty;
        }
    }

    private static void WriteRegistry(string text)
    {
        if(!OperatingSystem.IsWindows())
            throw new IOException("this system has no user PATH that can be changed");

        using(var key = Registry.CurrentUser.CreateSubKey(_environmentKey))
        {
            SetPath(key, text);
        }

        EnvironmentBroadcast.Announce();
    }

    /// <summary>
    /// Writes the value in the kind it already has.
    /// </summary>
    /// <remarks>
    /// This is so that a plain string does not start to expand names such as %NAME% in its other entries. A value that
    /// does not exist yet is created as the expandable kind, which is what Windows itself writes.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static void SetPath(RegistryKey key, string text)
    {
        var kind = key.GetValue(_pathValue) == null ? RegistryValueKind.ExpandString : key.GetValueKind(_pathValue);
        key.SetValue(_pathValue, text, kind == RegistryValueKind.ExpandString ? RegistryValueKind.ExpandString : RegistryValueKind.String);
    }
}