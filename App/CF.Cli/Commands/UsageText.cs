using CF.Core.Setup;

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
        "  commitfence init [--hook pre-push|pre-commit] [--print]",
        "  commitfence doctor",
        "  commitfence stats [--all]",
        "  commitfence settings show",
        "  commitfence settings set strictness <quiet|standard|strict>",
        "  commitfence structure show",
        "  commitfence structure set add-module <name>",
        "  commitfence structure set rename-module <name> <new name>",
        "  commitfence structure set remove-module <name>",
        "  commitfence structure set add-part <module> <kind:pattern>",
        "  commitfence structure set remove-part <module> <kind:pattern>",
        "  commitfence structure set allow <from> <to>",
        "  commitfence structure set stop-allowing <from> <to>",
        "  commitfence exemptions list [--rule <id>]    (the lines of the last commit that set a rule aside)",
        "  commitfence catalog [<language> [<artifact type>]]",
        "  commitfence rules list",
        "  commitfence rules show <id>",
        "  commitfence rules enable <id>",
        "  commitfence rules disable <id>",
        "  commitfence rules severity <id> block|warn",
        "  commitfence rules add <id> --for <type> --title <text> [--language <id>] [--severity block|warn] [--where <condition>]... (--require <condition>... | --forbid) [--paths <a,b>] [--message <text>] [--why <text>] [--before <id>]",
        "  Conditions: property=value, property=a,b, property<=n, property>=n, property!=value, property~pattern",
        "  commitfence rules set <id> [--for <type>] [--title <text>] [--where <condition>... | --no-where] [--require <condition>... | --forbid] [--paths <a,b> | --no-paths] [--message <text> | --no-message] [--why <text> | --no-why]",
        "  commitfence rules copy <id> <new id>    (a copy right after the rule, titled Copy of its title)",
        "  commitfence rules remove <id>",
        "  commitfence rules try <id> [<file>]    (standard input without a file)",
        "  commitfence review --files <path>... [--quick]",
        "  commitfence fix <rule> <file> [--apply] [[--push] [--head <branch>] | --staged | --base <revision> [--head <revision>] | --all [<target>]] [--settings <path>]",
        "  commitfence review --staged [--quick]",
        "  commitfence review --hook pre-commit [--quick]",
        "  commitfence review --hook pre-push <remote name> <remote url>",
        "  commitfence review --push    (what a push of the current branch would send, as the pre-push hook is told)",
        "  commitfence review --base <revision> [--head <revision>]",
        "  commitfence review --all [<folder or project file>]",
        "Put this first in the pre-push hook:",
        "  " + HookLine.PrePush,
        "rules list and rules show also cover the rules that .editorconfig sets; the product never changes that file.",
        "Every rules command takes [--settings <path>]; review takes [--settings <path>] and [--format text|prompt]    (prompt prints what was found as text to hand to an AI coding agent)."
    };

    /// <summary>
    /// The whole text, a line for each command.
    /// </summary>
    public static string Text
    {
        get { return string.Join(Environment.NewLine, _lines); }
    }
}
