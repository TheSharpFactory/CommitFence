namespace CF.Core.Setup;

/// <summary>
/// The one line a team adds to its pre-push hook, as the first command.
/// </summary>
/// <remarks>
/// It asks git for the program, so that no search of the PATH can find an older copy, and it ends in an exit so that a
/// failure stops the push.
/// </remarks>
public static class HookLine
{
    internal const string PrePushCommand =
        """
        p="$(git config --get commitfence.path)"; [ -n "$p" ] && [ -f "$p" ] || { echo "CommitFence is not set up on this machine: run commitfence install from the folder you unpacked it to." >&2; exit 1; }; "$p" review --hook pre-push "$@"
        """;

    /// <summary>
    /// The line, word for word.
    /// </summary>
    public const string PrePush = PrePushCommand + " || exit $?";

    internal static readonly string PreCommitCommand = PrePushCommand.Replace("--hook pre-push", "--hook pre-commit --quick", StringComparison.Ordinal);

    /// <summary>The line for reviewing the index before a commit.</summary>
    public static readonly string PreCommit = PreCommitCommand + " || exit $?";

    internal static string For(HookKind kind)
    {
        return kind == HookKind.PrePush ? PrePush : PreCommit;
    }

    /// <summary>
    /// The name of the user's git setting that holds the full path of the program.
    /// </summary>
    public const string PathSetting = "commitfence.path";
}