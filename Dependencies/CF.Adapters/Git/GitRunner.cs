using CF.Core.Ports;

namespace CF.Adapters.Git;

/// <summary>
/// Runs the git command for one repository: always on git's folder by name, never asking for a login, never using the
/// network, and never taking the folder or work tree from the environment of a hook.
/// </summary>
internal sealed class GitRunner
{
    private const string _gitProgram = "git";

    private static readonly IReadOnlyDictionary<string, string?> _baseEnvironment = new Dictionary<string, string?>
    {
        ["GIT_DIR"] = null,
        ["GIT_WORK_TREE"] = null,
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_OPTIONAL_LOCKS"] = "0",
        ["GIT_LFS_SKIP_SMUDGE"] = "1",

        // In a partial clone git fetches a missing object from the remote by itself (a lazy fetch). Git 2.44 (February
        // 2024) introduced this variable to stop that: the command fails with the missing object instead. Older git
        // does not know the variable and ignores it, so there a partial clone can still reach the network.
        ["GIT_NO_LAZY_FETCH"] = "1"
    };

    private readonly IProcessRunner _processRunner;

    public GitRunner(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public string? ReadVersion()
    {
        var arguments = new List<string> { "--version" };
        var result = _processRunner.Run(new ProcessRequest(_gitProgram, arguments, null, _baseEnvironment, null, GitTimeLimits.Query));
        if(result.ExitCode == -1)
            return null;

        const string prefix = "git version ";
        if(result.ExitCode == 0 && result.Output.StartsWith(prefix, StringComparison.Ordinal))
            return result.Output.Substring(prefix.Length).Trim();

        throw new GitException(Describe(arguments, result));
    }

    public ProcessResult Run(RepositoryLocation repository, IReadOnlyList<string> arguments)
    {
        return Run(repository, arguments, new Dictionary<string, string?>(), null);
    }

    public ProcessResult Run(RepositoryLocation repository, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string?> environment, string? standardInput)
    {
        var fullArguments = new List<string> { "--git-dir=" + repository.GitDirectory };
        fullArguments.AddRange(arguments);

        var result = _processRunner.Run(new ProcessRequest(_gitProgram, fullArguments, repository.WorkFolder, MergeEnvironment(environment), standardInput, GitTimeLimits.Query));

        return RefuseTimeout(arguments, result);
    }

    public ProcessResult RunOutsideRepository(string folder, IReadOnlyList<string> arguments)
    {
        var fullArguments = new List<string> { "-C", folder };
        fullArguments.AddRange(arguments);

        var result = _processRunner.Run(new ProcessRequest(_gitProgram, fullArguments, null, _baseEnvironment, null, GitTimeLimits.Query));

        return RefuseTimeout(arguments, result);
    }

    public string RunChecked(RepositoryLocation repository, IReadOnlyList<string> arguments)
    {
        return RunChecked(repository, arguments, new Dictionary<string, string?>(), null);
    }

    public string RunChecked(RepositoryLocation repository, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string?> environment, string? standardInput)
    {
        var result = Run(repository, arguments, environment, standardInput);
        if(result.ExitCode != 0)
            throw new GitException(Describe(arguments, result));

        return result.Output;
    }

    /// <summary>
    /// A git command that ran out of time says nothing about the repository: it is never read as "no" or as "nothing
    /// found", so it ends the review as incomplete.
    /// </summary>
    private static ProcessResult RefuseTimeout(IReadOnlyList<string> arguments, ProcessResult result)
    {
        if(result.TimedOut)
            throw new GitException(Describe(arguments, result), timedOut: true);

        return result;
    }

    public static string Describe(IReadOnlyList<string> arguments, ProcessResult result)
    {
        var command = string.Join(" ", arguments.Where(argument => !argument.StartsWith('-') && !argument.Contains('=')).Take(1));
        var reason = string.IsNullOrWhiteSpace(result.Error) ? $"exit code {result.ExitCode}" : result.Error.Trim();

        return $"git {command} failed: {reason}";
    }

    private static IReadOnlyDictionary<string, string?> MergeEnvironment(IReadOnlyDictionary<string, string?> environment)
    {
        var merged = new Dictionary<string, string?>(_baseEnvironment);
        foreach(var variable in environment)
            merged[variable.Key] = variable.Value;

        return merged;
    }
}
