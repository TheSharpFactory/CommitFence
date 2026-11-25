using CF.Adapters.Git;
using CF.Adapters.Machine;
using CF.Cli.Commands;
using CF.Core.Code;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CF.Core.Setup;
using CF.Languages.CSharp;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>Doctor reads the repository and machine facts and prints the shared report.</summary>
public sealed class DoctorTests
{
    private const string _first = "#!/bin/sh\n" + HookLine.PrePush + "\n";
    private const string _late = "#!/bin/sh\n# Git LFS\ngit lfs pre-push\n" + HookLine.PrePush + "\n";
    private const string _lateFound = "It runs on line 4 of .git/hooks/pre-push. Line 3, git lfs pre-push, reads the push's ref lines before it, so CommitFence gets none.";
    private const string _noStop = "#!/bin/sh\n\"$(git config --get commitfence.path)\" review --hook pre-push \"$@\"\nexit 0\n";
    private const string _global = "{\n  \"sdk\": {\n    \"version\": \"10.0.100\",\n    \"rollForward\": \"disable\"\n  }\n}\n";
    private static readonly SystemFileSystem _files = new SystemFileSystem();

    [Theory]
    [InlineData(_first, DoctorOutcome.Passes, "Line 2 of .git/hooks/pre-push, ending in || exit $?, so it can stop a push.", null)]
    [InlineData(_late, DoctorOutcome.Fails, _lateFound, 4)]
    [InlineData("#!/bin/sh\nprintf '%s' example\n" + HookLine.PrePush, DoctorOutcome.Fails, "It runs on line 3 of .git/hooks/pre-push. Line 2, printf '%s' example, runs before it and may read the push's ref lines.", 3)]
    [InlineData("#!/bin/sh\n# " + HookLine.PrePush + "\nexit 0\n", DoctorOutcome.Fails, "No line of .git/hooks/pre-push runs CommitFence.", null)]
    [InlineData(_noStop, DoctorOutcome.Fails, "Line 2 does not end in || exit $?, so it does not explicitly stop a push when CommitFence fails.", 2)]
    [InlineData("#!/bin/sh\n\"$(git config --get commitfence.path)\" review --hook pre-push \"$@\" || true\n", DoctorOutcome.Fails, "Line 2 does not end in || exit $?, so it does not explicitly stop a push when CommitFence fails.", 2)]
    [InlineData(null, DoctorOutcome.Fails, "The repository has no pre-push hook.", null)]
    [InlineData("#!/bin/sh\n# hook comment\n\n  # another comment\n" + HookLine.PrePush, DoctorOutcome.Passes, "Line 5 of .git/hooks/pre-push, ending in || exit $?, so it can stop a push.", null)]
    public void ChecksTheHookLineAndExplainsWhereItIs(string? script, DoctorOutcome outcome, string found, int? line)
    {
        using var folder = new TestFolder();
        var git = new DoctorGitStub(folder.Path);
        var check = new PrePushDoctorCheck(git, _files);
        if(script != null)
            Write(git.FindPrePushHook(git.Repository), script);

        var result = check.Run(Context(git.Repository));

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(found, result.Found.ToString());
        Assert.Equal(line, result.Fix?.Excerpt?.Line);
        if(result.Fix != null)
            Assert.Contains(result.Fix.Actions, action => action.Kind == DoctorActionKind.CopyText && action.Value == HookLine.PrePush);
    }

    [Theory]
    [InlineData(false, false, "1.2.3.0", DoctorOutcome.Fails, "commitfence.path is not set.")]
    [InlineData(true, false, "1.2.3.0", DoctorOutcome.Fails, "commitfence.path points at {path}, which is not there.")]
    [InlineData(true, true, "0.2", DoctorOutcome.Fails, "commitfence.path is {path}, version 0.2. The repository asks for 1.0.0.0 or later.")]
    [InlineData(true, true, "1.2.3.0", DoctorOutcome.Passes, "commitfence.path is {path}, version 1.2.3.0. The repository asks for 1.0.0.0 or later.")]
    public void ComparesTheConfiguredProgramWithTheRepositoryFloor(bool configured, bool exists, string version, DoctorOutcome outcome, string found)
    {
        using var folder = new TestFolder();
        var program = Path.Combine(folder.Path, "commitfence");
        Write(Path.Combine(folder.Path, SettingsFileName.Default), """{"requires":"1.0.0.0","rules":[]}""");
        if(exists)
            Write(program, "");

        var git = new DoctorGitStub(folder.Path) { ProgramSetting = configured ? program : null };
        var check = new HookProgramDoctorCheck(git, _files, new SettingsFile(_files, new SettingsReader([])));
        var result = check.Run(Context(git.Repository, version));

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(found.Replace("{path}", program, StringComparison.Ordinal), result.Found.ToString());
    }

