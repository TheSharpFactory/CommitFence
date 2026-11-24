using CF.Adapters.Machine;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Languages.CSharp;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// What a whole review of a folder reports: only findings in the files it selected, a build failure wherever it lies, a
/// file no project compiles, a prerequisite that does not build, and the settings it was told to use.
/// </summary>
public sealed class WholeFindingsTests
{
    private const string _structure = """
        "structure": {
            "modules": { "high": ["namespace:Demo.High"], "low": ["namespace:Demo.Low"] },
            "directions": { "allow": ["high -> low"] }
        }
        """;

    [Fact]
    public void ReachFilterPreservesBuildFailuresOutsideSourceScope()
    {
        var changed = new List<ChangedFile> { new ChangedFile("src/In.cs", "csharp", ChangeStatus.Added, new List<LineRange> { LineRange.ForWholeFile() }) };
        var linkedCompileError = new Finding("CS0117", FindingSource.Compiler, Severity.Block, "csharp", "App", "net11.0", "shared/Linked.cs", 3, 5, "a member is missing", null, null);
        var oldRuleBreach = new Finding("TEAM010", FindingSource.CodeRule, Severity.Block, "csharp", "App", "net11.0", "shared/Linked.cs", 7, 1, "braces", null, null);
        var projectDiagnostic = new Finding("MSB3277", FindingSource.Compiler, Severity.Warn, "csharp", "App", "net11.0", null, null, null, "a version conflict", null, null);

        var kept = ReachFilter.Keep(new[] { linkedCompileError, oldRuleBreach, projectDiagnostic }, changed);

        Assert.Equal(new[] { linkedCompileError, projectDiagnostic }, kept);
    }

    [Fact]
    public void WholeReviewReportsOnlyFindingsInSelectedFiles()
    {
        var helper = ReviewSources.Class("Helper", ReviewSources.SignBad);
        var other = ReviewSources.Class("Other", ReviewSources.SignBad);
        using(var harness = new ReviewHarness())
        {
            var settings = "{ \"rules\": [" + ReviewSources.Team010 + "], " + _structure + " }\n";
            harness.Fake.AddCommit("head", new Dictionary<string, string>
            {
                ["commitfence.json"] = settings,
                ["src/App/Util/Helper.cs"] = helper,
                ["src/App/Other.cs"] = other,
                ["src/Clean/Fine.cs"] = ReviewSources.Class("Fine", ReviewSources.SignOk)
            });
            harness.Fake.Name("HEAD", "head");
            var answers = CreateAnswers("src/App/Util/Helper.cs", "src/App/Other.cs");
            var toolchain = new FakeToolchain { Answer = request => new ToolchainResult(CreateAnalyzerFindings("src/App/Util/Helper.cs", "src/App/Other.cs"), new List<Coverage>(), new List<string>(), answers, new List<FileReading>()) };
            harness.Languages = new List<ILanguage> { new TestLanguage(toolchain, new FakeMeaningSource(answers)) };

            var nested = Review(harness, "src/App/Util");
            var repository = Review(harness, string.Empty);
            var clean = Review(harness, "src/Clean");

            Assert.Equal(Expected("src/App/Util/Helper.cs", helper), Tuples(nested));
            Assert.Equal(ExitCode.Blocked, nested.ExitCode);
            Assert.Equal(new[] { new RuleCount("ARCH001", 1), new RuleCount("IDE0044", 1), new RuleCount("TEAM010", 1) }, nested.CountByRule());

            Assert.Equal(Expected("src/App/Other.cs", other).Concat(Expected("src/App/Util/Helper.cs", helper)).ToList(), Tuples(repository));
            Assert.Equal(new[] { new RuleCount("ARCH001", 2), new RuleCount("IDE0044", 2), new RuleCount("TEAM010", 2) }, repository.CountByRule());

            Assert.Empty(clean.Findings);
            Assert.Equal(ExitCode.Passed, clean.ExitCode);
        }
    }

