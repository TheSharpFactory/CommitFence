using CF.Cli.Commands;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Review;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// "commitfence review --all": which folder or project file it takes for the target, which files of the last commit that
/// makes, what makes a target wrong, what the arguments may and may not hold, and when it says that work is not
/// committed.
/// </summary>
public sealed class WholeTargetTests
{
    private static readonly Dictionary<string, string> _headFiles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["README.md"] = "readme\n",
        ["src/App/App.csproj"] = ReviewSources.ProjectFile,
        ["src/App/Program.cs"] = ReviewSources.Class("Program", ReviewSources.Add),
        ["src/App/Util/Helper.cs"] = ReviewSources.Class("Helper", ReviewSources.Twice),
        ["src/Lib/Lib.csproj"] = ReviewSources.ProjectFile,
        ["src/Lib/Lib.cs"] = ReviewSources.Class("Lib", ReviewSources.Add)
    };

    [Theory]
    [InlineData("src/App", "", "README.md|src/App/App.csproj|src/App/Program.cs|src/App/Util/Helper.cs|src/Lib/Lib.cs|src/Lib/Lib.csproj", TestDisplayName = "an omitted target, from a subdirectory, takes every tracked file")]
    [InlineData("src/App", "../Lib", "src/Lib/Lib.cs|src/Lib/Lib.csproj", TestDisplayName = "a relative sibling folder")]
    [InlineData("", "src/App/app.CSPROJ", "src/App/App.csproj|src/App/Program.cs|src/App/Util/Helper.cs", TestDisplayName = "a project file written in other letters")]
    [InlineData("", "src/App/Util", "src/App/Util/Helper.cs", TestDisplayName = "a nested folder")]
    public void WholeTargetSelectsCommittedFiles(string currentFolder, string target, string expectedPaths)
    {
        using(var harness = new ReviewHarness())
        {
            harness.Fake.AddCommit("head", _headFiles);
            harness.Fake.Name("HEAD", "head");
            harness.Fake.ForbidDiffs = true;
            var languages = new List<ILanguage> { new TestLanguage(new FakeToolchain()) };
            var targetPath = target.Length == 0 ? string.Empty : Path.GetFullPath(target, Path.Combine(harness.RepositoryFolder, currentFolder));

            var changeSet = Assert.Single(new ChangeResolver(harness.Fake, languages).Resolve(harness.Fake.Repository, ReviewScope.ForWholeFolder(targetPath)));

            Assert.Equal(expectedPaths.Split('|'), changeSet.Files.Select(file => file.Path).ToArray());
            Assert.True(changeSet.IsWholeReview);
            Assert.All(changeSet.Files, file => Assert.Equal(ChangeStatus.Added, file.Status));
            Assert.All(changeSet.Files, file => Assert.True(Assert.Single(file.ChangedLines).IsWholeFile));
            Assert.Empty(harness.Fake.Diffs);
        }
    }

    [Theory]
    [InlineData("OutsideTheRepository", ExitCode.BadSettingsOrUsage)]
    [InlineData("AbsentFromTheCommit", ExitCode.BadSettingsOrUsage)]
    [InlineData("TrackedSourceFile", ExitCode.BadSettingsOrUsage)]
    [InlineData("AmbiguousLetterCase", ExitCode.BadSettingsOrUsage)]
    [InlineData("RepositoryWithoutACommit", ExitCode.Incomplete)]
    public void InvalidWholeTargetCannotStartAReview(string scenario, ExitCode expectedExit)
    {
        var toolchain = new FakeToolchain();
        using(var harness = new ReviewHarness())
        {
            harness.Languages = new List<ILanguage> { new TestLanguage(toolchain) };
            var files = new Dictionary<string, string>(_headFiles, StringComparer.Ordinal) { ["src/lib/other.cs"] = ReviewSources.Class("Other", ReviewSources.Add), ["src/Lib/Lib.cs"] = ReviewSources.Class("Lib", ReviewSources.Add) };
            if(scenario != "RepositoryWithoutACommit")
            {
                harness.Fake.AddCommit("head", files);
                harness.Fake.Name("HEAD", "head");
            }

            var target = scenario switch
            {
                "OutsideTheRepository" => Path.Combine(harness.Scratch, "elsewhere"),
                "AbsentFromTheCommit" => "src/Missing",
                "TrackedSourceFile" => "src/App/Program.cs",
                "AmbiguousLetterCase" => "src/LIB",
                _ => string.Empty
            };

            var outcome = harness.Run(new[] { "review", "--all", target }.Where(argument => argument.Length > 0).ToArray());

            Assert.Equal(expectedExit, outcome.ExitCode);
            Assert.Empty(toolchain.Requests);
            Assert.Empty(harness.FakeSnapshotStore!.Moves);
            if(scenario == "AmbiguousLetterCase")
            {
                Assert.Contains("'src/Lib'", outcome.Text, StringComparison.Ordinal);
                Assert.Contains("'src/lib'", outcome.Text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void WholeArgumentsBindOneTargetAndRejectConflicts()
    {
        var valid = new (string Arguments, string Target, string? Settings)[]
        {
            ("review --all", string.Empty, null),
            ("review --all src/App", "src/App", null),
            ("review src/App --all", "src/App", null),
            ("review --all --settings rules.json src/App", "src/App", "rules.json"),
            ("review src/App --settings rules.json --all", "src/App", "rules.json")
        };
        foreach(var row in valid)
        {
            var result = ReviewArguments.Parse(row.Arguments.Split(' '));

            var arguments = result.Arguments;
            Assert.True(arguments != null, row.Arguments + ": " + result.Error);
            Assert.Equal(ReviewMode.All, arguments!.Mode);
            Assert.Equal(row.Target, arguments.Target);
            Assert.Equal(row.Settings, arguments.SettingsPath);
        }

        var invalid = new[]
        {
            "review --all --hook pre-push origin https://example.invalid/origin",
            "review --all --staged",
            "review --all --base main",
            "review --all --head main",
            "review --all --files a.cs",
            "review --all src other",
            "review --all --all",
            "review --all --settings"
        };
        foreach(var row in invalid)
        {
            var result = ReviewArguments.Parse(row.Split(' '));

            Assert.True(result.Arguments == null, row + " was accepted");
            Assert.NotEmpty(result.Error);
        }
    }

    [Theory]
    [InlineData(null, false, TestDisplayName = "nothing is uncommitted")]
    [InlineData("docs/guide.md", false, TestDisplayName = "only a file outside the folder is edited")]
    [InlineData("src/App/Program.cs", true, TestDisplayName = "a file inside the folder is edited")]
    public void DirtyWorkNoticeIsLimitedToTheSelectedFolder(string? editedPath, bool expectedNotice)
    {
        using(var harness = new ReviewHarness())
        {
            harness.Fake.AddCommit("head", _headFiles);
            harness.Fake.Name("HEAD", "head");
            harness.Fake.HasWork = folder => editedPath != null && (folder.Length == 0 || editedPath.StartsWith(folder + "/", StringComparison.Ordinal));
            var languages = new List<ILanguage> { new TestLanguage(new FakeToolchain()) };
            var repository = harness.Fake.Repository;
            var resolver = new WholeFolderResolver(harness.Fake, repository, new ChangeSetBuilder(harness.Fake, repository, languages), languages);

            var changeSet = resolver.Resolve(Path.Combine(repository.WorkFolder, "src", "App"));

            Assert.Equal(expectedNotice, changeSet.HasUncommittedWork);
            Assert.Equal("src/App", Assert.Single(harness.Fake.UncommittedChecks));
        }
    }
}
