using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CF.App.Parts;
using CF.App.Settings;
using CF.App.Shell;
using CF.App.Tests.Support;
using CF.Core.Findings;
using CF.Core.Rules;
using CF.Core.Updates;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// The Settings screen: the strictness choice and its table, the update status row in each of its states, the notes of the version,
/// and the way the notice's "What's new" lands here. A fake settings service, updater and languages answer.
/// </summary>
public sealed class SettingsScreenTests
{
    private const string _notes = "Intro.\n\n### The app\n\n- **Rules** one.\n- Two.\n\n### Fixes\n\n- One `code`.\n\n### Downloads\n\n| a | b |";

    private Task? _running;

    [AvaloniaFact]
    public async Task ChoosingStrictCallsTheServiceAndTheTableFollowsARefusalShowsTheBannerAndCustomStandsOnlyForACustomMapping()
    {
        using var shell = new ShellHost();
        var model = shell.Model.Settings;
        using var host = new ViewHost(new SettingsView { DataContext = model }, 1440, 900);
        await model.RefreshAsync();
        host.Pump();

        // the choice, the column in force, the sentence with its three counts, and the card of the one language
        Assert.Equal(["Quiet", "Standard", "Strict"], model.Choices.Select(choice => choice.Label));
        Assert.Equal("Standard", model.SelectedChoice?.Label);
        Assert.Equal(["Standard"], model.Columns.Where(column => column.IsInForce).Select(column => column.Name));
        Assert.StartsWith("With Standard, **41** rules of the file block a push, **6** only warn and **31** are off.", model.Sentence, StringComparison.Ordinal);
        Assert.Equal("Files ending in .cs: 502 in invoicing. A full review builds them with the repository's .NET SDK.", Assert.Single(model.LanguageCards).Detail);
        Assert.Equal("Each language comes as a plug-in. This version has one.", model.LanguagesNote);

        // "Strict" is saved at once through the service, and the marked column, the cells and the sentence follow
        model.SelectedChoice = model.Choices[2];
        await model.ChoiceSettledAsync();
        host.Pump();
        Assert.Equal([StrictnessPreset.Strict], shell.SettingsService.Saved);
        Assert.Equal(["Strict"], model.Columns.Where(column => column.IsInForce).Select(column => column.Name));
        Assert.Equal(["Strict"], model.Choices.Where(choice => choice.IsCurrent).Select(choice => choice.Label));
        Assert.Equal("Blocks", model.Rows[1].Cells[2].Word);
        Assert.True(model.Rows[1].Cells[2].IsInForce);
        Assert.StartsWith("With Strict, **47** rules of the file block a push, **0** only warn and **31** are off.", model.Sentence, StringComparison.Ordinal);
        Assert.False(model.HasBanner);

        // the file cannot be written: the banner says why and the choice goes back to what the file says
        shell.SettingsService.SaveFailure = new RuleChangeException("commitfence.json changed since it was read, so nothing was written.");
        model.SelectedChoice = model.Choices[0];
        await model.ChoiceSettledAsync();
        host.Pump();
        Assert.Equal("commitfence.json changed since it was read, so nothing was written.", model.BannerText);
        Assert.Equal("Strict", model.SelectedChoice?.Label);
        Assert.Equal([StrictnessPreset.Strict], shell.SettingsService.Saved);

        // the note's words never change; whether it shows is decided by IsCustom alone, so a screen bound early shows it when the report arrives
        Assert.False(model.IsCustom);
        Assert.Equal("This repository has a mapping of its own in commitfence.json. Remove it there to choose a preset.", model.CustomNote);

        // "Custom" stands only while the file holds a mapping of its own; a preset is not applied over it
        shell.SettingsService.SaveFailure = null;
        shell.SettingsService.Custom = new Strictness(StrictnessPreset.Custom, null, Severity.Block, Severity.Warn);
        await model.RefreshAsync();
        host.Pump();
        Assert.Equal(["Quiet", "Standard", "Strict", "Custom"], model.Choices.Select(choice => choice.Label));
        Assert.Equal("Custom", model.SelectedChoice?.Label);
        Assert.Equal("Set in commitfence.json", model.Choices[3].Tip);
        Assert.Equal(["Custom"], model.Columns.Where(column => column.IsInForce).Select(column => column.Name));
        model.SelectedChoice = model.Choices[0];
        await model.ChoiceSettledAsync();
        Assert.Equal("Custom", model.SelectedChoice?.Label);
        Assert.Equal([StrictnessPreset.Strict], shell.SettingsService.Saved);

        // while the mapping is in force the presets are off, not ignored in silence, and the choice says why; Custom keeps its tip
        host.Pump();
        Assert.Equal([false, false, false, true], model.Choices.Select(choice => choice.IsEnabled));
        Assert.Equal("This repository has a mapping of its own in commitfence.json. Remove it there to choose a preset.", model.CustomNote);
        var segments = host.Window.GetVisualDescendants().OfType<ListBoxItem>().Where(item => item.FindAncestorOfType<SegmentedList>()?.Name == "StrictnessChoice").ToList();
        Assert.Equal([false, false, false, true], segments.Select(segment => segment.IsEnabled));
        Assert.Contains(host.Window.GetVisualDescendants().OfType<TextBlock>(), block => block.Name == "CustomNote" && block.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task ChoosingStrictAndAtOnceStandardAgainLeavesStandardInTheFile()
    {
        using var shell = new ShellHost();
        var model = shell.Model.Settings;
        await model.RefreshAsync();

        // the second choice is made while the first save is still in flight: the saves run in order and the last choice is the one kept
        using var gate = new ManualResetEventSlim(false);
        shell.SettingsService.SaveGate = gate;
        model.SelectedChoice = model.Choices[2];
        model.SelectedChoice = model.Choices[1];
        gate.Set();
        await model.ChoiceSettledAsync();
        Assert.Equal([StrictnessPreset.Strict, StrictnessPreset.Standard], shell.SettingsService.Saved);
        Assert.Equal("Standard", model.SelectedChoice?.Label);
        Assert.Equal(["Standard"], model.Columns.Where(column => column.IsInForce).Select(column => column.Name));
        Assert.StartsWith("With Standard, ", model.Sentence, StringComparison.Ordinal);
    }

    [AvaloniaTheory]
    [InlineData("UpToDate", "Up to date. Checked today, {time}.", "Check now")]
    [InlineData("LastChecked", "Last checked today, {time}.", "Check now")]
    [InlineData("NeverChecked", "Not checked yet.", "Check now")]
    [InlineData("Checking", "Checking for updates", "")]
    [InlineData("CouldNotCheck", "Could not check for updates. GitHub could not be reached.", "Try again")]
    [InlineData("Available", "9.0.0.0 is available. You have 1.0.0.0.", "Update now")]
    [InlineData("CannotUpdate", "9.0.0.0 is available. You have 1.0.0.0. This copy is not a release download, so it cannot update itself.", "Open the release page")]
    [InlineData("Downloading", "Downloading 9.0.0.0 64%", "Cancel")]
    [InlineData("SizeNotKnown", "Downloading 9.0.0.0", "Cancel")]
    [InlineData("Cancelling", "Cancelling the update", "")]
    [InlineData("Replacing", "Replacing the files with 9.0.0.0 Too late to cancel.", "")]
    [InlineData("Installed", "9.0.0.0 is installed. Restarting.", "")]
    [InlineData("InstalledCouldNotRestart", "9.0.0.0 is installed. Close the app and start it again to use it.", "")]
    [InlineData("Cancelled", "The update was cancelled. You still have 1.0.0.0.", "Check now")]
    [InlineData("Failed", "The update failed. The download is damaged. Nothing was changed. You still have 1.0.0.0. Try again, or download it from the release page.", "Open the release page, Try again")]
    public async Task StatusRowSaysWhatItsTableSaysForEachStateWithItsButtons(string stateName, string words, string buttons)
    {
        var kind = Enum.Parse<UpdateStatusKind>(stateName);
        using var shell = new ShellHost();
        var status = shell.Model.Settings.Status;
        await Reach(shell, kind);

        Assert.Equal(kind, status.Kind);
        Assert.Equal(words.Replace("{time}", Samples.Now.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal), status.SpokenText);
        Assert.Equal(buttons, string.Join(", ", status.Buttons.Select(ButtonLabel)));
        Assert.Equal(kind is UpdateStatusKind.Downloading or UpdateStatusKind.SizeNotKnown, status.ShowsBar);
        Assert.Equal(kind == UpdateStatusKind.SizeNotKnown, status.BarFraction < 0);

        // the status leads the page in one pane for every state but the quiet ones
        Assert.Equal(kind is not (UpdateStatusKind.UpToDate or UpdateStatusKind.LastChecked or UpdateStatusKind.NeverChecked or UpdateStatusKind.Checking), status.LeadsOnePane);
        Assert.Equal(kind is UpdateStatusKind.UpToDate or UpdateStatusKind.Installed or UpdateStatusKind.InstalledCouldNotRestart, status.ShowsTick);
        await Finish(shell);
    }

    [AvaloniaFact]
    public async Task ACheckAskedForAnswersInTheStatusRowAndTheNoticeStaysHiddenAndWhatsNewLandsWithTheNotesShown()
    {
        using var shell = new ShellHost();
        shell.Record.LastCheckUtc = Samples.Now.UtcDateTime.AddHours(-3);
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 });
        var status = shell.Model.Settings.Status;

        // a check of an earlier run claims nothing: no tick, and "Check now"
        Assert.Equal(UpdateStatusKind.LastChecked, status.Kind);
        Assert.StartsWith("Last checked today, ", status.Rest, StringComparison.Ordinal);
        Assert.False(status.ShowsTick);

        // F5 on Settings is "Check now": it answers "Up to date" here, and the notice does not appear
        shell.Model.CurrentScreen = Screen.Settings;
        host.Pump();
        host.Press(PhysicalKey.F5);
        await WaitUntil(host, () => shell.Updater.Checks == 1 && shell.Model.Updates.IsIdle);
        Assert.Equal(UpdateStatusKind.UpToDate, status.Kind);
        Assert.Equal($"Up to date. Checked today, {Samples.Now.ToLocalTime():HH:mm}.", status.Rest);
        Assert.False(shell.Model.Updates.IsNoticeVisible);

        // a check that could not finish says so here, with its reason, and the notice stays away
        shell.Updater.Result = new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Problem = "GitHub could not be reached." };
        await status.CheckNowCommand.ExecuteAsync(null);
        Assert.Equal(UpdateStatusKind.CouldNotCheck, status.Kind);
        Assert.Equal("GitHub could not be reached.", status.Meta);
        Assert.False(shell.Model.Updates.IsNoticeVisible);

