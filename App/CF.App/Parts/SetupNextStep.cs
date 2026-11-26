using CF.App.Review;

namespace CF.App.Parts;

/// <summary>The command a developer can copy after writing a file that belongs in the repository.</summary>
internal sealed class SetupNextStep
{
    public SetupNextStep(string worktreeRoot, string path, string detail, string message, Func<ITextClipboard?> clipboard)
    {
        Lead = $"Commit and share {path}";
        Detail = " " + detail;
        var plainPath = path.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '/' or '_' or '-')
            && !path.StartsWith('-');
        var pathArgument = plainPath ? path : $"'{path.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
        var rootArgument = $"\"{EscapeRoot(worktreeRoot)}\"";
        IReadOnlyList<string> git = plainPath ? ["git", "-C", rootArgument] : ["git", "-C", rootArgument, "--literal-pathspecs"];
        AddWords = plainPath ? [.. git, "add", pathArgument] : [.. git, "add", "--", pathArgument];
        CommitWords = [.. git, "commit", "-m", .. $"\"{message}\"".Split(' '), "--", pathArgument];
        Command = $"{string.Join(' ', AddWords)} && {string.Join(' ', CommitWords)}";
        Copy = new CopyButtonViewModel("Copy", () => Command, clipboard, spokenName: "Copy the git command");
    }

    private static string EscapeRoot(string root)
    {
        // Use PowerShell on Windows and POSIX shell quoting elsewhere.
        if(OperatingSystem.IsWindows())
            return root.Replace('\\', '/').Replace("`", "``", StringComparison.Ordinal).Replace("$", "`$", StringComparison.Ordinal);

        return root.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal).Replace("`", "\\`", StringComparison.Ordinal);
    }

    public string Lead { get; }

    public string Detail { get; }

    public IReadOnlyList<string> AddWords { get; }
    public IReadOnlyList<string> CommitWords { get; }
    public string Command { get; }

    public CopyButtonViewModel Copy { get; }
}
