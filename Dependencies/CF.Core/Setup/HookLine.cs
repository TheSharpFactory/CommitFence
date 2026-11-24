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
    /// <summary>
    /// The line, word for word.
    /// </summary>
    public const string PrePush = "\"$(git config --get commitfence.path)\" review --hook pre-push \"$@\" || exit $?";

    /// <summary>
    /// The name of the user's git setting that holds the full path of the program.
    /// </summary>
    public const string PathSetting = "commitfence.path";
}