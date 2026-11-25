using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using CF.App.Parts;
using CF.App.Shell;
using CF.App.Tests.Support;
using CF.App.Updates;
using CF.Core.Updates;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// The notice of a newer version in the app: the check once a day and when asked, Later, Update now with its progress, Cancel and the
/// restart, and the copy that cannot update itself. A fake updater answers; nothing is replaced and no network is used.
/// </summary>
public sealed class UpdateNoticeTests
{
    private readonly FakeUpdater _updater = new FakeUpdater();
    private readonly MemoryCheckRecord _record = new MemoryCheckRecord();
    private readonly FixedClock _clock = new FixedClock(Samples.Now);

    [AvaloniaTheory]
    [InlineData("current")]
    [InlineData("available")]
    [InlineData("unavailable")]
    public async Task NoticeDistinguishesManualAndAutomaticChecks(string answer)
    {
        if(answer == "available")
            _updater.Result = FakeUpdater.Available("9.0.0.0", "The **best** version.\n\n### Fixes\n\n- One.\n\n### Downloads\n\n| a | b |");
        else if(answer == "unavailable")
            _updater.Result = new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Problem = "GitHub could not be reached." };

        var automatic = Create();
        await automatic.CheckAsync(false);
        var manual = Create();
        await manual.CheckAsync(true);

