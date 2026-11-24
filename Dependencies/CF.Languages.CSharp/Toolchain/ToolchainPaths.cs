namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Paths of the build, written one way so that they can be compared: full, with forward slashes.
/// </summary>
/// <remarks>
/// Case matters as it does on the system: it is ignored on Windows and macOS and counts on Linux, where two paths that
/// differ only by case are two files.
/// </remarks>
internal static class ToolchainPaths
{
    public static StringComparison Comparison { get; } = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static StringComparer Comparer { get; } = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string Normalize(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/');
    }

    public static string Combine(string folder, string relativePath)
    {
        return Normalize(Path.Combine(folder, relativePath));
    }

    public static string FolderOf(string path)
    {
        return Normalize(Path.GetDirectoryName(Normalize(path)) ?? string.Empty);
    }

    public static bool IsUnder(string folder, string path)
    {
        var prefix = folder.TrimEnd('/') + "/";
        return path.StartsWith(prefix, Comparison);
    }

    public static string RelativeTo(string folder, string path)
    {
        var normalizedFolder = Normalize(folder).TrimEnd('/');
        var normalizedPath = Normalize(path);
        return IsUnder(normalizedFolder, normalizedPath) ? normalizedPath.Substring(normalizedFolder.Length + 1) : normalizedPath;
    }
}