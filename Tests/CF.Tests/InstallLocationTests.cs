using CF.Adapters.Updates;
using Xunit;

namespace CF.Tests;

/// <summary>
/// Where the running program was unpacked, which download that folder takes and where the update may put files, and how the program
/// is started again.
/// </summary>
public sealed class InstallLocationTests
{
    [Fact]
    public void ReleaseLocationSelectsSafeReplacementRoot()
    {
        using(var folder = new TestFolder())
        {
            var platform = OperatingSystem.IsWindows() ? AppPlatform.Windows : AppPlatform.Linux;
            var commandLine = Path.Combine(folder.Path, ReleaseFixtures.Program("commitfence"));
            var app = Path.Combine(folder.Path, ReleaseFixtures.Program("commitfenceapp"));
            File.WriteAllText(commandLine, "cli");

            // a command line of its own: its folder is the root, and it takes the command line's download
            var standalone = InstallLocation.Find(commandLine, platform);
            Assert.Null(standalone.Problem);
            Assert.Equal(folder.Path, standalone.Folder);
            Assert.Equal(Path.GetFileName(commandLine), standalone.RelativeExecutable);
            Assert.Equal(PackageKind.CommandLine, standalone.Kind);

            // the command line beside the app updates both, as the app does
            File.WriteAllText(app, "app");
            Assert.Equal(PackageKind.App, InstallLocation.Find(commandLine, platform).Kind);
            Assert.Equal(PackageKind.App, InstallLocation.Find(app, platform).Kind);

            // a build from the source (the program is a .dll beside it), a program that is neither, and no path: nothing is replaced
            File.WriteAllText(Path.Combine(folder.Path, "commitfence.dll"), "dll");
            Assert.Equal("This copy is not a release download, so it cannot update itself.", InstallLocation.Find(commandLine, platform).Problem);
            Assert.NotNull(InstallLocation.Find(Path.Combine(folder.Path, "Other.exe"), platform).Problem);
            Assert.NotNull(InstallLocation.Find(null, platform).Problem);
        }

        // paths with "/" work on every system, so the layouts of the other systems are checked everywhere
        Assert.Null(InstallLocation.Find("/apps/cf/commitfence.exe", AppPlatform.Windows).Problem);
        Assert.NotNull(InstallLocation.Find("/apps/cf/commitfence", AppPlatform.Windows).Problem);
        Assert.NotNull(InstallLocation.Find("/opt/cf/commitfence.exe", AppPlatform.Linux).Problem);

        // on macOS the whole bundle is replaced, from the folder that holds it, whichever of its two programs runs
        var bundle = InstallLocation.Find("/Applications/CommitFence.app/Contents/MacOS/commitfenceapp", AppPlatform.MacOS);
        Assert.Null(bundle.Problem);
        Assert.Equal(PackageKind.App, bundle.Kind);
        Assert.Equal("Applications", Path.GetFileName(bundle.Folder));
        Assert.Equal("CommitFence.app", Path.GetFileName(bundle.Bundle));
        Assert.Equal(Path.Combine("CommitFence.app", "Contents", "MacOS", "commitfenceapp"), bundle.RelativeExecutable);
        var inside = InstallLocation.Find("/Applications/CommitFence.app/Contents/MacOS/commitfence", AppPlatform.MacOS);
        Assert.Null(inside.Problem);
        Assert.Equal(PackageKind.App, inside.Kind);

        // the command line of its own on macOS is a plain file
        Assert.Equal(PackageKind.CommandLine, InstallLocation.Find("/usr/local/cf/commitfence", AppPlatform.MacOS).Kind);

        // a copy macOS runs from a temporary read-only place, and a bundle that is not CommitFence's, are not replaced
        Assert.NotNull(InstallLocation.Find("/private/var/folders/x/T/AppTranslocation/1234/d/CommitFence.app/Contents/MacOS/commitfenceapp", AppPlatform.MacOS).Problem);
        Assert.NotNull(InstallLocation.Find("/Applications/Other.app/Contents/MacOS/commitfenceapp", AppPlatform.MacOS).Problem);
    }

    [Fact]
    public void RestartTargetsInstalledAppAndRepository()
    {
        // a standalone program starts again as itself, with the folder it worked on as its one argument, spaces and all
        var location = new InstallLocation { Folder = "/opt/cf", Executable = "/opt/cf/commitfenceapp" };
        var withFolder = location.CreateStartInfo("/home/me/my project");
        Assert.Equal("/opt/cf/commitfenceapp", withFolder.FileName);
        Assert.Equal("/home/me/my project", Assert.Single(withFolder.ArgumentList));
        Assert.False(withFolder.UseShellExecute);
        Assert.Empty(location.CreateStartInfo(null).ArgumentList);

        // a macOS bundle is started through open, which hands the folder on to the app
        var bundle = InstallLocation.Find("/Applications/CommitFence.app/Contents/MacOS/commitfenceapp", AppPlatform.MacOS);
        var startInfo = bundle.CreateStartInfo("/Users/me/my project");
        Assert.Equal("open", startInfo.FileName);
        Assert.Equal(new[] { "-n", bundle.Bundle, "--args", "/Users/me/my project" }, startInfo.ArgumentList);
    }
}
