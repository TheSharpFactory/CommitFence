using CF.App.Updates;
using CF.Core.Updates;
using Xunit;

namespace CF.Tests;

/// <summary>
/// The app's updates: the check once a day and when asked, the notice with the release notes, Later, Update now with its
/// progress, Cancel and the restart, and where the app cannot update itself. A fake updater answers; nothing is replaced.
/// </summary>
public sealed class UpdatesViewModelTests
{
    private static readonly DateTimeOffset _now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeUpdater _updater = new FakeUpdater();
    private readonly MemoryCheckRecord _record = new MemoryCheckRecord();
    private readonly FixedClock _clock = new FixedClock(_now);

    private UpdatesViewModel Create()
    {
        return new UpdatesViewModel(_updater, _record, _clock, "/work/repo");
    }

    [Fact]
    public async Task ACheckTheUserAskedForSaysWhenThisIsTheLatestVersion()
    {
        var updates = Create();

        await updates.CheckAsync(true);

        Assert.Equal("You have the latest version (1.0.0.0).", updates.StatusText);
        Assert.True(updates.IsNoticeVisible);
        Assert.False(updates.CanUpdate);
        Assert.Equal(_now.UtcDateTime, _record.LastCheckUtc);
    }

    [Fact]
    public async Task ANewerReleaseShowsInTheNoticeWithItsNotes()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0", "The **best** version.\n\n### Fixes\n\n- One.\n\n### Downloads\n\n| a | b |");
        var updates = Create();

        await updates.CheckAsync(false);

        Assert.True(updates.IsNoticeVisible);
        Assert.Equal("CommitFence 9.0.0.0 is available. You have 1.0.0.0.", updates.Headline);
        Assert.True(updates.CanUpdate);
        Assert.True(updates.CanDismiss);
        Assert.True(updates.HasNotes);
        Assert.Equal(3, updates.NotesLines.Count);
        Assert.Equal("The best version.", updates.NotesLines[0].Text);
        Assert.Null(updates.StatusText);
    }

    [Fact]
    public async Task AFailedCheckSaysSoOnlyWhenTheUserAsked()
    {
        _updater.Result = new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Problem = "GitHub could not be reached." };
        var updates = Create();

        await updates.CheckAsync(false);
        Assert.Null(updates.StatusText);
        Assert.False(updates.IsNoticeVisible);

        await updates.CheckAsync(true);
        Assert.StartsWith("Could not check for updates.", updates.StatusText, StringComparison.Ordinal);

        // a failed check does not count as the day's check
        Assert.Null(_record.LastCheckUtc);
    }

    [Fact]
    public async Task TheAutomaticCheckRunsOnceADay()
    {
        var updates = Create();

        await updates.CheckIfDueAsync();
        await updates.CheckIfDueAsync();
        Assert.Equal(1, _updater.Checks);

        _clock.Now = _now.AddHours(23);
        await updates.CheckIfDueAsync();
        Assert.Equal(1, _updater.Checks);

        _clock.Now = _now.AddHours(25);
        await updates.CheckIfDueAsync();
        Assert.Equal(2, _updater.Checks);
    }

    [Fact]
    public async Task LaterSendsTheNoticeAwayAndAnAutomaticCheckDoesNotBringTheSameVersionBack()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        var updates = Create();
        await updates.CheckAsync(false);

        updates.DismissCommand.Execute(null);
        Assert.False(updates.IsNoticeVisible);

        await updates.CheckAsync(false);
        Assert.False(updates.IsNoticeVisible);

        // a newer one comes
        _updater.Result = FakeUpdater.Available("9.1.0.0");
        await updates.CheckAsync(false);
        Assert.True(updates.IsNoticeVisible);

        // and asking for a check brings back what was sent away
        updates.DismissCommand.Execute(null);
        await updates.CheckAsync(true);
        Assert.True(updates.IsNoticeVisible);
    }

    [Fact]
    public async Task UpdateNowInstallsAndRestartsWithTheFolderTheAppWorkedOn()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        var updates = Create();
        System.Diagnostics.ProcessStartInfo? started = null;
        updates.StartAndExit = info =>
        {
            started = info;
            return true;
        };
        await updates.CheckAsync(false);

        await updates.InstallAsync();

        Assert.Equal(1, _updater.Installs);
        Assert.NotNull(started);
        Assert.Equal("/work/repo", Assert.Single(started.ArgumentList));
        Assert.Equal("Version 9.0.0.0 is installed. Restarting…", updates.StatusText);

        // the files were replaced: Cancel is gone (the progress arrives through a post to the window's thread)
        await WaitUntilAsync(() => updates.IsReplacing);
        Assert.False(updates.CanCancel);
    }

    [Fact]
    public async Task WhenTheAppCannotRestartItselfItSaysSo()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        var updates = Create();
        updates.StartAndExit = _ => false;
        await updates.CheckAsync(false);

        await updates.InstallAsync();

        Assert.Equal("Version 9.0.0.0 is installed. Close the app and start it again to use it.", updates.StatusText);
        Assert.False(updates.IsInstalling);
    }

    [Fact]
    public async Task CancelStopsTheDownloadAndChangesNothing()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        _updater.HoldInstall = new TaskCompletionSource();
        var updates = Create();
        var restarted = false;
        updates.StartAndExit = _ =>
        {
            restarted = true;
            return true;
        };
        await updates.CheckAsync(false);

        var install = updates.InstallAsync();
        await WaitUntilAsync(() => updates.IsInstalling && _updater.Installs == 1);
        Assert.True(updates.CanCancel);
        Assert.False(updates.CanDismiss);

        updates.CancelUpdateCommand.Execute(null);
        await install;

        Assert.Equal("The update was cancelled. You still have version 1.0.0.0.", updates.StatusText);
        Assert.False(updates.IsInstalling);
        Assert.False(restarted);
        Assert.True(updates.CanUpdate);
    }

    [Fact]
    public async Task AFailedUpdateSaysWhyAndTheAppCarriesOn()
    {
        _updater.Result = FakeUpdater.Available("9.0.0.0");
        _updater.InstallFailure = new UpdateException("The download of version 9.0.0.0 is damaged. Nothing was changed.");
        var updates = Create();
        await updates.CheckAsync(false);

        await updates.InstallAsync();

        Assert.Contains("damaged", updates.StatusText, StringComparison.Ordinal);
        Assert.EndsWith("You still have version 1.0.0.0.", updates.StatusText, StringComparison.Ordinal);
        Assert.False(updates.IsInstalling);
        Assert.True(updates.IsIdle);
    }

    [Fact]
    public async Task WhereTheAppCannotUpdateItselfTheNoticeOffersTheReleasePage()
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
        await WaitUntilAsync(() => opened != null);
        Assert.Equal("https://github.com/TheSharpFactory/CommitFence/releases/tag/v9.0.0.0", opened?.ToString());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var waited = 0;
        while(!condition() && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        Assert.True(condition());
    }
}