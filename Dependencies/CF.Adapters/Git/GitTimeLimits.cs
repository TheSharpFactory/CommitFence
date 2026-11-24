namespace CF.Adapters.Git;

/// <summary>
/// How long git may run before it is stopped. A hook must end: a git that hangs is never a pass.
/// </summary>
internal static class GitTimeLimits
{
    /// <summary>
    /// A question put to git about one repository (a tree, a diff, a ref). These end in seconds, even on a large
    /// repository; two minutes is generous and still ends a hung push.
    /// </summary>
    public static readonly TimeSpan Query = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Reading or writing one setting in the user's git configuration, which is a small file.
    /// </summary>
    public static readonly TimeSpan Setting = TimeSpan.FromSeconds(30);
}