using Avalonia.Platform.Storage;
using Microsoft.Win32;

namespace CF.App.Parts;

/// <summary>Uses a line-aware URI when the registered editor supports one.</summary>
internal static class SourceFileLauncher
{
    private const string _codeEditor = "Visual Studio Code";

    /// <summary>
    /// The name of the editor that opens a file at a line for a file of this kind, or nothing when the system's program for it
    /// does not take a line.
    /// </summary>
    public static string? FindEditorName(string extension = ".cs")
    {
        return IsCodeRegistered(extension) ? _codeEditor : null;
    }

    public static async Task OpenAsync(ILauncher launcher, string path, int line)
    {
        if(IsCodeRegistered(Path.GetExtension(path)))
        {
            var file = new Uri(Path.GetFullPath(path));
            // a file on a network share keeps its server's name outside the path: the editor's link cannot name it
            if(!file.IsUnc && await launcher.LaunchUriAsync(new Uri($"vscode://file/{file.AbsolutePath.TrimStart('/')}:{line}")))
                return;
        }

        await launcher.LaunchFileInfoAsync(new FileInfo(path));
    }

    private static bool IsCodeRegistered(string extension)
    {
        if(!OperatingSystem.IsWindows())
            return false;

        var choice = Registry.GetValue($@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice", "ProgId", null)
            ?? Registry.GetValue($@"HKEY_CLASSES_ROOT\{extension}", string.Empty, null);
        return choice is string id && id.StartsWith("VSCode.", StringComparison.OrdinalIgnoreCase);
    }
}