        // a newer version found by the automatic check shows the notice; "What's new" opens Settings with the notes shown and the keyboard on "Update now"
        shell.Updater.Result = FakeUpdater.Available("9.0.0.0", _notes);
        shell.Model.CurrentScreen = Screen.Dashboard;
        await shell.Model.Updates.CheckAsync(false);
        host.Pump();
        Assert.True(shell.Model.Updates.IsNoticeVisible);

        // a check asked for that fails while the update is known: the row keeps its words and its button and says so under them
        shell.Updater.Result = new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Problem = "GitHub could not be reached." };
        await status.CheckNowCommand.ExecuteAsync(null);
        Assert.Equal(UpdateStatusKind.Available, status.Kind);
        Assert.Equal("9.0.0.0 is available. You have 1.0.0.0.", status.Lead + " " + status.Rest);
        Assert.Equal("Could not check for updates. GitHub could not be reached.", status.Meta);
        Assert.Equal([UpdateStatusButton.UpdateNow], status.Buttons);
        shell.Updater.Result = FakeUpdater.Available("9.0.0.0", _notes);
        await shell.Model.Updates.CheckAsync(false);
        Assert.Equal(string.Empty, status.Meta);
        host.Find<Button>(button => button.Name == "WhatsNewButton" && button.IsEffectivelyVisible)!.Command!.Execute(null);
        await WaitUntil(host, () => shell.Model.Settings.Notes.Lines.Count > 0 && host.Find<Button>(InSettings("UpdateNowButton")) != null);
        Assert.Equal(Screen.Settings, shell.Model.CurrentScreen);
        Assert.True(shell.Model.Settings.ShowsNotes);
        Assert.True(shell.Model.Updates.IsNoticeVisible);
        Assert.True(host.HasFocusWithin(host.Find<Button>(InSettings("UpdateNowButton"))!));

        // Ctrl+U is "Update now" while it shows
        shell.Model.Updates.StartAndExit = _ => false;
        shell.Updater.Delivery = ProgressDelivery.Never;
        shell.Updater.HoldInstall = new TaskCompletionSource();
        host.Press(PhysicalKey.U, Avalonia.Input.RawInputModifiers.Control);
        await shell.Updater.InstallStarted.Task;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(UpdateStatusKind.Downloading, status.Kind);
        shell.Updater.HoldInstall.SetResult();
        await WaitUntil(host, () => shell.Model.Updates.IsIdle);
    }

    [AvaloniaFact]
    public async Task NotesFoldedGiveTheirHeadingsWithCountsAndShownGiveTheItems()
    {
        using var shell = new ShellHost();
        var notes = shell.Model.Settings.Notes;

        // the version installed: its heading, folded
        Assert.Equal($"What's in {shell.Services.ProductInfo.Version}", notes.Heading);
        Assert.StartsWith("Released ", notes.Released, StringComparison.Ordinal);
        Assert.True(notes.HasNotes);
        Assert.True(notes.ShowsFolded);

        // a version that waits: its headings with how many notes stand under each, and no date (the release gives none)
        shell.Updater.Result = FakeUpdater.Available("9.0.0.0", _notes);
        await shell.Model.Updates.CheckAsync(false);
        Assert.Equal("What's new in 9.0.0.0", notes.Heading);
        Assert.Equal(string.Empty, notes.Released);
        Assert.Equal(["The app, 2 notes", "Fixes, 1 note"], notes.Headings.Select(heading => heading.SpokenName));
        Assert.True(notes.ShowsFolded);

        // shown: the paragraph, each heading and the items with their bold and code marks, up to the downloads
        notes.ShowTheNotesCommand.Execute(null);
        Assert.True(notes.ShowsWhole);
        Assert.Equal(["Intro.", "The app", "**Rules** one.", "Two.", "Fixes", "One `code`."], notes.Lines.Select(line => line.Marked));
        Assert.Equal([false, true, false, false, true, false], notes.Lines.Select(line => line.IsHeading));
        Assert.Equal([false, false, true, true, false, true], notes.Lines.Select(line => line.IsItem));
        Assert.True(notes.CanOpenReleasePage);
        notes.HideTheNotesCommand.Execute(null);
        Assert.True(notes.ShowsFolded);

        // notes longer than the notice's limit are shown whole, so the items shown agree with the folded counts
        var longList = string.Join("\n", Enumerable.Range(1, 60).Select(number => $"- Note number {number} says something that fills the line."));
        shell.Updater.Result = FakeUpdater.Available("9.1.0.0", "### Everything\n\n" + longList);
        await shell.Model.Updates.CheckAsync(false);
        notes.ShowTheNotesCommand.Execute(null);
        Assert.Equal("Everything, 60 notes", Assert.Single(notes.Headings).SpokenName);
        Assert.Equal(60, notes.Lines.Count(line => line.IsItem));
    }

    [AvaloniaFact]
    public async Task OnePaneLeadsWithTheStatusWhileAnUpdateWaitsAndTheSwitchOfMotionIsKeptOnThisMachine()
    {
        using var shell = new ShellHost();
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 760, Height = 1000 });
        shell.Model.CurrentScreen = Screen.Settings;
        await WaitUntil(host, () => shell.Model.Settings.LanguageCards.Count > 0);
        var view = host.Find<SettingsView>(_ => true)!;
        var repository = view.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == "RepositoryColumn");
        var app = view.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Name == "AppColumn");
        var columns = view.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Name == "Columns");

        // up to date: this repository first, this app 48 under it
        Assert.Equal([repository, app], columns.Children);
        Assert.Equal("AppHead", ((Control)app.Children[0]).Name);

        // an update waits: this app comes first, and its status row leads it
        shell.Updater.Result = FakeUpdater.Available("9.0.0.0");
        await shell.Model.Updates.CheckAsync(false);
        host.Pump();
        Assert.Equal([app, repository], columns.Children);
        Assert.Equal("StatusRow", ((Control)app.Children[0]).Name);
        Assert.True(view.IsEffectivelyVisible);

        // two columns from 1304 wide, in the order of the page
        host.Window.Width = 1440;
        host.Pump();
        Assert.Equal([repository, app], columns.Children);

        // on a system with no setting of its own the row of motion is the switch, saved at once on this machine and followed by the app
        var motion = Motion.ReducedOnThisMachine;
        try
        {
            var model = new SettingsViewModel(shell.Services, Samples.Folder(shell.RepositoryPath), shell.Model.Updates, false);
            var row = model.Facts.Single(fact => fact.Name == "Motion");
            Assert.True(row.HasSwitch);
            Assert.Equal("Reduce motion", row.Value);
            row.IsOn = true;
            Assert.True(shell.Machine.ReduceMotion);
            Assert.True(Motion.ReducedOnThisMachine);
        }
        finally
        {
            Motion.ReducedOnThisMachine = motion;
        }
    }

    private static Func<Button, bool> InSettings(string name)
    {
        return button => button.Name == name && button.IsEffectivelyVisible && button.FindAncestorOfType<SettingsView>() != null;
    }

    private static async Task WaitUntil(ViewHost host, Func<bool> condition)
    {
        for(var turn = 0; turn < 200 && !condition(); turn++)
        {
            host.Pump();
            await Task.Delay(5);
        }

        Assert.True(condition());
        host.Pump();
    }

    private static string ButtonLabel(UpdateStatusButton button)
    {
        return button switch
        {
            UpdateStatusButton.CheckNow => "Check now",
            UpdateStatusButton.TryCheckAgain => "Try again",
            UpdateStatusButton.UpdateNow => "Update now",
            UpdateStatusButton.Cancel => "Cancel",
            UpdateStatusButton.OpenReleasePage => "Open the release page",
            UpdateStatusButton.TryUpdateAgain => "Try again",
            _ => throw new ArgumentOutOfRangeException(nameof(button))
        };
    }

    /// <summary>
    /// Brings the model to a state of the status row through the fake updater, the way the real one would.
    /// </summary>
    private async Task Reach(ShellHost shell, UpdateStatusKind kind)
    {
        var updates = shell.Model.Updates;
        var updater = shell.Updater;
        updater.Result = FakeUpdater.Available("9.0.0.0");
        updater.Delivery = ProgressDelivery.Before;
        updater.Problem = kind == UpdateStatusKind.CannotUpdate ? "This copy is not a release download, so it cannot update itself." : null;
        updates.StartAndExit = _ => kind == UpdateStatusKind.Installed;
        shell.Record.LastCheckUtc = kind == UpdateStatusKind.NeverChecked ? null : Samples.Now.UtcDateTime;
        switch(kind)
        {
            case UpdateStatusKind.LastChecked:
            case UpdateStatusKind.NeverChecked:
                return;
            case UpdateStatusKind.UpToDate:
                updater.Result = new UpdateCheckResult { Status = UpdateCheckStatus.UpToDate, Release = new ReleaseInfo { Version = new Version(1, 0, 0, 0) } };
                await updates.CheckAsync(true);
                updates.StatusText = null;
                return;
            case UpdateStatusKind.Checking:
                updater.HoldCheck = new TaskCompletionSource();
                _running = updates.CheckAsync(true);
                return;
            case UpdateStatusKind.CouldNotCheck:
                updater.Result = new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Problem = "GitHub could not be reached." };
                await updates.CheckAsync(true);
                return;
        }

        await updates.CheckAsync(false);
        switch(kind)
        {
            case UpdateStatusKind.Available:
            case UpdateStatusKind.CannotUpdate:
                return;
            case UpdateStatusKind.Installed:
            case UpdateStatusKind.InstalledCouldNotRestart:
                await updates.InstallAsync();
                return;
            case UpdateStatusKind.Failed:
                updater.InstallFailure = new UpdateException("The download is damaged. Nothing was changed.");
                await updates.InstallAsync();
                return;
        }

        // the states of an update that runs: it is held until the test lets it go
        updater.HoldInstall = new TaskCompletionSource();
        _running = updates.InstallAsync();
        await updater.InstallStarted.Task;
        Dispatcher.UIThread.RunJobs();
        if(kind is UpdateStatusKind.Cancelled or UpdateStatusKind.Cancelling)
        {
            updates.CancelUpdateCommand.Execute(null);
            if(kind == UpdateStatusKind.Cancelled)
            {
                await _running;
                _running = null;
            }

            return;
        }

        var report = kind == UpdateStatusKind.Replacing ? new UpdateProgress { Stage = UpdateStage.Replacing } : new UpdateProgress { Stage = UpdateStage.Downloading, Percent = kind == UpdateStatusKind.SizeNotKnown ? -1 : 64 };
        updater.LastProgress!.Report(report);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Lets go of what a state was held on, so that nothing is left running.
    /// </summary>
    private async Task Finish(ShellHost shell)
    {
        shell.Updater.HoldCheck?.TrySetResult();
        shell.Updater.HoldInstall?.TrySetResult();
        if(_running != null)
            await _running;
    }
}
