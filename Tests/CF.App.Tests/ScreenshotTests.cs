using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using CF.App.Exemptions;
using CF.App.Parts;
using CF.App.Review;
using CF.App.Rules;
using CF.App.Shell;
using CF.App.Settings;
using CF.App.Tests.Support;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Setup;
using CF.Core.Updates;
using CF.Integration.Tests;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// Not a test of behaviour: the pictures of the screens, for the designer and the people who judge the design. They are saved when
/// the environment variable CF_SCREENSHOTS names a folder; without it nothing is done.
/// </summary>
public sealed class ScreenshotTests
{
    private static async Task SaveSetupScreens(string folder, string theme, IReadOnlyList<FileRule> fileRules, ViewHost host, MainWindow window)
    {
        var setup = new FakeSetupService { WorktreeRoot = OperatingSystem.IsWindows() ? "C:/Projects/invoicing" : "/home/dev/projects/invoicing" };
        var setupFileRules = fileRules.ToList();
        setupFileRules[0] = RulesHost.FileRule("IDE0055", "Fix formatting", FileRuleState.On, Severity.Block, 14);
        using var shell = new ShellHost(settings: null, fileRules: setupFileRules, setup: setup);
        shell.Clock.Now = new DateTimeOffset(new DateTime(2026, 11, 24, 10, 42, 0, DateTimeKind.Local));
        window.DataContext = shell.Model;
        host.Window.FocusManager?.Focus(null);
        await Signals.FileRulesArrived(shell.Model.Rules);
        host.Pump();
        Assert.Single(host.FindAll<Button>(button => button.IsEffectivelyVisible && button.Classes.Contains("main")
            && Avalonia.Automation.AutomationProperties.GetName(button) == "Start CommitFence here"));
        host.Save(folder, $"rules-start-{theme}-1440.png");
        var rules = shell.Model.Rules;
        var failedStart = new SetupReport(["Could not write commitfence.json: the folder is read-only.",
            "Nothing was written. Make the folder writable and try again, or copy the three lines and create the file yourself."], true);
        setup.Start = _ => failedStart;
        await rules.StartSettingsCommand.ExecuteAsync(null);
        host.Save(folder, $"rules-startfail-{theme}-1440.png");

        setup.Start = _ => new SetupReport(["Could not write commitfence.json: a folder named commitfence.json already exists there.",
            "Nothing was written. Move or rename the commitfence.json folder, then try again."], true);
        await rules.StartSettingsCommand.ExecuteAsync(null);
        host.Save(folder, $"rules-startfail-folder-{theme}-1440.png");

        setup.Start = _ => new SetupReport(["Could not write commitfence.json: the folder is not in a Git repository.",
            "Nothing was written. Open a Git repository, then try again."], true);
        await rules.StartSettingsCommand.ExecuteAsync(null);
        host.Save(folder, $"rules-startfail-no-repository-{theme}-1440.png");

        using var startHeld = new ManualResetEventSlim();
        var startEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Start = _ =>
        {
            startEntered.SetResult();
            startHeld.Wait();
            return failedStart;
        };
        var starting = rules.StartSettingsCommand.ExecuteAsync(null);
        try
        {
            await startEntered.Task;
            host.Save(folder, $"rules-starting-{theme}-1440.png");
        }
        finally
        {
            startHeld.Set();
            await starting;
        }

        setup.Start = null;
        await rules.StartSettingsCommand.ExecuteAsync(null);
        host.Pump();
        host.Window.FocusManager?.Focus(null);
        host.Save(folder, $"rules-started-{theme}-1440.png");
        var added = false;
        var managed = false;
        var checkedAt = new DateTimeOffset(new DateTime(2026, 11, 24, 17, 12, 0, DateTimeKind.Local));
        shell.Clock.Now = checkedAt;
        shell.Doctor.Answer = path => FakeDoctorService.HookSetupReport(path, added, managed, hasLefthookConfig: true) with
        {
            CheckedAt = added ? checkedAt.AddMinutes(2) : checkedAt,
            Duration = TimeSpan.FromSeconds(added ? 1.2 : 1.3)
        };
        shell.Model.CurrentScreen = Screen.Doctor;
        await Signals.Until(shell.Model.Doctor, () => !shell.Model.Doctor.IsRunning);
        host.Pump();
        host.Save(folder, $"doctor-add-{theme}-1440.png");

        var checkAgain = host.Find<Button>(button => button.Name == "CheckAgainButton" && button.IsEffectivelyVisible)!;
        Assert.True(checkAgain.Focus(NavigationMethod.Tab));
        host.Press(PhysicalKey.Tab);
        var add = host.Find<Button>(button => button.IsEffectivelyVisible
            && Avalonia.Automation.AutomationProperties.GetName(button) == "Add the line to .husky/pre-push, as line 2")!;
        Assert.True(host.HasFocusWithin(add));
        host.Save(folder, $"doctor-addfocus-{theme}-1440.png");

        var row = shell.Model.Doctor.Groups[0].Checks[0];
        using var addHeld = new ManualResetEventSlim();
        var addEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Add = (_, _) =>
        {
            addEntered.SetResult();
            addHeld.Wait();
            return new SetupReport(["Could not write .husky/pre-push: the file is read-only.",
                "Nothing was changed. Make it writable and try again, or copy the line and add it by hand as line 2."], true);
        };
        host.Window.FocusManager?.Focus(null);
        var openScript = host.Find<Button>(button => button.IsEffectivelyVisible
            && Avalonia.Automation.AutomationProperties.GetName(button) == "Open the hook script")!;
        var adding = row.AddCommand.ExecuteAsync(null);
        try
        {
            await addEntered.Task;
            host.Pump();
            Assert.False(checkAgain.IsEffectivelyEnabled);
            Assert.False(host.Find<Button>(button => button.IsEffectivelyVisible
                && Avalonia.Automation.AutomationProperties.GetName(button) == "Copy the hook line")!.IsEffectivelyEnabled);
            Assert.False(openScript.IsEffectivelyEnabled);
            var status = host.Find<ItemsControl>(control => control.Name == "GroupList" && control.IsEffectivelyVisible)!;
            Assert.Equal("Adding", Avalonia.Automation.AutomationProperties.GetName(status));
            Assert.Equal(Avalonia.Automation.AutomationLiveSetting.Polite, Avalonia.Automation.AutomationProperties.GetLiveSetting(status));
            host.Save(folder, $"doctor-adding-{theme}-1440.png");
        }
        finally
        {
            addHeld.Set();
            await adding;
        }

        host.Pump();
        Assert.True(openScript.IsEffectivelyEnabled);
        host.Save(folder, $"doctor-failed-{theme}-1440.png");
        setup.Add = (_, _) =>
        {
            added = true;
            return new SetupReport([], false) { WorktreeRoot = setup.WorktreeRoot };
        };
        shell.Clock.Now = checkedAt.AddMinutes(2);
        await row.AddCommand.ExecuteAsync(null);
        host.Pump();
        host.Save(folder, $"doctor-added-{theme}-1440.png");

        added = false;
        managed = true;
        shell.Clock.Now = checkedAt;
        await shell.Model.Doctor.CheckAsync();
        host.Window.FocusManager?.Focus(null);
        host.Pump();
        var entry = shell.Model.Doctor.Groups[0].Checks[0].EntryLines;
        Assert.NotEmpty(entry);
        Assert.All(entry, line => Assert.False(line.HasNumber));
        Assert.Equal(5, entry.Count(line => line.ShowsPlus));
        host.Save(folder, $"doctor-other-{theme}-1440.png");
    }

