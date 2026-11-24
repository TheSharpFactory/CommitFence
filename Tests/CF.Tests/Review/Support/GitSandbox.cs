using System.Security.Cryptography;
using System.Text;
using CF.Adapters.Machine;
using CF.Core.Ports;
using CF.Core.Setup;
using Xunit;

namespace CF.Tests.Review.Support;

/// <summary>
/// A folder with everything a real git and the real program need to run without touching the user's machine: a git
/// configuration of its own, a home folder, a data folder for the program and a file for its PATH. Every process is
/// started from C# with its arguments as a list and gets this environment.
/// </summary>
internal sealed class GitSandbox : IDisposable
{
    private static readonly TimeSpan _timeLimit = TimeSpan.FromMinutes(10);

    private readonly ProcessRunner _runner = new ProcessRunner();
    private readonly Dictionary<string, string?> _environment;
    private readonly string _real;

    public GitSandbox()
    {
        _real = Path.Combine(TestPaths.Temp, "cf-sandbox-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_real);
        Root = _real;
        if(!OperatingSystem.IsWindows())
        {
            // reached through a link, as the temporary folder of a Mac is (/var is a link to /private/var)
            Root = _real + "-link";
            Directory.CreateSymbolicLink(Root, _real);
        }

        var home = Path.Combine(Root, "home");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(Root, "gitconfig"), "[user]\n\tname = Check Runner\n\temail = check@example.invalid\n[init]\n\tdefaultBranch = main\n[core]\n\tautocrlf = false\n\tlongpaths = true\n[advice]\n\tdetachedHead = false\n");
        _environment = new Dictionary<string, string?>
        {
            ["GIT_CONFIG_GLOBAL"] = Path.Combine(Root, "gitconfig"),
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["HOME"] = home,
            ["USERPROFILE"] = home,
            ["XDG_CONFIG_HOME"] = Path.Combine(home, ".config"),
            ["COMMITFENCE_DATA_FOLDER"] = DataFolder,
            ["COMMITFENCE_USER_PATH_FILE"] = Path.Combine(Root, "userpath.txt"),
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GIT_OPTIONAL_LOCKS"] = "0",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["GIT_DIR"] = null,
            ["GIT_WORK_TREE"] = null,
            ["GIT_INDEX_FILE"] = null,
            ["GIT_PREFIX"] = null
        };
    }

    public string Root { get; }

    /// <summary>
    /// A process runner for the product's own adapters (the snapshot store, git), which gives every process the
    /// environment of this sandbox.
    /// </summary>
    public IProcessRunner Runner
    {
        get { return new SandboxRunner(this); }
    }

    /// <summary>
    /// Where the program keeps its private copies and answers.
    /// </summary>
    public string DataFolder
    {
        get { return Path.Combine(Root, "data"); }
    }

