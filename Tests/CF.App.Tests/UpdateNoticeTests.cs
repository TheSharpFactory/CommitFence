using System.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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
            Assert.Equal("You have the latest version (1.0.0.0).", manual.StatusText);
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
                Assert.Equal("CommitFence 9.0.0.0 is available. You have 1.0.0.0.", updates.Headline);
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
        Assert.Equal("Version 9.0.0.0 is installed. Restarting…", updates.StatusText);

        // the files were replaced: Cancel is gone
        Assert.True(updates.IsReplacing);
        Assert.False(updates.CanCancel);

        // an app that cannot start itself again says so, and stays usable
        var other = Create();
        _updater.Delivery = ProgressDelivery.Never;
        other.StartAndExit = _ => false;
        await other.CheckAsync(false);
        await other.InstallAsync();
        Assert.Equal("Version 9.0.0.0 is installed. Close the app and start it again to use it.", other.StatusText);
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

            Assert.Equal("The update was cancelled. You still have version 1.0.0.0.", updates.StatusText);
        }
        else
        {
            _updater.InstallFailure = new UpdateException("The download of version 9.0.0.0 is damaged. Nothing was changed.");

            await updates.InstallAsync();

            Assert.Contains("damaged", updates.StatusText, StringComparison.Ordinal);
            Assert.EndsWith("You still have version 1.0.0.0.", updates.StatusText, StringComparison.Ordinal);
        }

        // reports that were held back reach the app after the install has ended: they change nothing that is shown
        Dispatcher.UIThread.RunJobs();
        var shown = updates.StatusText;
        var percent = updates.DownloadPercent;
        _updater.DeliverHeld();
        _updater.LastProgress!.Report(new UpdateProgress { Stage = UpdateStage.Downloading, Percent = 90 });
        _updater.LastProgress.Report(new UpdateProgress { Stage = UpdateStage.Replacing });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(shown, updates.StatusText);
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
        _updater.Problem = "This copy of the program is not a release download, so it cannot update itself.";
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
        Assert.StartsWith("This copy of the program is not a release download", updates.ProblemText, StringComparison.Ordinal);

        // nothing is downloaded
        await updates.InstallAsync();
        Assert.Equal(0, _updater.Installs);

        updates.OpenReleasePageCommand.Execute(null);
        Assert.Equal("https://github.com/TheSharpFactory/CommitFence/releases/tag/v9.0.0.0", opened?.ToString());
    }

    private UpdatesViewModel Create()
    {
        return new UpdatesViewModel(_updater, _record, _clock, "/work/repo");
    }
}