    private const string _variable = "CF_SCREENSHOTS";

    // the app has one theme, dark
    private static readonly string[] _themes = ["dark"];

    [AvaloniaFact]
    public async Task RulesDesignStatesAreSavedWhenAFolderIsNamed()
    {
        var folder = Environment.GetEnvironmentVariable(_variable);
        if(string.IsNullOrWhiteSpace(folder))
            Assert.Skip(_variable + " names no folder.");

        await RuleFieldsPictures.SaveRound67RulesAsync(folder);
    }

    [AvaloniaFact]
    public async Task EachScreenIsSavedWhenAFolderIsNamed()
    {
        var folder = Environment.GetEnvironmentVariable(_variable);
        if(string.IsNullOrWhiteSpace(folder))
            Assert.Skip(_variable + " names no folder.");

        foreach(var theme in _themes)
        {
            var fileRules = new[]
            {
                RulesHost.ScopedRule(),
                RulesHost.FileRule("CA1000", "Do not declare static members on generic types", FileRuleState.On, Severity.Block, 4),
                RulesHost.FileRule("CA1822", "Mark members as static", FileRuleState.On, Severity.Warn, 8, "warning"),
                RulesHost.FileRule("IDE0005", "Remove unnecessary usings", FileRuleState.Off, null, 12, "silent")
            };
            using var reviewShell = new ShellHost(fileRules: fileRules);
            var shell = reviewShell;
            shell.Review.Result = _ => RefusedPush();
            var arrived = Signals.FileRulesArrived(shell.Model.Rules);
            var window = new MainWindow
            {
                DataContext = shell.Model,
                Width = 1440,
                Height = 900,
                RequestedThemeVariant = ThemeVariant.Dark
            };
            using var host = new ViewHost(window);
            await arrived;

            // Rules, with a rule open beside the list
            Assert.NotNull(shell.Model.Rules.SelectedRule);
            host.Save(folder, $"rules-{theme}-1440x900.png");

            // Try it on a rule that has paths: the note under the box
            using var pathsShell = new ShellHost(SettingsText.Four.Replace("\"id\": \"TEAM001\",", "\"id\": \"TEAM001\",\n            \"paths\": [\"src/**/*.cs\", \"lib/**/*.cs\"],", StringComparison.Ordinal));
            window.DataContext = pathsShell.Model;
            pathsShell.Model.Rules.SelectedItem = pathsShell.Model.Rules.Items.OfType<TeamRuleRowViewModel>().Single(row => row.Id == "TEAM001");
            var pathsTryIt = pathsShell.Model.Rules.CurrentPage!.TryIt;
            pathsTryIt.LoadExampleCommand.Execute(null);
            await Signals.Idle(pathsTryIt);
            host.Pump();
            host.Save(folder, $"rules-try-paths-{theme}-1440x900.png");

            using var fileNameShell = new ShellHost("""{"rules":[{"id":"NAME","language":"csharp","for":"class","severity":"block","title":"Match the file name","require":{"isNamedLikeFile":true}}]}""");
            window.DataContext = fileNameShell.Model;
            var fileNameTryIt = fileNameShell.Model.Rules.CurrentPage!.TryIt;
            await Signals.Idle(fileNameTryIt);
            Assert.True(fileNameTryIt.ReadsFileName);
            Assert.Equal("commitfence rules try NAME <file>", fileNameTryIt.Command);
            host.Save(folder, $"rules-try-file-name-{theme}-1440x900.png");
            window.Width = 760;
            host.Pump();
            fileNameShell.Model.Rules.OpenSelectedRule();
            host.Save(folder, $"rules-try-file-name-{theme}-760x900.png");
            window.Width = 1440;
            window.DataContext = shell.Model;
            host.Pump();

            shell.Model.Rules.SelectedItem = shell.Model.Rules.Items.OfType<FileRuleRowViewModel>().Single(row => row.Id == "IDE0055");
            host.Pump();
            host.Save(folder, $"rules-file-page-{theme}-1440.png");
            window.Width = 760;
            host.Pump();
            host.Save(folder, $"rules-places-{theme}-760.png");
            shell.Model.Rules.OpenSelectedRule();
            host.Save(folder, $"rule-page-{theme}-760.png");
            window.Width = 1440;
            host.Pump();

            var general = new FileRulePlace("*.cs", "warning", FileRuleState.On, Severity.Warn, 8, [], _ => true);
            var production = new FileRulePlace("src/Production/**", "error", FileRuleState.On, Severity.Block, 18, [],
                path => path.StartsWith("src/Production/", StringComparison.Ordinal));
            var mixed = new FileRule("CA1822", "Mark members as static", ".editorconfig", "C#", general, [production]);
            var mixedRules = fileRules.Select(rule => rule.Id == mixed.Id ? mixed : rule).ToList();
            using(var mixedShell = new ShellHost(fileRules: mixedRules))
            {
                var rules = mixedShell.Model.Rules;
                var mixedArrived = Signals.FileRulesArrived(rules);
                window.DataContext = mixedShell.Model;
                await mixedArrived;
                rules.SelectedItem = rules.Items.OfType<TeamRuleRowViewModel>().Single(row => row.Id == "TEAM001");
                rules.OpenSelectedRule();
                host.Pump();
                host.Save(folder, $"rules-mixed-{theme}-1440.png");
                rules.SelectedFilter = rules.Filters.Single(filter => filter.Filter == RuleFilter.Blocks);
                host.Pump();
                host.Save(folder, $"rules-mixedblocks-{theme}-1440.png");
                rules.SelectedFilter = rules.Filters.Single(filter => filter.Filter == RuleFilter.All);
                var scoped = new FileRule(mixed.Id, mixed.Title, mixed.File, mixed.Language, null, [production with { Pattern = "src/**.cs" }]);
                rules.Items.OfType<FileRuleRowViewModel>().Single(row => row.Id == scoped.Id).Update(scoped);
                host.Save(folder, $"rules-scoped-only-{theme}-1440.png");
                window.DataContext = shell.Model;
                host.Pump();
            }

            // Review, after a push the review refused, its files on disk as they were reviewed
            WriteFiles(shell.RepositoryPath, RefusedSources());
            shell.Model.ShowReview();
            await shell.Model.Review.RunCommand.ExecuteAsync(null);
            Assert.Equal(VerdictKind.Refused, shell.Model.Review.Verdict.Kind);
            host.Save(folder, $"review-{theme}-1440x900.png");

            // the same, right after a click on the two buttons that copy a prompt: both read "Copied", and at 760 wide
            var openFinding = shell.Model.Review.FileGroups.SelectMany(group => group.Findings).First(finding => finding.IsOpen);
            await openFinding.Copy.CopyCommand.ExecuteAsync(null);
            await shell.Model.Review.CopyAll.CopyCommand.ExecuteAsync(null);
            Assert.True(openFinding.Copy.IsCopied && shell.Model.Review.CopyAll.IsCopied);
            host.Pump();
            host.Save(folder, $"review-copied-{theme}-1440x900.png");
            window.Width = 760;
            host.Pump();
            host.Save(folder, $"review-copied-{theme}-760x900.png");
            window.Width = 1440;
            host.Pump();

            shell.Review.Result = _ => ScopedPush();
            await shell.Model.Review.RunCommand.ExecuteAsync(null);
            foreach(var group in shell.Model.Review.FileGroups)
            {
                group.IsOpen = true;
                foreach(var finding in group.Findings)
                    finding.IsOpen = true;
            }

            window.Height = 1180;
            host.Save(folder, $"review-places-{theme}-1440.png");
            window.Width = 760;
            window.Height = 1500;
            host.Save(folder, $"review-places-{theme}-760.png");
            window.Width = 1440;
            window.Height = 900;
            await SaveFixStates(folder, theme, host);
            await RuleFieldsPictures.SaveAsync(folder, theme);
            await ReviewFilesPictures.SaveAsync(folder, theme);
            using var cleanShell = new ShellHost(fileRules: fileRules);
            shell = cleanShell;
            window.DataContext = shell.Model;

            // an empty screen in the frame of the five screens: the head with the screen's name and an empty sheet, then with a panel
            // that the test gives it, at 1440 and at 760
            shell.Model.CurrentScreen = Screen.Dashboard;
            host.Pump();
            host.Save(folder, $"frame-empty-{theme}-1440.png");
            var frame = host.Find<ScreenFrame>(candidate => candidate.IsEffectivelyVisible)!;
            frame.Main = new TextBlock { Text = "The main column", Height = 400 };
            frame.Panel = new TextBlock { Text = "The panel", Height = 240 };
            host.Pump();
            host.Save(folder, $"frame-panel-{theme}-1440.png");
            window.Width = 760;
            window.Height = 1000;
            host.Pump();
            host.Save(folder, $"frame-panel-{theme}-760.png");
            frame.Panel = null;
            frame.Main = null;
            host.Pump();
            host.Save(folder, $"frame-empty-{theme}-760.png");
            window.Width = 1440;
            window.Height = 900;

            var update = FakeUpdater.Available(Samples.WaitingVersion, "The app\n\n### The app\n\n- **Rules** keep their order.\n- Settings shows what the strictness does.\n\n### Fixes\n\n- A review of a folder with no commit says so.\n\n### Downloads\n\n| a | b |");
            // the notice of a newer version, over the screen
            shell.Model.ShowRules();
            shell.Updater.Result = update;
            await shell.Model.Updates.CheckAsync(true);
            Assert.True(shell.Model.Updates.CanUpdate);
            host.Save(folder, $"update-{theme}-1440x900.png");

            // the notice as the last row of the page under a screen: a version is available, an update downloads at 64 percent, it
            // failed; at 1440, at 760 by 1000, and at the window's smallest, 760 by 560, where every button must be in view
            shell.Model.CurrentScreen = Screen.Dashboard;
            host.Pump();
            host.Save(folder, $"notice-available-{theme}-1440.png");
            Assert.True(host.Find<Button>(button => button.Name == "UpdateNowButton" && button.IsEffectivelyVisible)!.Focus(NavigationMethod.Tab));
            host.Save(folder, $"notice-focus-{theme}-1440.png");
            host.Window.FocusManager?.Focus(null);
            window.Width = 760;
            window.Height = 1000;
            host.Pump();
            host.Save(folder, $"notice-available-{theme}-760x1000.png");
            window.Width = 1440;
            window.Height = 900;
            shell.Updater.Delivery = ProgressDelivery.Before;
            shell.Updater.HoldInstall = new TaskCompletionSource();
            var install = shell.Model.Updates.InstallAsync();
            await shell.Updater.InstallStarted.Task;
            shell.Updater.LastProgress!.Report(new UpdateProgress { Stage = UpdateStage.Downloading, Percent = 64 });
            host.Pump();
            Assert.Equal("64%", shell.Model.Updates.PercentText);
            host.Save(folder, $"notice-downloading-{theme}-1440.png");
            shell.Updater.InstallFailure = new UpdateException("The download does not match the signed list of checksums, so nothing was changed.");
            shell.Updater.HoldInstall.SetResult();
            await install;
            host.Pump();
            host.Save(folder, $"notice-failed-{theme}-1440.png");
            Assert.True(host.Find<Button>(button => button.Name == "TryAgainButton" && button.IsEffectivelyVisible)!.Focus(NavigationMethod.Tab));
            host.Save(folder, $"notice-failed-focus-{theme}-1440.png");
            host.Window.FocusManager?.Focus(null);
            window.Width = 760;
            window.Height = 560;
            host.Pump();
            host.Save(folder, $"notice-failed-{theme}-760x560.png");
            window.Width = 1440;
            window.Height = 900;

            // Doctor: one check fails (1440, and the whole page at 760), the .NET SDK that global.json asks for is missing, every check
            // passes, and the checks run (the notice is sent away first, so that it is not in these pictures)
            shell.Model.Updates.DismissCommand.Execute(null);
            shell.Doctor.Answer = _ => FakeDoctorService.Report(hookFails: true);
            shell.Model.CurrentScreen = Screen.Doctor;
            await shell.Model.Doctor.CheckAsync();
            host.Pump();
            host.Save(folder, $"doctor-fail-{theme}-1440.png");
            var checkAgain = host.Find<Button>(button => button.Name == "CheckAgainButton" && button.IsEffectivelyVisible)!;
            Assert.True(checkAgain.Focus(NavigationMethod.Tab));
            host.Press(PhysicalKey.Tab);
            var openHook = host.Find<Button>(button => button.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetName(button) == "Open the hook script")!;
            Assert.True(host.HasFocusWithin(openHook));
            host.Save(folder, $"doctor-focus-{theme}-1440.png");
            host.Window.FocusManager?.Focus(null);
            window.Width = 760;
            window.Height = 1700;
            host.Pump();
            host.Save(folder, $"doctor-fail-{theme}-760.png");
            window.Width = 1440;
            window.Height = 900;
            shell.Doctor.Answer = _ => FakeDoctorService.SdkReport();
            await shell.Model.Doctor.CheckAsync();
            host.Window.FocusManager?.Focus(null);
            host.Pump();
            host.Save(folder, $"doctor-sdk-{theme}-1440.png");
            shell.Doctor.Answer = _ => FakeDoctorService.Report();
            await shell.Model.Doctor.CheckAsync();
            host.Window.FocusManager?.Focus(null);
            host.Pump();
            host.Save(folder, $"doctor-ready-{theme}-1440.png");
            shell.Doctor.Answer = _ => FakeDoctorService.Report(hookFails: true);
            await shell.Model.Doctor.CheckAsync();
            using var held = new ManualResetEventSlim();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            shell.Doctor.StartedCount = 2;
            shell.Doctor.Answer = _ =>
            {
                entered.SetResult();
                held.Wait();
                return FakeDoctorService.Report();
            };
            var checking = shell.Model.Doctor.CheckAsync();
            try
            {
                await entered.Task;
                host.Save(folder, $"doctor-checking-{theme}-1440.png");
            }
            finally
            {
                held.Set();
                await checking;
            }

            await SaveSetupScreens(folder, theme, fileRules, host, window);
            window.DataContext = shell.Model;
            host.Pump();

            // Settings: up to date (1440 and 760), the notes of the version installed shown, a custom mapping in the file, then an update
            // that is available, downloading and failed (1440, and the whole page at 760)
            shell.Updater.Delivery = ProgressDelivery.Before;
            shell.Updater.InstallFailure = null;
            shell.Updater.HoldInstall = null;
            shell.Updater.Result = FakeUpdater.UpToDate();
            shell.Model.CurrentScreen = Screen.Settings;
            await shell.Model.Updates.CheckAsync(true);
            await shell.Model.Settings.RefreshAsync();
            host.Window.FocusManager?.Focus(null);
            host.Pump();
            host.Save(folder, $"settings-{theme}-1440.png");
            var strictness = host.Find<SegmentedList>(list => list.Name == "StrictnessChoice" && list.IsEffectivelyVisible)!;
            Assert.True(strictness.ContainerFromIndex(strictness.SelectedIndex)!.Focus(NavigationMethod.Tab));
            host.Pump();
            host.Save(folder, $"settings-focus-{theme}-1440.png");
            host.Window.FocusManager?.Focus(null);
            window.Width = 760;
            window.Height = 1300;
            host.Pump();
            host.Save(folder, $"settings-{theme}-760.png");
            window.Width = 1440;
            window.Height = 900;
            shell.Model.Settings.ShowsNotes = true;
            window.Height = 1300;
            host.Pump();
            host.Save(folder, $"settings-notes-{theme}-1440.png");
            shell.Model.Settings.ShowsNotes = false;
            window.Height = 900;
            await SaveStrictnessScreens(folder, theme, shell, host);
            shell.SettingsService.Custom = new Strictness(StrictnessPreset.Custom, null, Severity.Block, Severity.Warn);
            await shell.Model.Settings.RefreshAsync();
            host.Pump();
            host.Save(folder, $"settings-custom-{theme}-1440.png");
            shell.SettingsService.Custom = null;
            await shell.Model.Settings.RefreshAsync();
            shell.Updater.Result = update;
            await shell.Model.Updates.CheckAsync(true);
            host.Pump();
            host.Save(folder, $"settings-available-{theme}-1440.png");
            window.Width = 760;
            window.Height = 1500;
            host.Pump();
            host.Save(folder, $"settings-available-{theme}-760.png");
            window.Width = 1440;
            window.Height = 900;
            shell.Updater.HoldInstall = new TaskCompletionSource();
            var installs = shell.Updater.Installs;
            var settingsInstall = shell.Model.Updates.InstallAsync();
            while(shell.Updater.Installs == installs)
            {
                await Task.Delay(5);
            }

            await Task.Delay(20);
            shell.Updater.LastProgress!.Report(new UpdateProgress { Stage = UpdateStage.Downloading, Percent = 64 });
            host.Pump();
            host.Save(folder, $"settings-downloading-{theme}-1440.png");
            window.Width = 760;
            window.Height = 1500;
            host.Pump();
            host.Save(folder, $"settings-downloading-{theme}-760.png");
            window.Width = 1440;
            window.Height = 900;
            shell.Updater.InstallFailure = new UpdateException("The download does not match the signed list of checksums, so nothing was changed.");
            shell.Updater.HoldInstall.SetResult();
            await settingsInstall;
            host.Pump();
            host.Save(folder, $"settings-failed-{theme}-1440.png");
            Assert.True(host.Find<Button>(button => button.Name == "TryUpdateAgainButton" && button.IsEffectivelyVisible)!.Focus(NavigationMethod.Tab));
            host.Pump();
            host.Save(folder, $"settings-failed-focus-{theme}-1440.png");
            host.Window.FocusManager?.Focus(null);
            window.Width = 760;
            window.Height = 1500;
            host.Pump();
            host.Save(folder, $"settings-failed-{theme}-760.png");
            window.Width = 1440;
            window.Height = 900;
            await ArchitecturePictures.SaveAsync(folder, theme);

            await SaveExemptions(folder, theme);
            await DashboardPictures.SaveAsync(folder, theme);
        }
    }

