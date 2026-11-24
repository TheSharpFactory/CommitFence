using System.Text.RegularExpressions;
using CF.Languages.CSharp.Toolchain;

namespace CF.Languages.CSharp.EditorConfig;

/// <summary>
/// Finds the folder of the SDK that the machine's dotnet would build the repository with, so that its files can be read.
/// </summary>
internal sealed partial class SdkFolder
{
    private static readonly TimeSpan _timeLimit = TimeSpan.FromMinutes(1);

    private readonly DotnetTool _dotnet;

    public SdkFolder(DotnetTool dotnet)
    {
        _dotnet = dotnet;
    }

    [GeneratedRegex(@"^(?<version>\S+) \[(?<folder>.+)\]\s*$")]
    private static partial Regex ListedSdk();

    /// <summary>
    /// The folder of the SDK for the repository, or nothing when dotnet cannot be started, says it has no fitting SDK or
    /// does not list it.
    /// </summary>
    public string? Find(string repositoryFolder)
    {
        var check = new SdkCheck(_dotnet).Check(repositoryFolder);
        if(check.Problem != null)
            return null;

        var listing = _dotnet.Run(repositoryFolder, new List<string> { "--list-sdks" }, _timeLimit);
        if(listing.ExitCode != 0)
            return null;

        var match = listing.Output.Split('\n').Select(line => ListedSdk().Match(line.TrimEnd('\r'))).FirstOrDefault(candidate => candidate.Success && candidate.Groups["version"].Value == check.Version);
        return match == null ? null : Path.Combine(match.Groups["folder"].Value, check.Version);
    }
}
