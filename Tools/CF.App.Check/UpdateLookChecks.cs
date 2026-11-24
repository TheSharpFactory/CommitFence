using System.Diagnostics;
using Avalonia;
using Avalonia.Styling;
using CF.App.Updates;
using CF.Composition;
using CF.Core.Ports;
using CF.Core.Updates;

namespace CF.App.Check;

/// <summary>
/// The pictures of the update notice in the real main window, on the Rules screen of a sample folder, light and dark: a newer
/// version with its release notes, an update downloading, and a copy that cannot update itself. An updater that answers
/// from memory stands in for the real one, so nothing touches the network or a file.
/// </summary>
internal sealed class UpdateLookChecks
{
    private const string _notes = "### What is new\n\n"
        + "- The app and the command line now update themselves from the latest release.\n"
        + "- **Update now** shows the progress of the download, and Cancel stops it before any file is replaced.\n"
        + "- A review that is running holds the update back until it ends.\n"
        + "- The release notes are shown here before anything is downloaded.\n\n"
        + "### Fixes\n\n"
        + "- A review of a push with a rename no longer reports the old name as removed.\n"
        + "- The count by rule keeps its order when a rule is switched off.\n"
        + "- A settings file with a comment at its end is read again.\n"
        + "- The repository button shows the branch when the folder is a worktree.\n"
        + "- Pressing Escape closes the effect menu and keeps the row selected.\n\n"
        + "### Downloads\n\n| Download | What |\n|---|---|\n";

    private readonly CheckContext _context;

    public UpdateLookChecks(CheckContext context)
    {
        _context = context;
    }

    public void Run()
    {
        _context.Guarded("update-look", "the update notice is saved in three states, light and dark, at 1440 by 900", SavePictures);
    }

    private void SavePictures()
    {
        var saved = new List<string>();
        SaveState(saved, "available", new PictureUpdater(null, false));
        SaveState(saved, "installing", new PictureUpdater(null, true));
        SaveState(saved, "cannot", new PictureUpdater("This copy of the program is not a release download, so it cannot update itself.", false));

        var wrong = saved.Where(path => !File.Exists(path) || LookChecks.PngSize(path) != (1440, 900)).ToArray();
        _context.Report.Check("update-look", $"{saved.Count} pictures saved in {_context.Options.PicturesFolder}, each 1440 by 900", saved.Count == 6 && wrong.Length == 0, wrong);
    }

    private void SaveState(List<string> saved, string state, PictureUpdater updater)
    {
        var real = _context.Services;
        var services = new CommitFenceServices(real.ProductInfo, real.Clock, real.Languages, real.Rules, real.Review, real.Setup, real.Folders, updater, new NeverChecked());
        using var app = new AppWindow(services, _context.Folders.NewFolder(), 1440, 900);
        app.PumpUntil(() => app.Model.Updates.HasUpdate, 10000);
        if(updater.HoldsInstall)
        {
            app.Model.Updates.UpdateNowCommand.Execute(null);
            app.PumpUntil(() => app.Model.Updates.DownloadPercent == 50, 10000);
        }

        app.MovePointerAway();
        saved.Add(app.Save(_context.Options.PicturesFolder, $"update-{state}-light.png"));
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        app.Pump(500);
        saved.Add(app.Save(_context.Options.PicturesFolder, $"update-{state}-dark.png"));
        Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        app.Pump(300);
        if(updater.HoldsInstall)
        {
            app.Model.Updates.CancelUpdateCommand.Execute(null);
            app.Pump(300);
        }
    }

    /// <summary>
    /// An updater that finds version 9.0.0.0 at once and, when asked to install, stops half way through the download.
    /// </summary>
    private sealed class PictureUpdater : IUpdater
    {
        private readonly string? _problem;

        public PictureUpdater(string? problem, bool holdsInstall)
        {
            _problem = problem;
            HoldsInstall = holdsInstall;
        }

        public bool HoldsInstall { get; }

        public Version CurrentVersion
        {
            get { return new Version(1, 0, 0, 0); }
        }

        public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            var release = new ReleaseInfo
            {
                Version = new Version(9, 0, 0, 0),
                Notes = _notes,
                PageUri = new Uri("https://github.com/TheSharpFactory/CommitFence/releases/tag/v9.0.0.0"),
                PackageName = "CommitFence-win-x64.zip",
                PackageUri = new Uri("https://example.com/CommitFence-win-x64.zip"),
                ChecksumsUri = new Uri("https://example.com/SHA256SUMS.txt"),
                SignatureUri = new Uri("https://example.com/SHA256SUMS.txt.sig")
            };

            return Task.FromResult(new UpdateCheckResult { Status = UpdateCheckStatus.Available, Release = release });
        }

        public string? FindProblem(ReleaseInfo release)
        {
            return _problem;
        }

        public async Task InstallAsync(ReleaseInfo release, IProgress<UpdateProgress>? progress, UpdateCancellation? cancellation)
        {
            progress?.Report(new UpdateProgress { Stage = UpdateStage.Downloading, Percent = 50 });
            await Task.Delay(Timeout.Infinite, cancellation?.Token ?? CancellationToken.None);
        }

        public Task CleanUpAsync(int attempts)
        {
            return Task.CompletedTask;
        }

        public ProcessStartInfo CreateStartInfo(string? argument)
        {
            return new ProcessStartInfo("commitfenceapp");
        }
    }

    /// <summary>
    /// A record that says no check was ever made, and keeps nothing.
    /// </summary>
    private sealed class NeverChecked : IUpdateCheckRecord
    {
        public DateTime? LastCheckUtc
        {
            get { return null; }
        }

        public void Save(DateTime checkedUtc)
        {
        }
    }
}