    private static async Task SaveStrictnessScreens(string folder, string theme, ShellHost shell, ViewHost host)
    {
        var settings = shell.Model.Settings;
        host.Save(folder, $"settings-preset-{theme}-1440.png");
        settings.SelectedChoice = settings.Choices[3];
        await settings.ChoiceSettledAsync();
        host.Save(folder, $"settings-own-{theme}-1440.png");

        shell.SettingsService.SaveFailure = new RuleChangeException("Could not write commitfence.json: the file is read-only.");
        settings.EffectEditors[1].Choose(Severity.Block);
        await settings.ChoiceSettledAsync();
        host.Save(folder, $"settings-refused-{theme}-1440.png");

        shell.SettingsService.SaveFailure = null;
        settings.RetryStrictnessCommand.Execute(null);
        await settings.ChoiceSettledAsync();
        foreach(var width in new List<int> { 1440, 900 })
        {
            host.Window.Width = width;
            host.Pump();
            var warning = host.Find<EffectPickerButton>(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == "warning effect")!;
            Assert.True(warning.Focus(NavigationMethod.Tab));
            host.Press(PhysicalKey.Enter);
            host.Save(folder, $"settings-editing-{theme}-{width}.png");
            host.Press(PhysicalKey.Escape);
        }

        host.Window.Width = 1440;
        host.Window.FocusManager?.Focus(null);
        settings.SelectedChoice = settings.Choices[1];
        await settings.ChoiceSettledAsync();
        host.Save(folder, $"settings-replaced-{theme}-1440.png");
        await settings.ShownAsync();
    }

