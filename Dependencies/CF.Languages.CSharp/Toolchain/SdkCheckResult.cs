namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// What the machine's dotnet says about the tree: the version of the SDK it would use, or a plain message that says
/// what is missing.
/// </summary>
internal sealed class SdkCheckResult
{
    private SdkCheckResult(string version, string? problem)
    {
        Version = version;
        Problem = problem;
    }

    public string Version { get; }

    public string? Problem { get; }

    public static SdkCheckResult Ready(string version)
    {
        return new SdkCheckResult(version, null);
    }

    public static SdkCheckResult NotReady(string problem)
    {
        return new SdkCheckResult(string.Empty, problem);
    }
}