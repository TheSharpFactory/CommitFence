using CF.Adapters.Updates;
using Xunit;

namespace CF.Tests;

/// <summary>
/// Where the running program was unpacked, which download that folder takes, and how the program is started again.
/// </summary>
public sealed class InstallLocationTests
{
    [Fact]
    public void AReleaseDownloadCanUpdateItselfButABuildFromTheSourceCannot()
    {
        using(var folder = new TestFolder())
        {
            var platform = OperatingSystem.IsWindows() ? AppPlatform.Windows : AppPlatform.Linux;
            var program = Path.Combine(folder.Path, ReleaseFixtures.Program("commitfence"));
            File.WriteAllText(program, "cli");

            var release = InstallLocation.Find(program, platform);
            Assert.Null(release.Problem);
            Assert.Equal(folder.Path, release.Folder);
            Assert.Equal(Path.GetFileName(program), release.RelativeExecutable);

            File.WriteAllText(Path.Combine(folder.Path, "commitfence.dll"), "dll");
            Assert.NotNull(InstallLocation.Find(program, platform).Problem);
            Assert.NotNull(InstallLocation.Find(Path.Combine(folder.Path, "Other.exe"), platform).Problem);
            Assert.NotNull(InstallLocation.Find(null, platform).Problem);
        }
    }

    [Fact]
    public void AFolderThatHoldsTheAppTakesTheFullDownloadAndOneWithOnlyTheCommandLineTakesTheOtherOne()
    {
        using(var folder = new TestFolder())
        {
            var platform = OperatingSystem.IsWindows() ? AppPlatform.Windows : AppPlatform.Linux;
            var commandLine = Path.Combine(folder.Path, ReleaseFixtures.Program("commitfence"));
            var app = Path.Combine(folder.Path, ReleaseFixtures.Program("commitfenceapp"));
            File.WriteAllText(commandLine, "cli");

            Assert.Equal(PackageKind.CommandLine, InstallLocation.Find(commandLine, platform).Kind);

            File.WriteAllText(app, "app");

            // the command line beside the app updates both, as the app does
            Assert.Equal(PackageKind.App, InstallLocation.Find(commandLine, platform).Kind);
            Assert.Equal(PackageKind.App, InstallLocation.Find(app, platform).Kind);
        }
    }

    [Fact]
    public void EachSystemHasItsOwnProgramName()
    {
        // paths with "/" work on every system, so all layouts are checked everywhere
        var windows = InstallLocation.Find("/apps/cf/commitfence.exe", AppPlatform.Windows);
        Assert.Null(windows.Problem);
        Assert.Equal("commitfence.exe", windows.RelativeExecutable);
        Assert.Equal(PackageKind.CommandLine, windows.Kind);
        Assert.Equal(PackageKind.App, InstallLocation.Find("/apps/cf/commitfenceapp.exe", AppPlatform.Windows).Kind);
        Assert.NotNull(InstallLocation.Find("/apps/cf/commitfence", AppPlatform.Windows).Problem);

        var linux = InstallLocation.Find("/opt/cf/commitfence", AppPlatform.Linux);
        Assert.Null(linux.Problem);
        Assert.Equal("commitfence", linux.RelativeExecutable);
        Assert.NotNull(InstallLocation.Find("/opt/cf/commitfence.exe", AppPlatform.Linux).Problem);
        Assert.NotNull(InstallLocation.Find("/opt/cf/other", AppPlatform.Linux).Problem);

        Assert.NotNull(InstallLocation.Find("/opt/cf/commitfenceapp", AppPlatform.MacOS).Problem);
    }

    [Fact]
    public void OnMacOSTheWholeAppBundleIsUpdatedUnlessMacOSRunsATemporaryCopy()
    {
        var app = InstallLocation.Find("/Applications/CommitFence.app/Contents/MacOS/commitfenceapp", AppPlatform.MacOS);
        Assert.Null(app.Problem);
        Assert.Equal(PackageKind.App, app.Kind);
        Assert.Equal("Applications", Path.GetFileName(app.Folder));
        Assert.Equal("CommitFence.app", Path.GetFileName(app.Bundle));
        Assert.Equal(Path.Combine("CommitFence.app", "Contents", "MacOS", "commitfenceapp"), app.RelativeExecutable);

        // the command line inside the bundle belongs to the app's download
        var inside = InstallLocation.Find("/Applications/CommitFence.app/Contents/MacOS/commitfence", AppPlatform.MacOS);
        Assert.Null(inside.Problem);
        Assert.Equal(PackageKind.App, inside.Kind);
        Assert.Equal(Path.Combine("CommitFence.app", "Contents", "MacOS", "commitfence"), inside.RelativeExecutable);

        // the command line of its own (the tar.gz) is a plain file
        var alone = InstallLocation.Find("/usr/local/cf/commitfence", AppPlatform.MacOS);
        Assert.Null(alone.Problem);
        Assert.Equal(PackageKind.CommandLine, alone.Kind);
        Assert.Equal("commitfence", alone.RelativeExecutable);

        Assert.NotNull(InstallLocation.Find("/private/var/folders/x/T/AppTranslocation/1234/d/CommitFence.app/Contents/MacOS/commitfenceapp", AppPlatform.MacOS).Problem);
        Assert.NotNull(InstallLocation.Find("/Applications/Other.app/Contents/MacOS/commitfenceapp", AppPlatform.MacOS).Problem);
    }

    [Fact]
    public void TheAppStartsAgainWithTheFolderItWorkedOn()
    {
        var location = new InstallLocation { Folder = "/opt/cf", Executable = "/opt/cf/commitfenceapp" };

        var withFolder = location.CreateStartInfo("/home/me/project");
        Assert.Equal("/opt/cf/commitfenceapp", withFolder.FileName);
        Assert.Equal("/home/me/project", Assert.Single(withFolder.ArgumentList));
        Assert.False(withFolder.UseShellExecute);
        Assert.Empty(location.CreateStartInfo(null).ArgumentList);
    }

    [Fact]
    public void OnMacOSTheBundleIsStartedThroughOpen()
    {
        var bundle = InstallLocation.Find("/Applications/CommitFence.app/Contents/MacOS/commitfenceapp", AppPlatform.MacOS);

        var startInfo = bundle.CreateStartInfo("/Users/me/project");

        Assert.Equal("open", startInfo.FileName);
        Assert.Equal($"-n|{bundle.Bundle}|--args|/Users/me/project", string.Join("|", startInfo.ArgumentList));
    }
}