    private static async Task SaveExemptions(string folder, string theme)
    {
        using var shell = new ShellHost(SettingsText.Exempted, [
            RulesHost.FileRule("CA1822", "Mark members as static", FileRuleState.On, Severity.Warn, 8, "warning"),
            RulesHost.FileRule("IDE0055", "Fix formatting", FileRuleState.On, Severity.Block, 14),
            RulesHost.FileRule("CA1051", "Do not declare visible instance fields", FileRuleState.On, Severity.Warn, 20, "warning"),
            RulesHost.FileRule("IDE0051", "Remove unused private members", FileRuleState.On, Severity.Warn, 26, "warning")
        ]);
        var window = new MainWindow
        {
            DataContext = shell.Model,
            Width = 1440,
            Height = 900,
            RequestedThemeVariant = ThemeVariant.Dark
        };
        using var host = new ViewHost(window);
        var model = shell.Model.Exemptions;
        shell.Model.CurrentScreen = Screen.Exemptions;
        await model.ScanAsync();
        host.Window.FocusManager?.Focus(null);
        host.Pump();
        host.Save(folder, $"exemptions-{theme}-1440.png");
        var merged = model.Items.OfType<ExemptionFileViewModel>().First().Rows.Single(row => row.HasMultipleLines);
        merged.IsOpen = true;
        host.Save(folder, $"exemptions-merged-{theme}-1440.png");
        merged.IsOpen = false;

        window.Width = 760;
        window.Height = 1300;
        host.Pump();
        host.Save(folder, $"exemptions-{theme}-760.png");
        model.TogglePanelCommand.Execute(null);
        host.Save(folder, $"exemptions-by-rule-{theme}-760.png");
        model.TogglePanelCommand.Execute(null);
        window.Width = 1440;
        window.Height = 900;

        // a rule chosen, then "Narrow the rule" on Rules
        model.RuleRows.Single(row => row.RuleId == "TEAM001").ChooseCommand.Execute(null);
        host.Pump();
        host.Save(folder, $"exemptions-rule-{theme}-1440.png");
        model.Chosen!.ActCommand.Execute(null);
        host.Pump();
        host.Save(folder, $"exemptions-narrow-landing-{theme}-1440.png");
        shell.Model.Rules.CurrentPage!.BackToOriginCommand.Execute(null);
        await model.ScanAsync();

        // only the exemptions with no reason, then a commit with none and a scan that failed
        model.ShowWithoutReasonCommand.Execute(null);
        host.Pump();
        host.Save(folder, $"exemptions-trouble-{theme}-1440.png");
        shell.Exemptions.Answer = ExemptionSamples.None;
        await model.ScanAsync();
        host.Pump();
        host.Save(folder, $"exemptions-empty-{theme}-1440.png");
    }