    [Fact]
    public void LooseFileCanBlockAnIncompleteReview()
    {
        var loose = ReviewSources.Class("Loose", ReviewSources.SignBad);
        var runner = new ScriptedProcessRunner();
        using(var harness = new ReviewHarness())
        {
            harness.Languages = new List<ILanguage> { new CSharpLanguage(runner, new SystemFileSystem(), new FolderOfTest(Path.Combine(harness.Scratch, "data"))) };
            harness.Fake.AddCommit("head", new Dictionary<string, string> { ["commitfence.json"] = ReviewSources.Settings(ReviewSources.Team010), ["Loose.cs"] = loose });
            harness.Fake.Name("HEAD", "head");

            var result = Review(harness, string.Empty);

            Assert.Equal(ExitCode.Blocked, result.ExitCode);
            var finding = Assert.Single(result.Findings);
            Assert.Equal(("Loose.cs", "TEAM010", ReviewSources.LineOf(loose, "if(x > 0)")), (finding.File, finding.RuleId, finding.Line));
            var gap = Assert.Single(result.Coverage, entry => entry.Status == CoverageStatus.Failed);
            Assert.Equal("Loose.cs", gap.What);
            Assert.Contains("no project compiles this file", gap.Reason, StringComparison.Ordinal);
            Assert.Empty(runner.Requests);
        }
    }