    [Theory]
    [InlineData(_global, false, "global.json asks for 10.0.100 and does not roll forward; this machine has 11.0.100 only.")]
    [InlineData(null, true, "No global.json, so the newest: 11.0.100, which builds net11.0.")]
    [InlineData(_global, true, "global.json asks for 10.0.100 and dotnet selects 10.0.100.")]
    public void ExplainsTheSdkSelectionMadeByDotnet(string? globalJson, bool selected, string found)
    {
        using var folder = new TestFolder();
        var repository = new RepositoryLocation(folder.Path, Path.Combine(folder.Path, ".git"));
        var global = Path.Combine(folder.Path, "global.json");
        if(globalJson != null)
            Write(global, globalJson);

        var runner = Dotnet(selected, globalJson == null ? "11.0.100" : "10.0.100");
        var data = new FolderOfTest(Path.Combine(folder.Path, "data"));
        var language = new CSharpLanguage(runner, _files, data);
        var result = language.DoctorChecks[0].Run(Context(repository));

        Assert.Equal(selected ? DoctorOutcome.Passes : DoctorOutcome.Fails, result.Outcome);
        Assert.Equal(found, result.Found.ToString());
        Assert.Equal(selected && globalJson != null ? "10.0.100" : "11.0.100", result.SdkVersion);
        Assert.Equal(selected ? (int?)null : 3, result.Fix?.Excerpt?.Line);
    }

    [Fact]
    public void WithoutGitOnlyTheFirstCheckFails()
    {
        using var folder = new TestFolder();
        var runner = new ScriptedProcessRunner().Route(request => request.FileName == "git" && request.Arguments.Contains("--version"),
            _ => ScriptedProcessRunner.Failure(-1, "missing"));
        var data = new FolderOfTest(Path.Combine(folder.Path, "data"));
        var service = Service(folder.Path, new GitCommandLine(runner, data), data, runner, new ScriptedProcessRunner());

        var report = service.Check(folder.Path);

        Assert.False(report.Ready);
        Assert.Equal(7, report.Checks.Count);
        Assert.Equal(DoctorOutcome.Fails, report.Checks[0].Outcome);
        Assert.Equal("git was not found on the PATH.", report.Checks[0].Found.ToString());
        Assert.Equal("Not checked: it needs git to find the program.", report.Checks[1].Found.ToString());
        Assert.All(report.Checks.Skip(1), result => Assert.Equal(DoctorOutcome.NotChecked, result.Outcome));
        Assert.Single(runner.Requests);
    }

    [Theory]
    [InlineData(_late, 4, "Until it is fixed, a push that sends a commit ends as Review incomplete and is refused.")]
    [InlineData(_noStop, 2, "Until it is fixed, a review that refuses a push may not stop it.")]
    public void CommandShowsFailureWithSourceAndThenAReadyRepository(string script, int line, string verdict)
    {
        using var sandbox = new GitSandbox();
        var repo = sandbox.CreateRepository("doctor");
        var path = Path.Combine("hooks", "pre-push");
        var program = Path.Combine(repo.Folder, "commitfence");
        repo.Git("config", "core.hooksPath", "hooks");
        repo.Git("config", "commitfence.path", program);
        repo.Write(path, script);
        repo.Write("commitfence", "");
        repo.Write("analyzers/CF.Meaning.dll", "");
        repo.Write(".editorconfig", "[*.cs]\ndotnet_diagnostic.CA1822.severity = error\n");
        var data = new FolderOfTest(sandbox.DataFolder);
        var service = Service(repo.Folder, new GitCommandLine(sandbox.Runner, data), data, sandbox.Runner, Dotnet(true, "11.0.100"));

        var failing = RunCommand(repo.Folder, service, "doctor");

        Assert.Equal(ExitCode.Blocked, failing.Code);
        Assert.StartsWith("1 check fails", failing.Output, StringComparison.Ordinal);
        Assert.Contains("[Pass] The .NET SDK the repository needs is installed", failing.Output, StringComparison.Ordinal);
        Assert.Contains("[Pass] .editorconfig is found", failing.Output, StringComparison.Ordinal);
        Assert.Contains(verdict, failing.Output, StringComparison.Ordinal);
        Assert.Contains($"> {line} | " + script.Split('\n')[line - 1], failing.Output, StringComparison.Ordinal);
        Assert.Contains("This machine", failing.Output, StringComparison.Ordinal);
        Assert.Contains("Checked 1970-01-01 00:00:00 +00:00, in 0.0 s", failing.Output, StringComparison.Ordinal);
        Assert.Empty(failing.Error);
        Assert.Equal(script, File.ReadAllText(Path.Combine(repo.Folder, path)));
        Assert.False(Directory.Exists(data.RootFolder));

        repo.Write(path, _first);
        var ready = RunCommand(repo.Folder, service, "doctor");
        Assert.Equal(ExitCode.Passed, ready.Code);
        Assert.StartsWith("Ready for pushes", ready.Output, StringComparison.Ordinal);
        Assert.Equal(ExitCode.BadSettingsOrUsage, RunCommand(repo.Folder, service, "doctor", "--fix").Code);
    }