    private static async Task SaveFixStates(string folder, string theme, ViewHost host)
    {
        using var scratch = new TestFolder();
        using var gate = new ManualResetEventSlim();
        var fixture = new FixReviewFixture(scratch.Path) { Gate = gate };
        using var shell = new ShellHost(fileRules: FixReviewFixture.FileRules, fixes: fixture, parentFolder: scratch.Path);
        File.WriteAllBytes(fixture.PathToSource, FixReviewFixture.Before);
        shell.Review.Result = _ => fixture.Result();
        var window = host.Window;
        window.DataContext = shell.Model;
        window.Height = 1120;
        shell.Model.ShowReview();
        await shell.Model.Review.RunCommand.ExecuteAsync(null);
        var findings = Assert.Single(shell.Model.Review.FileGroups).Findings;
        var first = findings[0];
        try
        {
            Assert.True(first.IsPreparing);
            host.Save(folder, $"review-fix-preparing-{theme}-1440.png");
        }
        finally
        {
            gate.Set();
        }

        await first.Preparation!;
        Assert.True(first.HasFix);
        host.Save(folder, $"review-fix-ready-{theme}-1440.png");
        window.Width = 760;
        host.Save(folder, $"review-fix-ready-{theme}-760.png");
        window.Width = 1440;
        host.Pump();

        var difference = host.Find<DiffCode>(code => code.IsEffectivelyVisible);
        Assert.NotNull(difference);
        Assert.True(difference.Focus(NavigationMethod.Tab));
        host.Save(folder, $"review-fix-focus-{theme}-1440.png");

        first.IsOpen = false;
        findings[3].IsOpen = true;
        Assert.True(findings[3].ShowsNoFix);
        host.Save(folder, $"review-fix-unavailable-{theme}-1440.png");

        findings[3].IsOpen = false;
        first.IsOpen = true;
        await first.ApplyFixCommand.ExecuteAsync(null);
        Assert.True(first.IsFixed && findings[1].IsFixed);
        host.Save(folder, $"review-fix-fixed-{theme}-1440.png");

        findings[2].IsOpen = true;
        Assert.True(findings[2].FileChanged);
        host.Save(folder, $"review-fix-changed-{theme}-1440.png");

        File.WriteAllBytes(fixture.PathToSource, FixReviewFixture.Before);
        await shell.Model.Review.RunCommand.ExecuteAsync(null);
        var failed = Assert.Single(shell.Model.Review.FileGroups).Findings[0];
        await failed.Preparation!;
        File.AppendAllText(fixture.PathToSource, "// Changed outside the review.");
        await failed.ApplyFixCommand.ExecuteAsync(null);
        Assert.True(failed.HasFixNotice);
        Assert.True(failed.IsExcerptStale);
        Assert.True(failed.HasCode);
        Assert.Equal("Nothing was written to your file: it changed after the review.", failed.FixNoticeText);
        Assert.False(failed.OffersFix);
        host.Save(folder, $"review-fix-failed-{theme}-1440.png");
        window.Height = 900;
    }