        if(answer == "current")
        {
            // nothing to say unless asked; and then it says the version
            Assert.False(automatic.IsNoticeVisible);
            Assert.Equal($"You have the latest version ({Samples.InstalledVersion}).", manual.StatusText);
            Assert.False(manual.CanUpdate);
            Assert.Equal(Samples.Now.UtcDateTime, _record.LastCheckUtc);

            // the automatic check follows the clock: once a day
            var later = Create();
            await later.CheckIfDueAsync();
            await later.CheckIfDueAsync();
            Assert.Equal(2, _updater.Checks);
            _clock.Now = Samples.Now.AddHours(23);
            await later.CheckIfDueAsync();
            Assert.Equal(2, _updater.Checks);
            _clock.Now = Samples.Now.AddHours(25);
            await later.CheckIfDueAsync();
            Assert.Equal(3, _updater.Checks);
        }
        else if(answer == "available")
        {
            // the same notice, asked or not: the version, the notes and the way to update
            foreach(var updates in new[] { automatic, manual })
            {
                Assert.True(updates.IsNoticeVisible);
                Assert.Equal("CommitFence 9.0.0.0 is available.", updates.Title);
                Assert.Equal($"You have {Samples.InstalledVersion}. Nothing is downloaded until you say so.", updates.Line);
                Assert.True(updates.CanUpdate);
                Assert.True(updates.CanDismiss);
                Assert.True(updates.HasNotes);
                Assert.Equal("The best version.", updates.NotesLines[0].Text);
                Assert.Equal(3, updates.NotesLines.Count);
                Assert.Null(updates.StatusText);
            }
        }
        else
        {
            // no network is normal: the automatic check says nothing, the one asked for says why, and neither counts as the day's check
            Assert.False(automatic.IsNoticeVisible);
            Assert.Null(automatic.StatusText);
            Assert.StartsWith("Could not check for updates.", manual.StatusText, StringComparison.Ordinal);
            Assert.Contains("could not be reached", manual.StatusText, StringComparison.Ordinal);
            Assert.Null(_record.LastCheckUtc);
        }
    }

    [AvaloniaFact]
    public async Task LaterSuppressesOnlyTheDismissedRelease()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        var updates = Create();
        await updates.CheckAsync(false);

        updates.DismissCommand.Execute(null);
        Assert.False(updates.IsNoticeVisible);

        // an automatic check does not bring the same version back
        await updates.CheckAsync(false);
        Assert.False(updates.IsNoticeVisible);

        // a newer one does
        _updater.Result = FakeUpdater.Available("9.1.0.0");
        await updates.CheckAsync(false);
        Assert.True(updates.IsNoticeVisible);

        // and so does asking
        updates.DismissCommand.Execute(null);
        await updates.CheckAsync(true);
        Assert.True(updates.IsNoticeVisible);
    }

    [AvaloniaTheory]
    [InlineData(ProgressDelivery.Before)]
    [InlineData(ProgressDelivery.After)]
    [InlineData(ProgressDelivery.Never)]
    public async Task InstalledAppRestartsOrOffersManualRestart(ProgressDelivery delivery)
    {
        _updater.Delivery = delivery;
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        var updates = Create();
        ProcessStartInfo? started = null;
        updates.StartAndExit = info =>
        {
            started = info;
            return true;
        };
        await updates.CheckAsync(false);

        await updates.InstallAsync();
        _updater.DeliverHeld();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, _updater.Installs);
        Assert.NotNull(started);
        Assert.Equal("/work/repo", Assert.Single(started.ArgumentList));
        Assert.Equal(UpdateNoticeState.Installed, updates.NoticeState);
        Assert.Equal("9.0.0.0 is installed.", updates.Title);
        Assert.Equal("Restarting.", updates.Line);

        // the files were replaced: Cancel is gone
        Assert.True(updates.IsReplacing);
        Assert.False(updates.CanCancel);

        // an app that cannot start itself again says so, and stays usable
        var other = Create();
        _updater.Delivery = ProgressDelivery.Never;
        other.StartAndExit = _ => false;
        await other.CheckAsync(false);
        await other.InstallAsync();
        Assert.Equal(UpdateNoticeState.InstalledCouldNotRestart, other.NoticeState);
        Assert.Equal("Close the app and start it again to use it.", other.Line);
        Assert.False(other.IsInstalling);
    }

    [AvaloniaTheory]
    [InlineData("cancelled", ProgressDelivery.Before)]
    [InlineData("cancelled", ProgressDelivery.After)]
    [InlineData("cancelled", ProgressDelivery.Never)]
    [InlineData("failed", ProgressDelivery.Before)]
    [InlineData("failed", ProgressDelivery.After)]
    [InlineData("failed", ProgressDelivery.Never)]
    public async Task UnfinishedInstallKeepsAppUsable(string ending, ProgressDelivery delivery)
    {
        _updater.Delivery = delivery;
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        var updates = Create();
        var restarted = false;
        updates.StartAndExit = _ =>
        {
            restarted = true;
            return true;
        };
        await updates.CheckAsync(false);

        if(ending == "cancelled")
        {
            _updater.HoldInstall = new TaskCompletionSource();
            var install = updates.InstallAsync();
            await _updater.InstallStarted.Task;
            Dispatcher.UIThread.RunJobs();
            Assert.True(updates.CanCancel);
            Assert.False(updates.CanDismiss);

            updates.CancelUpdateCommand.Execute(null);
            await install;

            Assert.Equal(UpdateNoticeState.Cancelled, updates.NoticeState);
            Assert.Equal("The update was cancelled.", updates.Title);
            Assert.Equal($"You still have {Samples.InstalledVersion}.", updates.Line);
        }
        else
        {
            _updater.InstallFailure = new UpdateException("The download of version 9.0.0.0 is damaged. Nothing was changed.");

            await updates.InstallAsync();

            Assert.Equal(UpdateNoticeState.Failed, updates.NoticeState);
            Assert.Equal("The update failed.", updates.Title);
            Assert.Contains("damaged", updates.Line, StringComparison.Ordinal);
            Assert.EndsWith($" You still have {Samples.InstalledVersion}.", updates.Line, StringComparison.Ordinal);
        }

        // reports that were held back reach the app after the install has ended: they change nothing that is shown
        Dispatcher.UIThread.RunJobs();
        var shown = updates.NoticeState;
        var percent = updates.DownloadPercent;
        _updater.DeliverHeld();
        _updater.LastProgress!.Report(new UpdateProgress { Stage = UpdateStage.Downloading, Percent = 90 });
        _updater.LastProgress.Report(new UpdateProgress { Stage = UpdateStage.Replacing });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(shown, updates.NoticeState);
        Assert.Equal(percent, updates.DownloadPercent);
        Assert.False(updates.IsReplacing);

        Assert.False(restarted);
        Assert.False(updates.IsInstalling);
        Assert.True(updates.IsIdle);
        Assert.True(updates.CanUpdate);
        Assert.True(updates.CanDismiss);
    }

    [AvaloniaFact]
    public async Task UnsupportedCopyOffersReleasePage()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        _updater.Problem = "This copy is not a release download, so it cannot update itself.";
        var updates = Create();
        Uri? opened = null;
        updates.OpenUri = uri =>
        {
            opened = uri;
            return Task.CompletedTask;
        };
        await updates.CheckAsync(false);

        Assert.False(updates.CanUpdate);
        Assert.True(updates.CanOpenReleasePage);
        Assert.True(updates.HasProblem);
        Assert.StartsWith("This copy is not a release download", updates.ProblemText, StringComparison.Ordinal);

        // nothing is downloaded
        await updates.InstallAsync();
        Assert.Equal(0, _updater.Installs);

        updates.OpenReleasePageCommand.Execute(null);
        Assert.Equal("https://github.com/TheSharpFactory/CommitFence/releases/tag/v9.0.0.0", opened?.ToString());
    }

    [AvaloniaTheory]
    [InlineData("Available", "CommitFence 9.0.0.0 is available.", "You have {installed}. Nothing is downloaded until you say so.", "none", "Later, What's new, Update now")]
    [InlineData("CannotUpdate", "CommitFence 9.0.0.0 is available.", "You have {installed}. This copy is not a release download, so it cannot update itself.", "none", "Later, Open the release page")]
    [InlineData("Downloading", "Downloading 9.0.0.0", "The app restarts when it is in place.", "64%", "Cancel")]
    [InlineData("SizeNotKnown", "Downloading 9.0.0.0", "The app restarts when it is in place.", "moving", "Cancel")]
    [InlineData("Cancelling", "Cancelling the update", "You will keep {installed}.", "none", "")]
    [InlineData("Replacing", "Replacing the files with 9.0.0.0", "Too late to cancel. It takes a moment.", "none", "")]
    [InlineData("Installed", "9.0.0.0 is installed.", "Restarting.", "none", "")]
    [InlineData("InstalledCouldNotRestart", "9.0.0.0 is installed.", "Close the app and start it again to use it.", "none", "Close")]
    [InlineData("Cancelled", "The update was cancelled.", "You still have {installed}.", "none", "Close")]
    [InlineData("Failed", "The update failed.", "The download is damaged. Nothing was changed. You still have {installed}.", "none", "Close, Open the release page, Try again")]
    public async Task NoticeSaysWhatItsTableSaysForEachState(string stateName, string title, string line, string bar, string buttons)
    {
        var state = Enum.Parse<UpdateNoticeState>(stateName);
        var updates = Create();
        await Reach(updates, state);

        // the title, the line, the bar (its percent, or a piece that moves, or none) and the buttons, as the table of the notice gives them
        Assert.Equal(state, updates.NoticeState);
        Assert.True(updates.IsNoticeVisible);
        Assert.Equal(title, updates.Title);
        Assert.Equal(line.Replace("{installed}", Samples.InstalledVersion, StringComparison.Ordinal), updates.Line);
        Assert.Equal(bar, updates.ShowsBar ? (updates.ShowsPercent ? updates.PercentText : "moving") : "none");
        Assert.Equal(buttons, string.Join(", ", updates.Buttons.Select(ButtonLabel)));
        Assert.Equal(state == UpdateNoticeState.SizeNotKnown, updates.BarFraction < 0);
        await FinishRunning(updates);
    }

    [AvaloniaFact]
    public async Task WhatsNewFocusesUpdateNowInSettingsAndTheNoticeReturnsOnOtherScreens()
    {
        using var shell = new ShellHost();
        shell.Updater.Result = FakeUpdater.Available("9.0.0.0");
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 });
        await shell.Model.Updates.CheckAsync(false);
        host.Pump();
        var notice = host.Find<UpdateNoticeView>(_ => true)!;
        Assert.True(notice.IsVisible);

        // F6 reaches the notice, after the rail and the screen's regions
        var reached = false;
        for(var press = 0; press < 6 && !reached; press++)
        {
            host.Press(PhysicalKey.F6);
            reached = host.HasFocusWithin(notice);
        }

        Assert.True(reached);

        // "What's new" opens Settings and shows the notes with the keyboard on its update button.
        var whatsNew = host.Find<Button>(button => button.Name == "WhatsNewButton" && button.IsEffectivelyVisible)!;
        Assert.True(whatsNew.Focus(NavigationMethod.Tab));
        host.Press(PhysicalKey.Enter);
        Assert.Equal(Screen.Settings, shell.Model.CurrentScreen);
        Assert.True(shell.Model.Settings.ShowsNotes);
        Assert.False(notice.IsEffectivelyVisible);
        var updateNow = Assert.Single(host.FindAll<Button>(button => button.Name == "UpdateNowButton" && button.IsEffectivelyVisible));
        Assert.Same(updateNow, host.Window.FocusManager?.GetFocusedElement());

        host.Press(PhysicalKey.Digit3, RawInputModifiers.Control);
        Assert.Equal(Screen.Dashboard, shell.Model.CurrentScreen);
        Assert.True(notice.IsEffectivelyVisible);
        var rail = host.Find<ListBox>(list => list.Name == "Rail")!;
        Assert.True(host.HasFocusWithin(rail));

        Assert.True(whatsNew.Focus(NavigationMethod.Tab));
        host.Press(PhysicalKey.Digit6, RawInputModifiers.Control);
        Assert.False(notice.IsEffectivelyVisible);
        Assert.True(host.HasFocusWithin(rail));

        host.Press(PhysicalKey.Digit3, RawInputModifiers.Control);
        Assert.True(notice.IsEffectivelyVisible);

        // "Later" sends the version away: the notice leaves, and the same version does not come back by itself
        host.Find<Button>(button => button.Name == "LaterButton" && button.IsEffectivelyVisible)!.Command!.Execute(null);
        host.Pump();
        Assert.False(shell.Model.Updates.IsNoticeVisible);
        Assert.False(notice.IsVisible);
        await shell.Model.Updates.CheckAsync(false);
        host.Pump();
        Assert.False(notice.IsVisible);
    }

    [AvaloniaFact]
    public async Task DownloadDoesNotRestartTheArrivalAndANewerReleaseReplacesAnOldOutcome()
    {
        var motion = Motion.Reduced;
        Motion.Reduced = () => false;
        try
        {
            using var shell = new ShellHost();
            shell.Updater.Result = FakeUpdater.Available("9.0.0.0");
            shell.Updater.HoldInstall = new TaskCompletionSource();
            using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 });
            var updates = shell.Model.Updates;
            await updates.CheckAsync(false);
            host.Pump();
            var notice = host.Find<UpdateNoticeView>(_ => true)!;
            Assert.Equal(1, notice.Arrivals);

            // every percent of a download raises the notice's properties: it stays as it is, with no new arrival
            var install = updates.InstallAsync();
            await shell.Updater.InstallStarted.Task;
            foreach(var percent in new[] { 10, 40, 70 })
            {
                shell.Updater.LastProgress!.Report(new UpdateProgress { Stage = UpdateStage.Downloading, Percent = percent });
                host.Pump();
                Assert.Equal($"{percent}%", updates.PercentText);
            }

            Assert.Equal(1, notice.Arrivals);

            // an update that was cancelled, still shown, gives way to the newer version found since
            updates.CancelUpdateCommand.Execute(null);
            await install;
            Assert.Equal(UpdateNoticeState.Cancelled, updates.NoticeState);
            shell.Updater.Result = FakeUpdater.Available("9.1.0.0");
            await updates.CheckAsync(false);
            Assert.Equal(UpdateNoticeState.Available, updates.NoticeState);
            Assert.Equal("CommitFence 9.1.0.0 is available.", updates.Title);
        }
        finally
        {
            Motion.Reduced = motion;
        }
    }

    private static string ButtonLabel(UpdateButton button)
    {
        return button switch
        {
            UpdateButton.WhatsNew => "What's new",
            UpdateButton.OpenReleasePage => "Open the release page",
            UpdateButton.TryAgain => "Try again",
            UpdateButton.UpdateNow => "Update now",
            UpdateButton.Later => "Later",
            UpdateButton.Cancel => "Cancel",
            UpdateButton.Close => "Close",
            _ => throw new ArgumentOutOfRangeException(nameof(button))
        };
    }

    private Task? _running;

    /// <summary>
    /// Brings the model to a state of the notice's table through the fake updater, the way the real one would.
    /// </summary>
    private async Task Reach(UpdatesViewModel updates, UpdateNoticeState state)
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        _updater.Delivery = ProgressDelivery.Before;
        _updater.Problem = state == UpdateNoticeState.CannotUpdate ? "This copy is not a release download, so it cannot update itself." : null;
        updates.StartAndExit = _ => state == UpdateNoticeState.Installed;
        await updates.CheckAsync(false);
        switch(state)
        {
            case UpdateNoticeState.Available:
            case UpdateNoticeState.CannotUpdate:
                return;
            case UpdateNoticeState.Installed:
            case UpdateNoticeState.InstalledCouldNotRestart:
                await updates.InstallAsync();
                return;
            case UpdateNoticeState.Failed:
                _updater.InstallFailure = new UpdateException("The download is damaged. Nothing was changed.");
                await updates.InstallAsync();
                return;
        }

        // the states of an update that runs: it is held until the test lets it go
        _updater.HoldInstall = new TaskCompletionSource();
        _running = updates.InstallAsync();
        await _updater.InstallStarted.Task;
        Dispatcher.UIThread.RunJobs();
        if(state == UpdateNoticeState.Cancelled || state == UpdateNoticeState.Cancelling)
        {
            updates.CancelUpdateCommand.Execute(null);
            if(state == UpdateNoticeState.Cancelled)
            {
                await _running;
                _running = null;
            }

            return;
        }

        var report = state == UpdateNoticeState.Replacing ? new UpdateProgress { Stage = UpdateStage.Replacing } : new UpdateProgress { Stage = UpdateStage.Downloading, Percent = state == UpdateNoticeState.SizeNotKnown ? -1 : 64 };
        _updater.LastProgress!.Report(report);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Lets a held update go, so that nothing is left running when the test ends.
    /// </summary>
    private async Task FinishRunning(UpdatesViewModel updates)
    {
        if(_running == null)
            return;

        updates.CancelUpdateCommand.Execute(null);
        _updater.HoldInstall?.TrySetResult();
        await _running;
        _running = null;
    }

    private UpdatesViewModel Create()
    {
        return new UpdatesViewModel(_updater, _record, _clock, "/work/repo");
    }
}
