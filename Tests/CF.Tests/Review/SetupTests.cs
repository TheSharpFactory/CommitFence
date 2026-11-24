using CF.Adapters.Machine;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CF.Core.Setup;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// "commitfence install" and "commitfence init": what they would do and what they did, and what a repeated init leaves
/// of a team's own rules.
/// </summary>
public sealed class SetupTests
{
    [Fact]
    public void InstallPreviewAndApplyUseTheSameExecutableAndFolder()
    {
        using(var folder = new TestFolder())
        {
            var program = Path.Combine(folder.Path, "tools", "commitfence");
            var programFolder = Path.GetDirectoryName(program)!;
            var settings = new RecordingGitSettings();
            var userPath = new RecordingUserPath();
            var service = new SetupService(settings, userPath, null!, null!);

            var preview = service.Install(program, new InstallOptions(true, true));

            Assert.False(preview.Failed);
            Assert.Contains(preview.Lines, line => line.StartsWith("Would set", StringComparison.Ordinal) && line.Contains(HookLine.PathSetting, StringComparison.Ordinal) && line.Contains(program, StringComparison.Ordinal));
            Assert.Contains(preview.Lines, line => line.StartsWith("Would add", StringComparison.Ordinal) && line.Contains(programFolder, StringComparison.Ordinal));
            Assert.Empty(settings.Writes);
            Assert.Empty(userPath.Added);

            var applied = service.Install(program, new InstallOptions(false, true));

            Assert.False(applied.Failed);
            Assert.Equal(new[] { (HookLine.PathSetting, program) }, settings.Writes);
            Assert.Equal(new[] { programFolder }, userPath.Added);
        }
    }

    [Fact]
    public void InitCreatesStarterRulesAndPreservesTeamEditsOnRepeat()
    {
        using(var harness = new ReviewHarness())
        {
            var files = new SystemFileSystem();
            var service = new SetupService(new RecordingGitSettings(), new RecordingUserPath(), harness.Fake, files);
            var rulesPath = Path.Combine(harness.RepositoryFolder, "commitfence.json");

            var first = service.Init(harness.RepositoryFolder);

            Assert.False(first.Failed);
            Assert.Contains(HookLine.PrePush, first.Lines);
            var starter = new SettingsFile(files, new SettingsReader(harness.Languages)).Read(rulesPath);
            Assert.Empty(starter.ActiveRules);

            var teamRules = ReviewSources.Settings(ReviewSources.Team010);
            File.WriteAllText(rulesPath, teamRules);
            var second = service.Init(harness.RepositoryFolder);

            Assert.False(second.Failed);
            Assert.Equal(teamRules, File.ReadAllText(rulesPath));
        }
    }
}
