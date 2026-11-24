namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Asks the machine's dotnet, started in the tree under review so that its global.json counts, which SDK it would use.
/// </summary>
/// <remarks>
/// CommitFence never installs an SDK: when there is none, or not the one the tree asks for, the review says so.
/// </remarks>
internal sealed class SdkCheck
{
    private readonly DotnetTool _dotnet;

    public SdkCheck(DotnetTool dotnet)
    {
        _dotnet = dotnet;
    }

    /// <summary>
    /// The version of the SDK the tree would be built with, or a plain message that says what is missing and what to do.
    /// </summary>
    public SdkCheckResult Check(string treeFolder)
    {
        var result = _dotnet.Run(treeFolder, new List<string> { "--version" }, DotnetTimeLimits.SdkQuestion);
        if(result.ExitCode == 0)
            return SdkCheckResult.Ready(result.Output.Trim());

        if(result.TimedOut)
            return SdkCheckResult.NotReady("The machine is not ready: the dotnet command did not answer in time (" + DotnetTool.SummarizeErrors(result) + "), so the C# code cannot be built. Check that dotnet runs from a terminal, then try again.");

        if(result.ExitCode == -1)
            return SdkCheckResult.NotReady("The machine is not ready: the .NET SDK is not installed (the dotnet command cannot be started), so the C# code cannot be built. Install the .NET SDK the repository uses, then try again.");

        return SdkCheckResult.NotReady("The machine is not ready: no installed .NET SDK fits the global.json of this repository (dotnet says: " + DotnetTool.SummarizeErrors(result) + "). Install the SDK version global.json asks for, or change global.json, then try again.");
    }
}