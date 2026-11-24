using Avalonia.Platform.Storage;
using Microsoft.Win32;

namespace CF.App.Parts;

/// <summary>Uses a line-aware URI when the registered editor supports one.</summary>
internal static class SourceFileLauncher
{
    public static async Task OpenAsync(ILauncher launcher, string path, int line)
    {
        if(OperatingSystem.IsWindows())
        {
            var extension = Path.GetExtension(path);
            var choice = Registry.GetValue($@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice", "ProgId", null)
                ?? Registry.GetValue($@"HKEY_CLASSES_ROOT\{extension}", string.Empty, null);
            if(choice is string id && id.StartsWith("VSCode.", StringComparison.OrdinalIgnoreCase))
            {
                var file = new Uri(Path.GetFullPath(path));
                // a file on a network share keeps its server's name outside the path: the editor's link cannot name it
                if(!file.IsUnc && await launcher.LaunchUriAsync(new Uri($"vscode://file/{file.AbsolutePath.TrimStart('/')}:{line}")))
                    return;
            }
        }

        await launcher.LaunchFileInfoAsync(new FileInfo(path));
    }
}