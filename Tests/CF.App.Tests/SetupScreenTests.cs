using System.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using CF.App.Doctor;
using CF.App.Parts;
using CF.App.Shell;
using CF.App.Tests.Support;
using CF.Core.Code;
using CF.Core.Services;
using CF.Core.Setup;
using Xunit;

namespace CF.App.Tests;

/// <summary>Setup actions through the same view models as the two screens.</summary>
public sealed class SetupScreenTests
{
    [AvaloniaTheory]
    [InlineData(1100)]
    [InlineData(1440)]
    public async Task RulesStartsSettingsAndLeavesTheEditorConfigListAvailable(int width)
    {
        var setup = new FakeSetupService();
        using var shell = new ShellHost(settings: null, fileRules: [RulesHost.ScopedRule()], setup: setup);
        var rules = shell.Model.Rules;
        await Signals.FileRulesArrived(rules);
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = width, Height = 900 });

        Assert.True(rules.NeedsSetup);
        Assert.Equal("Not written yet", rules.SetupState);
        Assert.Equal(!rules.IsOnePane, rules.ShowsNoRuleBlock);
        Assert.Contains(rules.Items, item => item is CF.App.Rules.FileRuleRowViewModel);
        Assert.Null(rules.SelectedItem);
        var start = host.Find<BusyButton>(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == "Start CommitFence here");
        Assert.NotNull(start);
        Assert.True(start.Focus(NavigationMethod.Tab));
        await rules.StartSettingsCommand.ExecuteAsync(null);
        host.Pump();

        Assert.False(rules.NeedsSetup);
        Assert.Null(rules.SetupError);
        Assert.Equal("Written", rules.SetupState);
        Assert.Equal("No rules yet", rules.NoRuleHeading);
        Assert.Equal("Started: commitfence.json is written, with no rules yet.", rules.SetupConfirmation);
        Assert.Equal("Today, 12:00", rules.SetupWrittenAt);
        var step = Assert.IsType<SetupNextStep>(rules.CommitSettings);
        Assert.Equal("Commit and share commitfence.json so the team gets the same rules.", step.Lead + step.Detail);
        var git = $"git -C \"{shell.RepositoryPath.Replace('\\', '/')}\"";
        Assert.Equal($"{git} add commitfence.json", string.Join(' ', step.AddWords));
        Assert.Equal($"{git} commit -m \"Add CommitFence settings\" -- commitfence.json", string.Join(' ', step.CommitWords));
        Assert.Equal($"{git} add commitfence.json && {git} commit -m \"Add CommitFence settings\" -- commitfence.json", step.Command);
        Assert.Single(host.FindAll<TextBlock>(text => text.IsEffectivelyVisible && AutomationProperties.GetName(text) == rules.SetupConfirmation));
        Assert.Single(host.FindAll<Button>(button => button.IsEffectivelyVisible && button.Command == rules.OpenSettingsFileCommand));
        var newRule = host.Find<Button>(button => button.Name == "NewRuleButton");
        Assert.NotNull(newRule);
        Assert.True(host.HasFocusWithin(newRule));
        Assert.Equal(SetupService.StarterSettings, File.ReadAllText(Path.Combine(shell.RepositoryPath, "commitfence.json")));
        shell.Model.CurrentScreen = Screen.Review;
        Assert.Null(rules.SetupConfirmation);
    }

    [AvaloniaFact]
    public async Task TwoLineGitPreviewCopiesACommandThatCommitsOnlyTheSetupFile()
    {
        using var folder = new TestFolder();
        var repository = Path.Combine(folder.Path, "repository with spaces");
        Directory.CreateDirectory(repository);
        var config = Path.Combine(folder.Path, "gitconfig");
        File.WriteAllText(config, "[user]\n    name = Setup Test\n    email = setup@example.invalid\n");
        var environment = new Dictionary<string, string?>
        {
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_GLOBAL"] = config,
            ["GIT_DIR"] = null,
            ["GIT_WORK_TREE"] = null,
            ["GIT_INDEX_FILE"] = null
        };
        async Task<string> Run(string program, string workingFolder, IReadOnlyList<string> arguments, string? input = null)
        {
            var start = new ProcessStartInfo(program)
            {
                WorkingDirectory = workingFolder,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach(var argument in arguments)
                start.ArgumentList.Add(argument);

            foreach(var variable in environment)
                start.Environment[variable.Key] = variable.Value;

            using var process = Process.Start(start)!;
            var cancellation = TestContext.Current.CancellationToken;
            var output = process.StandardOutput.ReadToEndAsync(cancellation);
            var error = process.StandardError.ReadToEndAsync(cancellation);
            if(input != null)
                await process.StandardInput.WriteAsync(input.AsMemory(), cancellation);

            process.StandardInput.Close();
            if(!process.WaitForExit(TimeSpan.FromSeconds(30)))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail($"{program} did not finish.");
            }

            Assert.True(process.ExitCode == 0, await error);
            return (await output).Trim();
        }

        Task<string> Git(params string[] arguments)
        {
            return Run("git", repository, arguments);
        }

        try
        {
            await Git("init");
            File.WriteAllText(Path.Combine(repository, "commitfence.json"), SetupService.StarterSettings);
            File.WriteAllText(Path.Combine(repository, "unrelated.txt"), "Already staged");
            await Git("add", "unrelated.txt");

            var clipboard = new FakeClipboard();
            var step = new SetupNextStep(repository, "commitfence.json", "so the team gets the same rules.",
                "Add CommitFence settings", () => clipboard);
            await step.Copy.CopyCommand.ExecuteAsync(null);
            step.Copy.EndCopied();
            Assert.Contains($"\"{repository.Replace('\\', '/')}\"", step.AddWords);
            Assert.Equal($"{string.Join(' ', step.AddWords)} && {string.Join(' ', step.CommitWords)}", clipboard.Text);

            if(OperatingSystem.IsWindows())
                await Run("cmd.exe", folder.Path, ["/d", "/q"], $"{clipboard.Text}\r\nexit /b %ERRORLEVEL%\r\n");
            else
                await Run("/bin/sh", folder.Path, [], clipboard.Text + "\n");

            Assert.Equal("commitfence.json", await Git("show", "--format=", "--name-only", "HEAD"));
            Assert.Equal("unrelated.txt", await Git("diff", "--cached", "--name-only"));
        }
        finally
        {
            // Git objects are read-only on Windows and would prevent the test folder from being removed.
            if(OperatingSystem.IsWindows())
            {
                foreach(var file in Directory.EnumerateFiles(repository, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
            }
        }
    }

    [AvaloniaFact]
    public async Task NewRuleIsFilledOnlyAfterTheSettingsFileExists()
    {
        using var shell = new ShellHost(settings: null, setup: new FakeSetupService());
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 });
        var newRule = host.Find<Button>(button => button.Name == "NewRuleButton")!;
        Assert.Contains("outlined", newRule.Classes);
        Assert.DoesNotContain("main", newRule.Classes);

        await shell.Model.Rules.StartSettingsCommand.ExecuteAsync(null);
        host.Pump();
        Assert.True(File.Exists(Path.Combine(shell.RepositoryPath, "commitfence.json")));
        Assert.Contains("main", newRule.Classes);
        Assert.DoesNotContain("outlined", newRule.Classes);
    }

    [AvaloniaFact]
    public async Task FailedStartClearsItsErrorWhileRetryingAndLoadsTheFileWrittenByHand()
    {
        const string readOnly = "Could not write commitfence.json: the folder is read-only.";
        const string remedy = "Nothing was written. Make the folder writable and try again, or copy the three lines and create the file yourself.";
        var setup = new FakeSetupService { Start = _ => new SetupReport([readOnly, remedy], true) };
        using var shell = new ShellHost(settings: null, setup: setup);
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 });
        var clipboard = new FakeClipboard();
        var rules = shell.Model.Rules;
        rules.Clipboard = clipboard;
        await rules.StartSettingsCommand.ExecuteAsync(null);
        host.Pump();
        Assert.Equal("Not written", rules.SetupState);
        Assert.Equal(readOnly, rules.SetupError);
        Assert.Equal(remedy, rules.SetupErrorDetail);

        var copy = host.Find<Button>(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == "Copy");
        Assert.NotNull(copy);
        Assert.True(copy.Focus(NavigationMethod.Tab));
        host.Press(PhysicalKey.Enter);
        await rules.CopyStarter.CopyCommand.ExecutionTask!;
        Assert.Equal(SetupService.StarterSettings, clipboard.Text);
        Assert.Equal("Copied", AutomationProperties.GetName(copy));
        rules.CopyStarter.EndCopied();
        Assert.Equal("Copy", AutomationProperties.GetName(copy));

        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Start = _ =>
        {
            entered.SetResult();
            release.Wait();
            return new SetupReport(["Could not write commitfence.json: access denied.", "Check the folder permissions."], true);
        };
        var retrying = rules.StartSettingsCommand.ExecuteAsync(null);
        try
        {
            await entered.Task;
            host.Pump();
            Assert.Null(rules.SetupState);
            Assert.Null(rules.SetupError);
            Assert.Null(rules.SetupErrorDetail);
            Assert.False(copy.IsEffectivelyVisible);
        }
        finally
        {
            release.Set();
            await retrying;
        }

        host.Pump();
        Assert.Equal("Could not write commitfence.json: access denied.", rules.SetupError);
        Assert.Equal("Check the folder permissions.", rules.SetupErrorDetail);
        Assert.True(copy.IsEffectivelyVisible);

        File.WriteAllText(Path.Combine(shell.RepositoryPath, "commitfence.json"), SettingsText.Four);
        setup.Start = _ => throw new InvalidOperationException("The starter must not write over a file.");
        await rules.StartSettingsCommand.ExecuteAsync(null);
        host.Pump();
        Assert.False(rules.NeedsSetup);
        Assert.Null(rules.SetupError);
        Assert.Contains(rules.Items, item => item is CF.App.Rules.TeamRuleRowViewModel);
        var newRule = host.Find<Button>(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == "New rule");
        Assert.NotNull(newRule);
        Assert.True(newRule.IsFocused);
    }

    [AvaloniaTheory]
    [InlineData(SettingsText.Four, true)]
    [InlineData("{", false)]
    public async Task StartReloadsAFileThatAppearsDuringAFailedWrite(string contents, bool isValid)
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var setup = new FakeSetupService
        {
            Start = folder =>
            {
                entered.SetResult();
                release.Wait();
                File.WriteAllText(Path.Combine(folder, "commitfence.json"), contents);
                return new SetupReport(["Could not write commitfence.json: the file already exists."], true);
            }
        };
        using var shell = new ShellHost(settings: null, setup: setup);
        var rules = shell.Model.Rules;
        rules.NewRule();
        var draft = rules.CurrentPage!;
        draft.Title = "No methods";
        draft.ArtifactName = "method";
        draft.IsForbid = true;
        Assert.True(draft.SaveCommand.CanExecute(null));

        var starting = rules.StartSettingsCommand.ExecuteAsync(null);
        try
        {
            await entered.Task;
            Assert.False(draft.SaveCommand.CanExecute(null));
            draft.SaveCommand.Execute(null);
            rules.SaveOpenRule();
            Assert.False(File.Exists(Path.Combine(shell.RepositoryPath, "commitfence.json")));
        }
        finally
        {
            release.Set();
            await starting;
        }

        Assert.True(draft.SaveCommand.CanExecute(null));
        Assert.False(rules.NeedsSetup);
        Assert.Null(rules.SetupError);
        Assert.Null(rules.SetupConfirmation);
        Assert.Equal(isValid, rules.Items.Any(item => item is CF.App.Rules.TeamRuleRowViewModel));
        if(isValid)
            Assert.Null(rules.BannerText);
        else
            Assert.Contains("commitfence.json", rules.BannerText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task StartingKeepsTheButtonEnabledAndFocusedButWillNotStartTwice()
    {
        var setup = new FakeSetupService();
        using var shell = new ShellHost(settings: null, setup: setup);
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 });
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;
        setup.Start = _ =>
        {
            if(Interlocked.Increment(ref starts) == 1)
                entered.SetResult();

            release.Wait();
            return new SetupReport(["Could not write commitfence.json."], true);
        };
        var start = host.Find<BusyButton>(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == "Start CommitFence here");
        Assert.NotNull(start);
        var rules = shell.Model.Rules;
        var newRule = host.Find<Button>(button => button.Name == "NewRuleButton")!;
        Assert.True(start.Focus(NavigationMethod.Tab));
        host.Press(PhysicalKey.Enter);
        var starting = shell.Model.Rules.StartSettingsCommand.ExecutionTask!;
        try
        {
            await entered.Task;
            Assert.True(start.IsEffectivelyEnabled);
            Assert.True(host.HasFocusWithin(start));
            Assert.Equal("Starting, busy", AutomationProperties.GetName(start));
            Assert.Null(rules.SetupState);
            Assert.False(rules.NewRuleFromHeadCommand.CanExecute(null));
            Assert.False(newRule.IsEffectivelyEnabled);
            host.Press(PhysicalKey.Enter);
            host.Press(PhysicalKey.Space);
            await shell.Model.Rules.StartSettingsCommand.ExecuteAsync(null);
        }
        finally
        {
            release.Set();
            await starting;
        }

        Assert.Equal(1, starts);
        Assert.True(rules.NewRuleFromHeadCommand.CanExecute(null));
        Assert.True(host.HasFocusWithin(start));
        Assert.Equal("Try again", AutomationProperties.GetName(start));
    }

    [Fact]
    public void DisplayedHookRowsRejoinToTheOriginalLine()
    {
        var line = new FileLine(2, HookLine.PrePush);
        var rows = HookDisplayRows.For(line).ToList();
        Assert.Equal(6, rows.Count);
        Assert.Equal(2, rows[0].Number);
        Assert.All(rows.Skip(1), row => Assert.Equal(0, row.Number));
        Assert.Equal(HookLine.PrePush, string.Join(' ', rows.Select(row => row.Text.TrimStart())));
    }

    [AvaloniaFact]
    public async Task DoctorCommitStepCopiesTheScriptDoctorActuallyEdited()
    {
        const string script = "hooks/team-pre-push";
        var setup = new FakeSetupService();
        using var shell = new ShellHost(setup: setup);
        var clipboard = new FakeClipboard();
        var doctor = shell.Model.Doctor;
        doctor.Clipboard = clipboard;
        var added = false;
        setup.Add = (_, addition) =>
        {
            Assert.Equal(script, addition.ShownPath);
            added = true;
            return new SetupReport([], false) { WorktreeRoot = shell.RepositoryPath };
        };
        var activeScript = script;
        shell.Doctor.Answer = folder => FakeDoctorService.HookSetupReport(folder, added, script: activeScript);
        await doctor.CheckAsync();
        await doctor.Groups[0].Checks[0].AddCommand.ExecuteAsync(null);
        var step = Assert.IsType<SetupNextStep>(doctor.Groups[0].Checks[0].CommitHook);

        Assert.Equal("Commit and share hooks/team-pre-push so the team's pushes run CommitFence; each teammate needs CommitFence installed on their machine (Doctor checks it).",
            step.Lead + step.Detail);
        await step.Copy.CopyCommand.ExecuteAsync(null);
        step.Copy.EndCopied();
        var git = $"git -C \"{shell.RepositoryPath.Replace('\\', '/')}\"";
        Assert.Equal($"{git} add hooks/team-pre-push && {git} commit -m \"Run CommitFence before each push\" -- hooks/team-pre-push", clipboard.Text);

        await doctor.CheckAsync();
        Assert.Same(step, doctor.Groups[0].Checks[0].CommitHook);

        activeScript = "different-hooks/team-pre-push";
        await doctor.CheckAsync();
        var row = doctor.Groups[0].Checks[0];
        Assert.True(row.ShowsPass);
        Assert.Null(row.CommitHook);
        Assert.DoesNotContain(row.Found, part => part.Text.Contains("CommitFence added it at", StringComparison.Ordinal));

        activeScript = script;
        await doctor.CheckAsync();
        Assert.Null(doctor.Groups[0].Checks[0].CommitHook);
    }

    [AvaloniaFact]
    public async Task DoctorPreviewsTheLineThenChecksAgainAfterAdding()
    {
        var setup = new FakeSetupService();
        using var shell = new ShellHost(setup: setup);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var added = false;
        var writes = 0;
        setup.Add = (_, addition) =>
        {
            if(Interlocked.Increment(ref writes) == 1)
                entered.SetResult();

            release.Wait();
            Assert.Equal(".husky/pre-push", addition.ShownPath);
            Assert.Equal(2, addition.After.Line);
            added = true;
            return new SetupReport([], false);
        };
        shell.Doctor.Answer = folder => FakeDoctorService.HookSetupReport(folder, added);
        var doctor = shell.Model.Doctor;
        var announcements = new List<string>();
        doctor.HookWriteFinished += announcements.Add;
        await doctor.CheckAsync();
        var row = doctor.Groups[0].Checks[0];

        Assert.True(row.CanAdd);
        Assert.Equal("After adding, 3 lines, line 2 added", row.PreviewName);
        var open = row.Actions.Single(action => action.IsOpen).OpenCommand;
        Assert.True(open.CanExecute(null));
        var adding = row.AddCommand.ExecuteAsync(null);
        try
        {
            await entered.Task;
            Assert.False(doctor.AgainCommand.CanExecute(null));
            var copy = row.Actions.Single(action => action.IsCopy).Copy!;
            Assert.False(copy.CopyCommand.CanExecute(null));
            Assert.False(open.CanExecute(null));
            doctor.AgainCommand.Execute(null);
            await doctor.CheckAsync();
            await row.AddCommand.ExecuteAsync(null);
            Assert.Single(shell.Doctor.Folders);
        }
        finally
        {
            release.Set();
            await adding;
        }

        Assert.True(added);
        Assert.Equal(1, writes);
        Assert.Equal(2, shell.Doctor.Folders.Count);
        Assert.True(doctor.IsReady);
        Assert.True(doctor.AgainCommand.CanExecute(null));
        Assert.True(open.CanExecute(null));
        Assert.Contains(doctor.Groups[0].Checks[0].Found, part => part.Text.Contains("CommitFence added it at", StringComparison.Ordinal));
        Assert.Contains(".husky/pre-push", Assert.Single(announcements), StringComparison.Ordinal);
    }
}