    [Fact]
    public void SnapshotInspectionDetectsTrackedDamageWithoutRepairingIt()
    {
        using var sandbox = new GitSandbox();
        var repo = sandbox.CreateRepository("copy");
        var data = new FolderOfTest(sandbox.DataFolder);
        var git = new GitCommandLine(sandbox.Runner, data);
        var store = new GitSnapshotStore(sandbox.Runner, data);
        var repository = git.FindRepository(repo.Folder)!;

        var absent = store.Inspect(repository);
        Assert.Equal(PrivateCopyState.Absent, absent.State);
        Assert.False(Directory.Exists(absent.Folder));

        repo.Write("tracked.txt", "tracked\n");
        repo.Commit("Add tracked file");
        var copy = store.MoveTo(repository, git.ResolveTree(repository, "HEAD")!);
        var tracked = Path.Combine(copy, "tracked.txt");
        Write(Path.Combine(copy, "untracked.txt"), "untracked");
        Assert.Equal(PrivateCopyState.InOrder, store.Inspect(repository).State);

        var lockPath = Path.Combine(absent.Folder, "index.lock");
        Write(lockPath, "");
        var check = new PrivateCopyDoctorCheck(store);
        var locked = check.Run(Context(repository));
        Assert.Equal(DoctorOutcome.Fails, locked.Outcome);
        Assert.Equal("The private copy's index is locked by a review that stopped.", locked.Found.ToString());
        Assert.Contains(lockPath, locked.Fix!.Text.ToString(), StringComparison.Ordinal);
        Assert.Contains(locked.Fix.Actions, action => action.Kind == DoctorActionKind.CopyText && action.Value == lockPath);
        File.Delete(lockPath);

        File.Delete(tracked);
        Assert.Equal(PrivateCopyState.OutOfOrder, store.Inspect(repository).State);
        Assert.False(File.Exists(tracked));
    }

    [Theory]
    [InlineData(false, FixedProduct.ProgramVersion, DoctorOutcome.Passes)]
    [InlineData(true, "0.2", DoctorOutcome.Passes)]
    [InlineData(true, FixedProduct.ProgramVersion, DoctorOutcome.Fails)]
    public void MeaningUsesTheExistingConfiguredProgramAndItsVersion(bool hookExists, string componentVersion, DoctorOutcome outcome)
    {
        using var folder = new TestFolder();
        var program = Path.Combine(folder.Path, "commitfence");
        var hooked = Path.Combine(folder.Path, "hooked", "commitfence");
        var git = new DoctorGitStub(folder.Path) { ProgramSetting = hooked };
        if(hookExists)
            Write(hooked, "");

        var installation = Path.GetDirectoryName(hookExists ? hooked : program)!;
        Write(Path.Combine(installation, "analyzers", "CF.Meaning.dll"), "");
        var machine = new DoctorMachineStub(program) { Version = "0.2", ComponentVersion = componentVersion };
        var context = new DoctorContext(git.Repository, program, FixedProduct.ProgramVersion, machine, git);
        var language = new CSharpLanguage(new ScriptedProcessRunner(), _files, new FolderOfTest(folder.Path));

        Assert.Equal(outcome, language.DoctorChecks[1].Run(context).Outcome);
    }

    private static DoctorContext Context(RepositoryLocation repository, string version = FixedProduct.ProgramVersion)
    {
        var program = Path.Combine(repository.WorkFolder, "commitfence");
        return new DoctorContext(repository, program, FixedProduct.ProgramVersion, new DoctorMachineStub(program) { Version = version }, new DoctorGitStub(repository.WorkFolder));
    }

    private static ScriptedProcessRunner Dotnet(bool selected, string version)
    {
        return new ScriptedProcessRunner()
            .Route(request => request.Arguments.Contains("--version"), _ => selected
                ? ScriptedProcessRunner.Output(version + "\n")
                : ScriptedProcessRunner.Failure(1, "No compatible SDK is installed"))
            .Route(request => request.Arguments.Contains("--list-sdks"), _ => ScriptedProcessRunner.Output("11.0.100 [sdk]\n"));
    }

    private static DoctorService Service(string folder, IDoctorGit git, IDataFolder data, IProcessRunner gitRunner, IProcessRunner dotnet)
    {
        var languages = new List<ILanguage> { new CSharpLanguage(dotnet, _files, data) };
        return new DoctorService(new FixedClock(DateTimeOffset.UnixEpoch), new FixedProduct(), git, Context(new RepositoryLocation(folder, "")).Machine,
            data, _files, new GitSnapshotStore(gitRunner, data), new SettingsFile(_files, new SettingsReader(languages)), languages);
    }

    private static (ExitCode Code, string Output, string Error) RunCommand(string folder, IDoctorService service, params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var environment = new CommandEnvironment(folder, folder, Path.Combine(folder, "commitfence"), TextReader.Null, output, error, false);
        var runner = new CommandRunner(null!, null!, null!, null!, null!, environment, service);

        return (runner.Run(arguments), output.ToString(), error.ToString());
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
