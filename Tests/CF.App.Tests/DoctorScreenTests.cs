using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CF.App.Doctor;
using CF.App.Parts;
using CF.App.Shell;
using CF.App.Tests.Support;
using CF.Core.Review;
using CF.Core.Services;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// The Doctor screen: what it shows of the report the diagnosis gives (the verdict, the checks, the fix of a failing one, "This
/// machine"), what it does while the checks run, and the button of a fix that copies. A fake diagnosis answers.
/// </summary>
public sealed class DoctorScreenTests
{
    private readonly FakeDoctorService _doctor = new FakeDoctorService();
    private readonly FixedClock _clock = new FixedClock(Samples.Now);

    [AvaloniaFact]
    public async Task FailingCheckIsOpenWithItsFixItsLinesAndItsButtons()
    {
        _doctor.Answer = _ => FakeDoctorService.Report(hookFails: true);
        var model = Create(Samples.Folder("/work/invoicing"));
        using var host = new ViewHost(new DoctorView { DataContext = model }, 1440, 900);
        await model.ShownAsync();
        host.Pump();

        // the verdict, in the body's words, and what the screen's head says
        Assert.Equal("1 check fails", model.Heading);
        Assert.Equal("Until it is fixed, a push that sends a commit ends as Review incomplete and is refused.", string.Concat(model.Sentence.Select(part => part.Text)));
        Assert.Equal("Whether this machine reviews the pushes of invoicing", model.Meta);
        Assert.Equal("3 checks", model.ChecksText);
        Assert.Equal($"Checked today, {Samples.Now.ToLocalTime():HH:mm}, in 1.3 s", model.CheckedText);
        Assert.Equal(["Git and the hook", "C#"], model.Groups.Select(group => group.Name));

        // only the failing check is open: its fix, the lines the body gives, the last in force, and its two buttons; the others are one line
        var rows = model.Groups.SelectMany(group => group.Checks).ToList();
        var open = Assert.Single(rows, row => row.HasFix);
        Assert.Equal("The pre-push hook runs CommitFence first", open.Title);
        Assert.Equal([1, 2, 3, 4], open.Lines.Select(line => line.Number));
        Assert.Equal([false, false, false, true], open.Lines.Select(line => line.IsCurrent));
        Assert.Equal(["Open the hook script", "Copy the hook line"], open.Actions.Select(action => action.Label));
        Assert.Single(host.FindAll<FileLines>(lines => lines.IsEffectivelyVisible));

        // Tab reaches "Check again" and the two buttons of the fix, and nothing else
        var stops = host.FindAll<Button>(button => button.IsEffectivelyVisible && button.Focusable && button.IsTabStop).Select(Avalonia.Automation.AutomationProperties.GetName);
        Assert.Equal(["Check again", "Open the hook script", "Copy the hook line"], stops);

        // "This machine": the facts of the report, and what is not known says so
        Assert.Equal(["Program", "Version", "Git", ".NET SDK", "Data"], model.Machine.Select(row => row.Name));
        Assert.Equal("2.47.1.windows.1", model.Machine[2].Value);
        Assert.NotNull(host.Find<TextBlock>(text => text.Text == "This machine" && text.IsEffectivelyVisible));
        _doctor.Answer = _ => FakeDoctorService.Report(hookFails: true) with { Machine = FakeDoctorService.Report().Machine with { GitVersion = null } };
        await model.CheckAsync();
        Assert.True(model.Machine[2].IsUnknown);
        Assert.Equal("Git: unknown", model.Machine[2].SpokenName);
    }

    [AvaloniaFact]
    public async Task EveryCheckPassingIsReadyForPushesWithNoButtonButCheckAgain()
    {
        var model = Create(Samples.Folder("/work/invoicing"));
        using var host = new ViewHost(new DoctorView { DataContext = model }, 1440, 900);
        await model.ShownAsync();
        host.Pump();

        Assert.Equal(DoctorVerdictKind.Ready, model.Kind);
        Assert.Equal("Ready for pushes", model.Heading);
        Assert.Equal("Every check passes. A push from this machine is reviewed before it leaves.", string.Concat(model.Sentence.Select(part => part.Text)));
        Assert.All(model.Groups.SelectMany(group => group.Checks), row => Assert.False(row.HasFix));
        var buttons = host.FindAll<Button>(button => button.IsEffectivelyVisible).Select(Avalonia.Automation.AutomationProperties.GetName);
        Assert.Equal(["Check again"], buttons);
    }

