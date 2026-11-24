using System.Text.RegularExpressions;
using CF.Composition;

namespace CF.App.Check;

/// <summary>
/// One rule as <c>commitfence rules list</c> prints it.
/// </summary>
internal sealed record ListedRule(string Id, string Severity, string Title, bool IsOff)
{
    public bool Blocks
    {
        get { return !IsOff && Severity == "block"; }
    }

    public bool Warns
    {
        get { return !IsOff && Severity == "warn"; }
    }
}

/// <summary>
/// What every group of checks shares: the command-line program, the sample folders, the services of the app, the report,
/// and the small helpers they all use.
/// </summary>
internal sealed partial class CheckContext
{
    [GeneratedRegex(@"^(?<id>\S+)\s+(?<severity>block|warn)\s+\S+\s+\S+\s+(?<title>.*?)(?<off>\s+\(off\))?$")]
    private static partial Regex ListLine();

    public CheckContext(CheckOptions options, CommandLine commandLine, SampleFolders folders, CommitFenceServices services, CheckReport report)
    {
        Options = options;
        CommandLine = commandLine;
        Folders = folders;
        Services = services;
        Report = report;
    }

    public CheckOptions Options { get; }

    public CommandLine CommandLine { get; }

    public SampleFolders Folders { get; }

    public CommitFenceServices Services { get; }

    public CheckReport Report { get; }

    public AppWindow Open(string folder, double width = 1440, double height = 900)
    {
        return new AppWindow(Services, folder, width, height);
    }

    public static byte[] SettingsBytes(string folder)
    {
        return File.ReadAllBytes(Path.Combine(folder, SampleFolders.SettingsName));
    }

    public static string Hex(byte[] bytes)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).Substring(0, 12);
    }

    /// <summary>
    /// Checks that the settings file of the folder the app wrote is byte for byte the one the command line wrote on a second copy.
    /// </summary>
    public void SameBytes(string caseName, string text, string appFolder, string commandLineFolder)
    {
        var fromApp = SettingsBytes(appFolder);
        var fromCli = SettingsBytes(commandLineFolder);
        Report.Check(caseName, text, fromApp.AsSpan().SequenceEqual(fromCli), $"the app wrote {fromApp.Length} bytes ({Hex(fromApp)}), the command line {fromCli.Length} bytes ({Hex(fromCli)})");
    }

    public CommandResult Cli(string folder, params string[] arguments)
    {
        return CommandLine.Run(folder, arguments);
    }

    /// <summary>
    /// The rules <c>commitfence rules list</c> prints for the folder, in its order.
    /// </summary>
    public IReadOnlyList<ListedRule> ListedByCommandLine(string folder)
    {
        var result = Cli(folder, "rules", "list");
        var rules = new List<ListedRule>();
        foreach(var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = ListLine().Match(line);
            if(match.Success)
                rules.Add(new ListedRule(match.Groups["id"].Value, match.Groups["severity"].Value, match.Groups["title"].Value.Trim(), match.Groups["off"].Success));
        }

        return rules;
    }

    /// <summary>
    /// Runs the body; a body that throws is reported as a check that stopped, and the others go on.
    /// </summary>
    public void Guarded(string caseName, string text, Action body)
    {
        try
        {
            body();
        }
        catch(Exception exception)
        {
            Report.Broken(caseName, text, exception);
        }
    }
}
