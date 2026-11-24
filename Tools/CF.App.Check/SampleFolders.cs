using System.Diagnostics;

namespace CF.App.Check;

/// <summary>
/// Makes the sample repositories the cases work on: a folder named invoicing in a scratch folder, a git repository on
/// the branch main, holding a copy of the repository's own rules with TEAM012 switched off by the command line (so that the list
/// shows the off state). Every case gets a folder of its own.
/// </summary>
internal sealed class SampleFolders : IDisposable
{
    public const string SettingsName = "commitfence.json";

    private readonly CheckOptions _options;
    private readonly CommandLine _commandLine;
    private int _count;

    public SampleFolders(CheckOptions options, CommandLine commandLine)
    {
        _options = options;
        _commandLine = commandLine;
        Root = Path.Combine(Path.GetTempPath(), "commitfence-app-check-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        DataFolder = Path.Combine(Root, "data");
        Directory.CreateDirectory(DataFolder);
    }

    /// <summary>
    /// The scratch folder that holds everything the program makes; it is removed at the end.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// The data folder both programs are pointed at, so that no check touches the person's own.
    /// </summary>
    public string DataFolder { get; }

    public string NewFolder()
    {
        _count++;
        var folder = Path.Combine(Root, "case-" + _count, "invoicing");
        Directory.CreateDirectory(folder);
        File.Copy(_options.SampleSettingsPath, Path.Combine(folder, SettingsName));
        RunGit(folder, "init", "-q", "-b", "main");
        _commandLine.Run(folder, "rules", "disable", "TEAM012");

        return folder;
    }

    public void Dispose()
    {
        try
        {
            // git's own objects are read-only files, which a recursive delete refuses
            foreach(var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(Root, true);
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            // a file still held by the window's watcher: the system's temporary folder cleans it up
        }
    }

    private static void RunGit(string folder, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach(var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start);
        process?.WaitForExit(30000);
    }
}