    [Theory]
    [InlineData(false, TestDisplayName = "a prerequisite of the selected project does not build")]
    [InlineData(true, TestDisplayName = "the selected project cannot be read")]
    public void FailedPrerequisiteMakesSelectedProjectIncomplete(bool selectedProjectCannotBeRead)
    {
        var runner = ScriptedDotnet.Create(selectedProjectCannotBeRead);
        using(var harness = new ReviewHarness())
        {
            harness.Languages = new List<ILanguage> { new CSharpLanguage(runner, new SystemFileSystem(), new FolderOfTest(Path.Combine(harness.Scratch, "data"))) };
            harness.Fake.AddCommit("head", new Dictionary<string, string>
            {
                ["commitfence.json"] = ReviewSources.Settings(ReviewSources.Team010),
                ["App/App.csproj"] = ReviewSources.ProjectFile,
                ["App/Program.cs"] = ReviewSources.Class("Program", ReviewSources.Add),
                ["Lib/Lib.csproj"] = ReviewSources.ProjectFile,
                ["Lib/Lib.cs"] = ReviewSources.Class("Lib", ReviewSources.SignBad)
            });
            harness.Fake.Name("HEAD", "head");

            var result = Review(harness, "App");

            Assert.Equal(ExitCode.Incomplete, result.ExitCode);
            Assert.DoesNotContain(result.Findings, finding => finding.Severity == Severity.Block);
            Assert.DoesNotContain(result.Findings, finding => finding.File != null && finding.File.StartsWith("Lib/", StringComparison.Ordinal));
            var builds = runner.Requests.Where(request => request.Arguments.Contains("-t:Build")).ToList();
            if(selectedProjectCannotBeRead)
            {
                Assert.Empty(builds);
                Assert.Contains(result.Coverage, entry => entry.What == "App" && entry.Status == CoverageStatus.Failed && entry.Reason.Contains("cannot evaluate", StringComparison.Ordinal));
                return;
            }

            Assert.EndsWith("Lib.csproj", Assert.Single(builds).Arguments[1], StringComparison.Ordinal);
            Assert.Contains(result.Coverage, entry => entry.What.StartsWith("Lib", StringComparison.Ordinal) && entry.Status == CoverageStatus.Failed && entry.Reason.Contains("does not build", StringComparison.Ordinal));
            Assert.Contains(result.Coverage, entry => entry.What.StartsWith("App", StringComparison.Ordinal) && entry.Status == CoverageStatus.Failed && entry.Reason.Contains("prerequisite Lib", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void WholeReviewUsesTheRequestedSettingsSource()
    {
        var source = ReviewSources.Class("Core", ReviewSources.SignBad, "public int Run()\n{\n    return 1;\n}");
        var committed = ReviewSources.Settings(ReviewSources.Team010);

        using(var harness = new ReviewHarness())
        {
            var external = Path.Combine(harness.Scratch, "external.json");
            File.WriteAllText(external, ReviewSources.Settings(ReviewSources.NoRun));
            AddHead(harness, committed, source);

            var outcome = harness.Run("review", "--all", "--settings", external);

            Assert.Equal(ExitCode.Blocked, outcome.ExitCode);
            Assert.Contains("error EXT010", outcome.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("TEAM010", outcome.Text, StringComparison.Ordinal);
        }

        using(var harness = new ReviewHarness())
        {
            var toolchain = new FakeToolchain();
            harness.Languages = new List<ILanguage> { new TestLanguage(toolchain) };
            var external = Path.Combine(harness.Scratch, "broken.json");
            File.WriteAllText(external, "{ \"rules\": [ \n");
            AddHead(harness, committed, source);

            var outcome = harness.Run("review", "--all", "--settings", external);

            Assert.Equal(ExitCode.BadSettingsOrUsage, outcome.ExitCode);
            Assert.Contains(external, outcome.Text, StringComparison.Ordinal);
            Assert.Empty(toolchain.Requests);
        }

        using(var harness = new ReviewHarness())
        {
            var toolchain = new FakeToolchain();
            harness.Languages = new List<ILanguage> { new TestLanguage(toolchain) };
            AddHead(harness, "{ \"rules\": [ \n", source);

            var outcome = harness.Run("review", "--all");

            Assert.Equal(ExitCode.BadSettingsOrUsage, outcome.ExitCode);
            Assert.Contains("commitfence.json (in the tree under review)", outcome.Text, StringComparison.Ordinal);
            Assert.Empty(toolchain.Requests);
        }
    }

    private static void AddHead(ReviewHarness harness, string settings, string source)
    {
        harness.Fake.AddCommit("head", new Dictionary<string, string> { ["commitfence.json"] = settings, ["src/Core.cs"] = source });
        harness.Fake.Name("HEAD", "head");
    }

    private static ReviewResult Review(ReviewHarness harness, string target)
    {
        var path = target.Length == 0 ? string.Empty : Path.Combine(harness.RepositoryFolder, target);
        return harness.Service().ReviewChanges(harness.RepositoryFolder, ReviewScope.ForWholeFolder(path), null);
    }

    private static List<(string? File, int? Line, string RuleId, Severity Severity)> Tuples(ReviewResult result)
    {
        return result.Findings.Select(finding => (finding.File, finding.Line, finding.RuleId, finding.Severity)).OrderBy(tuple => tuple.File, StringComparer.Ordinal).ThenBy(tuple => tuple.Line).ThenBy(tuple => tuple.RuleId, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// What the review has for one file: the syntax rule at its line, the warning of the build at line 3, and the layer
    /// direction at line 8.
    /// </summary>
    private static List<(string? File, int? Line, string RuleId, Severity Severity)> Expected(string file, string source)
    {
        return new List<(string? File, int? Line, string RuleId, Severity Severity)>
        {
            (file, 3, "IDE0044", Severity.Warn),
            (file, 8, "ARCH001", Severity.Block),
            (file, ReviewSources.LineOf(source, "if(x > 0)"), "TEAM010", Severity.Block)
        }.OrderBy(tuple => tuple.File, StringComparer.Ordinal).ThenBy(tuple => tuple.Line).ThenBy(tuple => tuple.RuleId, StringComparer.Ordinal).ToList();
    }

    private static List<Finding> CreateAnalyzerFindings(params string[] files)
    {
        return files.Select(file => new Finding("IDE0044", FindingSource.EditorConfig, Severity.Warn, "csharp", "Demo", "net11.0", file, 3, 1, "make it readonly", null, ArtifactReach.OfSpan(new SourceSpan(3, 1, 3, 10)))).ToList();
    }

    private static MeaningAnswers CreateAnswers(params string[] files)
    {
        var uses = files.Select(file => new MeaningUse(file, "Sign", "Demo.Low", "Demo", "Demo.High.Thing.Do", "Demo.High.Thing", "Demo.High", "Demo", "src/High/Thing.cs", new List<SourcePoint> { new SourcePoint(8, 13) })).ToList();
        return new MeaningAnswers("csharp", new List<MeaningDeclaration>(), uses, new List<string>(), new List<string>(), new List<string>());
    }
}