    private static ReviewResult ScopedPush()
    {
        const string source = "src/Billing/InvoiceTotals.cs";
        const string test = "tests/Billing/InvoiceTests.cs";
        var findings = new[]
        {
            Samples.Finding(source, 4, "IDE0055", Severity.Block, "Fix formatting", FindingSource.EditorConfig),
            Samples.Finding(test, 4, "IDE0055", Severity.Warn, "Fix formatting", FindingSource.EditorConfig)
        };
        var files = new Dictionary<string, string>
        {
            [source] = "class InvoiceTotals\n{\n    public int Sum(int x, int y)\n    { return x+y; }\n}\n",
            [test] = "class InvoiceTests\n{\n    void Check()\n    { var sum=new InvoiceTotals().Sum(1, 2); }\n}\n"
        };
        return Samples.Result(ReviewOutcome.Blocked, findings, [Samples.Reviewed(source), Samples.Reviewed(test)], files);
    }

    private static ReviewResult RefusedPush()
    {
        var findings = new[]
        {
            Samples.Finding("src/Billing/InvoiceTotals.cs", 9, "TEAM001", Severity.Block, "No braces around a single-line if"),
            Samples.Finding("src/Billing/InvoiceTotals.cs", 5, "TEAM002", Severity.Warn, "Methods are documented"),
            Samples.Finding("src/Billing/Ledger.cs", 5, "TEAM002", Severity.Warn, "Methods are documented"),
            Samples.Finding("src/Orders/OrderLine.cs", 3, "TEAM004", Severity.Warn, "No partial classes")
        };
        var coverage = new[] { Samples.Reviewed("src/Billing/InvoiceTotals.cs"), Samples.Reviewed("src/Billing/Ledger.cs"), Samples.Reviewed("src/Orders/OrderLine.cs"), Samples.Reviewed("src/Orders/Order.cs") };
        return Samples.Result(ReviewOutcome.Blocked, findings, coverage, RefusedSources());
    }

    private static Dictionary<string, string> RefusedSources()
    {
        var totals = "namespace Billing;\n\npublic sealed class InvoiceTotals\n{\n    public int Total(int[] lines)\n    {\n        var sum = 0;\n        foreach(var line in lines)\n            if(line > 0) { sum += line; }\n\n        return sum;\n    }\n}\n";
        return new Dictionary<string, string>
        {
            ["src/Billing/InvoiceTotals.cs"] = totals,
            ["src/Billing/Ledger.cs"] = "namespace Billing;\n\npublic sealed class Ledger\n{\n    public void Post(int amount)\n    {\n    }\n}\n",
            ["src/Orders/OrderLine.cs"] = "namespace Orders;\n\npublic partial class OrderLine\n{\n    public int Quantity { get; set; }\n}\n"
        };
    }

    private static void WriteFiles(string folder, Dictionary<string, string> files)
    {
        foreach(var (name, text) in files)
        {
            var path = Path.Combine(folder, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
    }
}
