namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// How long each use of dotnet may run before it is stopped. A hook must end: a build that hangs is never a pass.
/// </summary>
internal static class DotnetTimeLimits
{
    /// <summary>
    /// Asking dotnet which SDK it would use. It only reads global.json and the installed SDKs.
    /// </summary>
    public static readonly TimeSpan SdkQuestion = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Asking the build to evaluate one project without compiling it.
    /// </summary>
    public static readonly TimeSpan ProjectEvaluation = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Restoring the packages of a project from the packages already on the machine; no network is used, so this is
    /// only disk work.
    /// </summary>
    public static readonly TimeSpan PackageRestore = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Compiling one project of the team's own, which can be large; twenty minutes is long for a push and short enough
    /// to end a build that is stuck.
    /// </summary>
    public static readonly TimeSpan Build = TimeSpan.FromMinutes(20);
}