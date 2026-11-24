using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Runs the machine's own dotnet through the process port.
/// </summary>
/// <remarks>
/// English messages, no telemetry, no build servers left behind, nothing added to the user's PATH or profile by the
/// SDK's first-run step, and none of git's variables, which a hook runs with and which would point a build at the
/// developer's repository. CommitFence never sets DOTNET_CLI_HOME: the SDK keeps using the user's own folder.
/// </remarks>
internal sealed class DotnetTool
{
    private static readonly IReadOnlyDictionary<string, string?> _environment = new Dictionary<string, string?>
    {
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",

        // The SDK's first-run step, on a machine where dotnet has not run before: it adds the global tools folder
        // (.dotnet/tools) to the user's PATH. This stops that (documented as DOTNET_ADD_GLOBAL_TOOLS_TO_PATH).
        ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0",

        // No welcome text and no telemetry notice on first use.
        ["DOTNET_NOLOGO"] = "1",

        // Older SDKs: do not run the first-time experience, which unpacks and caches packages in the user's profile.
        ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",

        // First run: do not generate and trust an ASP.NET Core HTTPS development certificate in the user's certificate
        // store.
        ["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "false",

        // No background download of the lists of workload updates (that is network use), and no workload integrity
        // check on first run.
        ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "1",
        ["DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"] = "1",
        ["DOTNET_CLI_UI_LANGUAGE"] = "en",
        ["VSLANG"] = "1033",
        ["MSBUILDDISABLENODEREUSE"] = "1",
        ["GIT_DIR"] = null,
        ["GIT_WORK_TREE"] = null,
        ["GIT_INDEX_FILE"] = null,
        ["GIT_PREFIX"] = null,
        ["GIT_OBJECT_DIRECTORY"] = null,
        ["GIT_ALTERNATE_OBJECT_DIRECTORIES"] = null,
        ["GIT_QUARANTINE_PATH"] = null
    };

    private readonly IProcessRunner _processRunner;

    public DotnetTool(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public ProcessResult Run(string workingFolder, IReadOnlyList<string> arguments, TimeSpan timeLimit)
    {
        return _processRunner.Run(new ProcessRequest("dotnet", arguments, workingFolder, _environment, null, timeLimit));
    }

    /// <summary>
    /// The error lines of a tool's output, each without the project file the tool appends, the first few joined into
    /// one sentence.
    /// </summary>
    public static string SummarizeErrors(ProcessResult result)
    {
        if(result.TimedOut)
            return result.Error.Trim();

        var lines = (result.Output + "\n" + result.Error).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var errors = lines.Where(line => line.Contains("error ", StringComparison.Ordinal)).Select(RemoveProjectSuffix).Distinct().Take(3).ToList();
        if(errors.Count > 0)
            return string.Join("; ", errors);

        var first = lines.FirstOrDefault();
        return first ?? $"dotnet ended with exit code {result.ExitCode} and said nothing";
    }

    private static string RemoveProjectSuffix(string line)
    {
        var bracket = line.LastIndexOf(" [", StringComparison.Ordinal);
        return bracket > 0 && line.EndsWith(']') ? line.Substring(0, bracket) : line;
    }
}