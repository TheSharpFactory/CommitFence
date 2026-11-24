using CF.Adapters.Machine;
using CF.Core.Ports;

namespace CF.Tests.Toolchain.Support;

/// <summary>
/// The real process runner, with variables of the child's environment replaced: what a build writes to the user's profile
/// and to the machine-wide package cache goes into the test's own folder instead.
/// </summary>
internal sealed class IsolatedProcessRunner : IProcessRunner
{
    private readonly ProcessRunner _inner = new ProcessRunner();
    private readonly Dictionary<string, string?> _variables;

    public IsolatedProcessRunner(string home, string packages)
    {
        _variables = new Dictionary<string, string?>
        {
            ["DOTNET_CLI_HOME"] = home,
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
            ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0",
            ["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "false",
            ["NUGET_PACKAGES"] = packages,
            ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(home, "http-cache"),
            ["NUGET_PLUGINS_CACHE_PATH"] = Path.Combine(home, "plugins-cache")
        };
    }

    public ProcessResult Run(ProcessRequest request)
    {
        var environment = new Dictionary<string, string?>(request.Environment);
        foreach(var variable in _variables)
            environment[variable.Key] = variable.Value;

        return _inner.Run(new ProcessRequest(request.FileName, request.Arguments, request.WorkingFolder, environment, request.StandardInput, request.TimeLimit));
    }
}
