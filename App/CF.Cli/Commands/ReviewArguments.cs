namespace CF.Cli.Commands;

/// <summary>
/// The arguments after the word review, made for one form: --files with one or more paths, --staged, --hook pre-push
/// with the remote's name and address, --base with an optional --head, or --all with an optional folder or project
/// file; and an optional --settings path, --format and --output.
/// </summary>
internal sealed class ReviewArguments
{
    private ReviewArguments(ReviewMode mode, string? settingsPath)
    {
        Mode = mode;
        SettingsPath = settingsPath;
    }

    public ReviewMode Mode { get; }

    public string? SettingsPath { get; }

    public IReadOnlyList<string> Files { get; private init; } = new List<string>();

    public string RemoteName { get; private init; } = string.Empty;

    public string RemoteUrl { get; private init; } = string.Empty;

    public string BaseRevision { get; private init; } = string.Empty;

    public string HeadRevision { get; private init; } = string.Empty;

    public string Target { get; private init; } = string.Empty;

    /// <summary>
    /// Whether the review is printed as a prompt for an AI coding agent (--format prompt) in place of the usual report.
    /// </summary>
    public bool AsPrompt { get; internal set; }

    /// <summary>Whether the review is written as a SARIF 2.1.0 log (--format sarif) for build servers.</summary>
    public bool AsSarif { get; internal set; }

    /// <summary>The file the report is written to (--output); the console then keeps the plain text.</summary>
    public string? OutputPath { get; internal set; }

    /// <summary>Whether the report is printed in place of the plain text: a prompt or a SARIF log with no file to go to.</summary>
    public bool ReplacesConsoleText
    {
        get { return (AsPrompt || AsSarif) && OutputPath == null; }
    }

    public bool Quick { get; set; }

    public static ReviewArgumentsResult Parse(string[] args, bool allowPushHead = false)
    {
        return new ReviewArgumentsReader(args, allowPushHead).Read();
    }

    public static ReviewArguments ForFiles(IReadOnlyList<string> files, string? settingsPath)
    {
        return new ReviewArguments(ReviewMode.Files, settingsPath) { Files = files };
    }

    public static ReviewArguments ForHook(string remoteName, string remoteUrl, string? settingsPath)
    {
        return new ReviewArguments(ReviewMode.Hook, settingsPath) { RemoteName = remoteName, RemoteUrl = remoteUrl };
    }

    public static ReviewArguments ForPush(string? settingsPath, string headRevision = "")
    {
        return new ReviewArguments(ReviewMode.Push, settingsPath) { HeadRevision = headRevision };
    }

    public static ReviewArguments ForStaged(string? settingsPath)
    {
        return new ReviewArguments(ReviewMode.Staged, settingsPath);
    }

    public static ReviewArguments ForRange(string baseRevision, string headRevision, string? settingsPath)
    {
        return new ReviewArguments(ReviewMode.Range, settingsPath) { BaseRevision = baseRevision, HeadRevision = headRevision };
    }

    public static ReviewArguments ForAll(string target, string? settingsPath)
    {
        return new ReviewArguments(ReviewMode.All, settingsPath) { Target = target };
    }
}