    /// <summary>
    /// The command line, as the tests were built with it, beside this assembly with the analyzer it hands to the compiler.
    /// </summary>
    public static string ProgramPath
    {
        get { return Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "commitfence.exe" : "commitfence"); }
    }

    /// <summary>
    /// Starts the program and waits for it. The extra variables are set for this process only; a value of nothing removes
    /// the variable.
    /// </summary>
    public ProcessResult Run(string program, string? folder, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string?>? extra = null)
    {
        return _runner.Run(new ProcessRequest(program, arguments, folder, WithSandbox(extra), null, _timeLimit));
    }

    /// <summary>
    /// Starts what the request asks for, with this sandbox's environment under the request's own.
    /// </summary>
    public ProcessResult Execute(ProcessRequest request)
    {
        return _runner.Run(new ProcessRequest(request.FileName, request.Arguments, request.WorkingFolder, WithSandbox(request.Environment), request.StandardInput, request.TimeLimit));
    }

    public ProcessResult RunProgram(string folder, params string[] arguments)
    {
        return Run(ProgramPath, folder, arguments);
    }

    public ProcessResult RunProgramWith(string folder, IReadOnlyDictionary<string, string?> extra, params string[] arguments)
    {
        return Run(ProgramPath, folder, arguments, extra);
    }

    /// <summary>
    /// Runs git in the folder and gives its result, whatever it is.
    /// </summary>
    public ProcessResult RunGit(string folder, params string[] arguments)
    {
        return Run("git", folder, arguments);
    }

    public ProcessResult RunGitWith(string folder, IReadOnlyDictionary<string, string?> extra, params string[] arguments)
    {
        return Run("git", folder, arguments, extra);
    }

    /// <summary>
    /// Runs git in the folder and fails the test when git does not end well. Returns what git wrote.
    /// </summary>
    public string Git(string folder, params string[] arguments)
    {
        var result = Run("git", folder, arguments);
        Assert.True(result.ExitCode == 0, $"git {string.Join(' ', arguments)} ended with {result.ExitCode}: {result.Error}");

        return result.Output.TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Installs the program as the team's hook would find it: through its own install command, which sets the git setting
    /// the hook line reads. The PATH is left alone.
    /// </summary>
    public void InstallProgram()
    {
        var result = RunProgram(Root, "install", "--no-path");

        Assert.True(result.ExitCode == 0, "install ended with " + result.ExitCode + ": " + result.Error);
        Assert.Equal(ProgramPath.Replace('\\', '/'), Git(Root, "config", "--global", "--get", HookLine.PathSetting));
    }

    /// <summary>
    /// A repository with an empty bare repository as its remote named origin.
    /// </summary>
    public SandboxRepository CreateRepository(string name)
    {
        var remote = Path.Combine(Root, "remotes", name + ".git");
        var folder = Path.Combine(Root, "repositories", name);
        Directory.CreateDirectory(remote);
        Directory.CreateDirectory(folder);
        Git(remote, "init", "--bare");
        Git(folder, "init");
        Git(folder, "remote", "add", "origin", remote);

        return new SandboxRepository(this, folder, remote);
    }

    private Dictionary<string, string?> WithSandbox(IReadOnlyDictionary<string, string?>? extra)
    {
        var environment = new Dictionary<string, string?>(_environment);
        foreach(var variable in extra ?? new Dictionary<string, string?>())
            environment[variable.Key] = variable.Value;

        return environment;
    }

    public void Dispose()
    {
        if(!string.Equals(Root, _real, StringComparison.Ordinal) && Directory.Exists(Root))
            Directory.Delete(Root);

        if(!Directory.Exists(_real))
            return;

        // git keeps its objects read-only, which a plain delete does not remove on Windows
        foreach(var entry in Directory.EnumerateFileSystemEntries(_real, "*", SearchOption.AllDirectories))
            File.SetAttributes(entry, FileAttributes.Normal);

        Directory.Delete(_real, true);
    }
}

/// <summary>
/// Starts what the product asks for, with the environment of a <see cref="GitSandbox" /> on top of what the request sets.
/// </summary>
internal sealed class SandboxRunner : IProcessRunner
{
    private readonly GitSandbox _sandbox;

    public SandboxRunner(GitSandbox sandbox)
    {
        _sandbox = sandbox;
    }

    public ProcessResult Run(ProcessRequest request)
    {
        return _sandbox.Execute(request);
    }
}

/// <summary>
/// A repository of a <see cref="GitSandbox" /> and the bare repository it pushes to.
/// </summary>
internal sealed class SandboxRepository
{
    private readonly GitSandbox _sandbox;

    public SandboxRepository(GitSandbox sandbox, string folder, string remote)
    {
        _sandbox = sandbox;
        Folder = folder;
        Remote = remote;
    }

    public string Folder { get; }

    public string Remote { get; }

