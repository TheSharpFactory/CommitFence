namespace CF.Cli.Commands;

/// <summary>
/// The arguments after the word review, made for one form: --files with one or more paths, --staged, --hook pre-push
/// with the remote's name and address, --base with an optional --head, or --all with an optional folder or project
/// file; and an optional --settings path.
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

    public static ReviewArgumentsResult Parse(string[] args)
    {
        return new ReviewArgumentsReader(args).Read();
    }

    public static ReviewArguments ForFiles(IReadOnlyList<string> files, string? settingsPath)
    {
        return new ReviewArguments(ReviewMode.Files, settingsPath) { Files = files };
    }

    public static ReviewArguments ForHook(string remoteName, string remoteUrl, string? settingsPath)
    {
        return new ReviewArguments(ReviewMode.Hook, settingsPath) { RemoteName = remoteName, RemoteUrl = remoteUrl };
    }

    public static ReviewArguments ForPush(string? settingsPath)
    {
        return new ReviewArguments(ReviewMode.Push, settingsPath);
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