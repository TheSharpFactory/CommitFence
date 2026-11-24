namespace CF.Cli.Commands;

/// <summary>
/// The text printed when the command line is used wrongly or help is asked for: one line for each command.
/// </summary>
internal static class UsageText
{
    private static readonly string[] _lines =
    {
        "Usage:",
        "  commitfence --help",
        "  commitfence --version",
        "  commitfence install [--print] [--no-path]",
        "  commitfence update [--check]    (installs the newest release; --check only says whether there is one)",
        "  commitfence init",
        "  commitfence catalog [<language> [<artifact type>]]",
        "  commitfence rules list",
        "  commitfence rules show <id>",
        "  commitfence rules enable <id>",
        "  commitfence rules disable <id>",
        "  commitfence rules severity <id> block|warn",
        "  commitfence rules add <id> --for <type> --title <text> [--language <id>] [--severity block|warn] [--where <property=value>]... (--require <property=value>... | --forbid) [--paths <a,b>] [--message <text>] [--why <text>] [--before <id>]",
        "  commitfence rules set <id> [--for <type>] [--title <text>] [--where <property=value>... | --no-where] [--require <property=value>... | --forbid] [--paths <a,b> | --no-paths] [--message <text> | --no-message] [--why <text> | --no-why]",
        "  commitfence rules copy <id> <new id>    (a copy right after the rule, titled Copy of its title)",
        "  commitfence rules remove <id>",
        "  commitfence rules try <id> [<file>]    (standard input without a file)",
        "  commitfence review --files <path>...",
        "  commitfence review --staged",
        "  commitfence review --hook pre-push <remote name> <remote url>",
        "  commitfence review --push    (what a push of the current branch would send, as the pre-push hook is told)",
        "  commitfence review --base <revision> [--head <revision>]",
        "  commitfence review --all [<folder or project file>]",
        "rules list and rules show also cover the rules that .editorconfig sets; the product never changes that file.",
        "Every rules command takes [--settings <path>]; review takes [--settings <path>]."
    };

    /// <summary>
    /// The whole text, a line for each command.
    /// </summary>
    public static string Text
    {
        get { return string.Join(Environment.NewLine, _lines); }
    }
}