    [AvaloniaFact]
    public async Task CheckingKeepsTheLastResultsDisablesCheckAgainAndDropsALateReportForAnotherFolder()
    {
        var other = new OpenedFolder("/work/billing", "/work/billing/commitfence.json", false, "billing", "main", true);
        var held = new TaskCompletionSource();
        var entered = new TaskCompletionSource();
        _doctor.Answer = folder =>
        {
            if(folder == "/work/billing")
                return FakeDoctorService.Report(heading: "2 checks fail");

            if(!held.Task.IsCompleted && _doctor.Folders.Count > 1)
            {
                entered.TrySetResult();
                held.Task.Wait();
            }

            return FakeDoctorService.Report();
        };
        var model = Create(Samples.Folder("/work/invoicing"));
        using var host = new ViewHost(new DoctorView { DataContext = model }, 1440, 900);

        // the first report, then "Check again": the verdict says Checking, the rows keep their results behind the busy ring
        await model.ShownAsync();
        host.Pump();
        Assert.Equal("Ready for pushes", model.Heading);
        Assert.True(model.AgainCommand.CanExecute(null));
        var again = model.CheckAsync();
        await entered.Task;
        host.Pump();
        Assert.Equal("Checking", model.Heading);
        Assert.Equal("3 checks of git, the program, C# and CommitFence.", string.Concat(model.Sentence.Select(part => part.Text)));
        Assert.False(model.AgainCommand.CanExecute(null));
        Assert.False(host.Find<Button>(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Check again")!.IsEffectivelyEnabled);
        Assert.All(model.Groups.SelectMany(group => group.Checks), row => Assert.True(row.IsBusy && row.ShowsRing && !row.ShowsPass));

        // the screen moves to another repository while that check runs: its report arrives late and is dropped
        await model.ShowFolderAsync(other);
        host.Pump();
        Assert.Equal("2 checks fail", model.Heading);
        Assert.Equal("Whether this machine reviews the pushes of billing", model.Meta);
        held.SetResult();
        await again;
        host.Pump();
        Assert.Equal("2 checks fail", model.Heading);
        Assert.True(model.AgainCommand.CanExecute(null));
        Assert.Equal(["/work/invoicing", "/work/invoicing", "/work/billing"], _doctor.Folders);
    }

    [AvaloniaFact]
    public async Task CopyTheHookLinePutsTheBodysTextOnTheClipboardAndReadsCopied()
    {
        _doctor.Answer = _ => FakeDoctorService.Report(hookFails: true);
        var model = Create(Samples.Folder("/work/invoicing"));
        var clipboard = new FakeClipboard();
        using var host = new ViewHost(new DoctorView { DataContext = model }, 1440, 900);
        model.Clipboard = clipboard;
        await model.ShownAsync();
        host.Pump();

        var copy = model.Groups.SelectMany(group => group.Checks).Single(row => row.HasFix).Actions.Single(action => action.IsCopy).Copy!;
        await copy.CopyCommand.ExecuteAsync(null);
        host.Pump();

        Assert.Equal(FakeDoctorService.HookLine, clipboard.Text);
        Assert.Equal("Copied", copy.SpokenName);
        Assert.NotNull(host.Find<TextBlock>(text => text.Text == "Copied" && text.IsEffectivelyVisible));
        copy.EndCopied();
        Assert.Equal("Copy the hook line", copy.SpokenName);

        // "Open the hook script" asks the window to open the file at the line the body names
        (string Path, int? Line)? asked = null;
        model.OpenFileRequested += (path, line) => asked = (path, line);
        model.Groups.SelectMany(group => group.Checks).Single(row => row.HasFix).Actions.Single(action => action.IsOpen).OpenCommand.Execute(null);
        Assert.Equal(("/work/invoicing/.git/hooks/pre-push", 4), asked);
    }

    [AvaloniaFact]
    public async Task ReasonThatSaysTheMachineIsNotReadyOffersOpenDoctor()
    {
        using var shell = new ShellHost();
        shell.Review.Result = _ => Samples.Result(ReviewOutcome.Incomplete, null,
        [
            Samples.NotReviewed("Billing.csproj", "The machine is not ready: the .NET SDK is not installed (the dotnet command cannot be started), so the C# code cannot be built."),
            Samples.NotReviewed("src/c.cs", "the file does not parse")
        ]);
        shell.Model.ShowReview();
        await shell.Model.Review.RunCommand.ExecuteAsync(null);

        // only the reason about the machine has the button, in its opened part
        var reasons = shell.Model.Review.Reasons;
        Assert.True(reasons[0].OpensDoctor);
        Assert.False(reasons[1].OpensDoctor);

        reasons[0].OpenDoctorCommand.Execute(null);
        Assert.Equal(Screen.Doctor, shell.Model.CurrentScreen);
    }

    [AvaloniaFact]
    public async Task F5WhileChecksRunStartsNoSecondCheckAndTheKeyboardMovesOnlyOutOfAFixThatWentAway()
    {
        using var shell = new ShellHost();
        shell.Doctor.Answer = _ => FakeDoctorService.Report(hookFails: true);
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 });
        shell.Model.ShowDoctor();
        while(shell.Model.Doctor.IsChecking || shell.Model.Doctor.Groups.Count == 0)
        {
            host.Pump();
            await Task.Yield();
        }

        // the report may arrive after the last pump of the wait: lay the rows out before looking for a button of theirs
        host.Pump();

        // F5 held down: the check that runs is the only one
        var held = new TaskCompletionSource();
        shell.Doctor.Answer = _ =>
        {
            held.Task.Wait();
            return FakeDoctorService.Report();
        };
        var copy = host.Find<Button>(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Copy the hook line")!;
        copy.Focus(NavigationMethod.Tab);
        host.Press(PhysicalKey.F5);
        host.Press(PhysicalKey.F5);
        host.Press(PhysicalKey.F5);
        Assert.Equal(2, shell.Doctor.Folders.Count);
        held.SetResult();
        while(shell.Model.Doctor.IsChecking)
        {
            host.Pump();
            await Task.Yield();
        }

        // the fix the keyboard was in is gone: the keyboard is on "Check again"
        host.Pump();
        var checkAgain = host.Find<Button>(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Check again")!;
        Assert.True(host.HasFocusWithin(checkAgain));

        // a fix that goes away while the keyboard is elsewhere leaves the keyboard where it is
        shell.Doctor.Answer = _ => FakeDoctorService.Report(hookFails: true);
        await shell.Model.Doctor.CheckAsync();
        host.Pump();
        shell.Doctor.Answer = _ => FakeDoctorService.Report();
        host.Window.FocusManager?.Focus(null);
        await shell.Model.Doctor.CheckAsync();
        host.Pump();
        Assert.False(host.HasFocusWithin(checkAgain));
    }

    private DoctorViewModel Create(OpenedFolder folder)
    {
        return new DoctorViewModel(_doctor, _clock, folder);
    }
}