    public void Write(string path, string text)
    {
        var full = Path.Combine(Folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    public string Git(params string[] arguments)
    {
        return _sandbox.Git(Folder, arguments);
    }

    public string Commit(string message)
    {
        Git("add", "-A");
        Git("commit", "-q", "-m", message);

        return Git("rev-parse", "HEAD");
    }

    /// <summary>
    /// What the remote holds: each ref and its object, one to a line.
    /// </summary>
    public string RemoteRefs()
    {
        return _sandbox.Git(Remote, "for-each-ref", "--format=%(refname) %(objectname)");
    }

    /// <summary>
    /// The team's hook: the line that init prints, as the first command of the pre-push hook, followed by a command that
    /// succeeds, so that a failure that is lost on the way shows as a push that goes through.
    /// </summary>
    public void InstallTeamHook()
    {
        var init = _sandbox.RunProgram(Folder, "init");
        Assert.True(init.ExitCode == 0, "init ended with " + init.ExitCode + ": " + init.Error);
        var lines = init.Output.ReplaceLineEndings("\n").Split('\n');
        var hookLine = lines[Array.FindIndex(lines, line => line.StartsWith("Add this line", StringComparison.Ordinal)) + 1];
        Assert.Contains("review --hook pre-push", hookLine, StringComparison.Ordinal);

        var hook = Path.Combine(Folder, ".git", "hooks", "pre-push");
        File.WriteAllText(hook, "#!/bin/sh\n" + hookLine + "\nexit 0\n");
        if(!OperatingSystem.IsWindows())
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>
    /// A project that builds and a team's rules, committed with the source files given.
    /// </summary>
    public void WriteProject(string rules, params (string Path, string Text)[] files)
    {
        Write("Demo.csproj", ReviewSources.ProjectFile + "\n");
        Write(".gitignore", "bin/\nobj/\n");
        Write("README.txt", "readme\n");
        Write("commitfence.json", rules);
        foreach(var file in files)
            Write(file.Path, file.Text);
    }

    /// <summary>
    /// What the developer owns, as one text: the index, the settings, the hooks, every ref but the remote's, the stash,
    /// every file of the working folder and the status. Taking it changes none of them.
    /// </summary>
    public string CaptureDeveloperState()
    {
        var state = new StringBuilder();
        state.AppendLine("index " + HashOf(Path.Combine(Folder, ".git", "index")));
        state.AppendLine("config " + HashOf(Path.Combine(Folder, ".git", "config")));
        AppendFiles(state, "hook", Path.Combine(Folder, ".git", "hooks"));
        state.AppendLine("head " + Git("rev-parse", "HEAD"));
        state.AppendLine("branch " + Git("symbolic-ref", "-q", "HEAD"));
        state.AppendLine("refs " + Git("for-each-ref", "--format=%(refname) %(objectname)", "refs/heads", "refs/tags", "refs/stash"));
        state.AppendLine("stash " + Git("stash", "list"));
        AppendFiles(state, "file", Folder, ".git");
        state.AppendLine("status " + Git("status", "--porcelain"));

        return state.ToString();
    }

    /// <summary>
    /// An unstaged edit, an untracked file and an entry in the stash.
    /// </summary>
    public void LeaveWorkInProgress()
    {
        File.AppendAllText(Path.Combine(Folder, "README.txt"), "stashed\n");
        Git("stash");
        File.AppendAllText(Path.Combine(Folder, "README.txt"), "unstaged edit\n");
        Write("notes.txt", "untracked\n");
    }

    private static void AppendFiles(StringBuilder state, string label, string folder, string? skippedFolder = null)
    {
        if(!Directory.Exists(folder))
            return;

        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(folder, path).Replace('\\', '/')).Where(path => skippedFolder == null || !path.StartsWith(skippedFolder + "/", StringComparison.Ordinal)).Order(StringComparer.Ordinal);
        foreach(var file in files)
            state.AppendLine($"{label} {file} {HashOf(Path.Combine(folder, file))}");
    }

    private static string HashOf(string path)
    {
        return File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "absent";
    }
}
