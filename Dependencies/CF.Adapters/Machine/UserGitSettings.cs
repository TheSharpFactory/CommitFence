using CF.Adapters.Git;
using CF.Core.Ports;

namespace CF.Adapters.Machine;

/// <summary>
/// The user's global git settings, read and written by running git itself.
/// </summary>
/// <remarks>
/// That way git's own rules for the file, quoting and the variable GIT_CONFIG_GLOBAL (which checks use to point it at a
/// scratch file) apply.
/// </remarks>
public sealed class UserGitSettings : IUserGitSettings
{
    private const string _gitProgram = "git";
    private const int _notSetExitCode = 1;

    private static readonly IReadOnlyDictionary<string, string?> _environment = new Dictionary<string, string?>
    {
        ["GIT_TERMINAL_PROMPT"] = "0"
    };

    private readonly IProcessRunner _processRunner;

    /// <summary>
    /// Creates the adapter.
    /// </summary>
    public UserGitSettings(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    /// <inheritdoc />
    public string? Read(string key)
    {
        var result = Run(new List<string> { "config", "--global", "--get", key });
        if(result.ExitCode == _notSetExitCode)
            return null;

        if(result.ExitCode != 0)
            throw new GitException(Describe("read", key, result));

        return result.Output.Trim();
    }

    /// <inheritdoc />
    public void Write(string key, string value)
    {
        var result = Run(new List<string> { "config", "--global", key, value });
        if(result.ExitCode != 0)
            throw new GitException(Describe("write", key, result));
    }

    private ProcessResult Run(IReadOnlyList<string> arguments)
    {
        return _processRunner.Run(new ProcessRequest(_gitProgram, arguments, null, _environment, null, GitTimeLimits.Setting));
    }

    private static string Describe(string action, string key, ProcessResult result)
    {
        var reason = string.IsNullOrWhiteSpace(result.Error) ? $"exit code {result.ExitCode}" : result.Error.Trim();
        return $"git could not {action} the setting {key}: {reason}";
    }
}