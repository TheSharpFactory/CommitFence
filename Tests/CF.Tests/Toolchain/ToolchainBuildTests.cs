using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CF.Adapters.Machine;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Languages.CSharp;
using CF.Languages.CSharp.Meaning;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Toolchain;

/// <summary>
/// What a review does with the evidence of a build, and with the lack of it: the build is played by a script, the review
/// and the plug-in are the real ones.
/// </summary>
public sealed class ToolchainBuildTests
{
    private const string _sourceWithConditionalBreaches = """
        namespace N;

        public class C
        {
            public int A(int v)
            {
        #if NET9_0 && FEATURE_X
                if(v > 0) { return 1; }
        #endif
        #if NET11_0
                if(v > 1) { return 2; }
        #endif
        #if NET9_0 || NET11_0
                if(v > 2) { return 3; }
        #endif
        #if NEVER
                if(v > 3) { return 4; }
        #error never defined
        #endif
                return 0;
            }
        }

        """;

    public static TheoryData<int, string, string, string> UnavailableSdks { get; } = new TheoryData<int, string, string, string>
    {
        new TheoryDataRow<int, string, string, string>(-1, "dotnet could not be started.", "not installed", "global.json") { TestDisplayName = "dotnet cannot be started" },
        new TheoryDataRow<int, string, string, string>(145, "A compatible .NET SDK was not found. Requested SDK version: 9.0.320", "global.json", "not installed") { TestDisplayName = "no installed SDK fits global.json" }
    };

    public static TheoryData<bool, string, string, string, bool> MissingEvidence { get; } = new TheoryData<bool, string, string, string, bool>
    {
        new TheoryDataRow<bool, string, string, string, bool>(true, string.Empty, "did not finish", string.Empty, false) { TestDisplayName = "the compile does not finish" },
        new TheoryDataRow<bool, string, string, string, bool>(false, "Strings.resx(1,1): error MSB3552: Resource file Strings.resx cannot be read. [/x/Library.csproj]", "before the compiler ran", "Strings.resx", false) { TestDisplayName = "a resource build fails before the compiler runs" },
        new TheoryDataRow<bool, string, string, string, bool>(false, string.Empty, "not inside the private copy", "CS0103", true) { TestDisplayName = "the compiler names a file by a path the tree does not contain, as a linked temporary folder makes it do" }
    };

    private static FakeProject Project(string name, string framework, string[] sources, params string[] references)
    {
        return new FakeProject(name + "/" + name + ".csproj", new FakeUnit(framework, sources, references.Select(reference => reference + "/" + reference + ".csproj").ToList()));
    }

    private static BuildAnswer CompileError(FakeBuildFixture fixture, string file, int line)
    {
        var log = new SarifLogText().Rule("CS0103", "error", true).Result("CS0103", "error", "The name 'missing' does not exist in the current context.", fixture.FullPath(file), line, 5).Closing("any", 1);
        return new BuildAnswer(1, log.ToString());
    }

    private static BuildAnswer ForeignRootError()
    {
        var foreign = Path.Combine(TestPaths.Temp, "cf-elsewhere", "Library", "Greeter.cs");
        var log = new SarifLogText().Rule("CS0103", "error", true).Result("CS0103", "error", "The name 'missing' does not exist in the current context.", foreign, 3, 5).Closing("any", 1);
        return new BuildAnswer(1, log.ToString());
    }

    private static List<string> FailedBuilds(IEnumerable<Coverage> coverage)
    {
        return coverage.Where(entry => entry.Kind == CoverageKind.Build && entry.Status != CoverageStatus.Reviewed).Select(entry => entry.What).Order(StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void BuildFailureStopsOnlyDependentFrameworks()
    {
        var consumer = new FakeProject("Consumer/Consumer.csproj", new FakeUnit("net9.0", new[] { "Consumer/Use.cs", "Consumer/Common.cs" }, new[] { "Library/Library.csproj" }), new FakeUnit("net11.0", new[] { "Consumer/Use.cs", "Consumer/Common.cs" }, new[] { "Library/Library.csproj" }));
        using(var fixture = new FakeBuildFixture(TreeFiles.NoRules, Project("Library", "net9.0", new[] { "Library/Greeter.cs" }), consumer, Project("Tool", "net11.0", new[] { "Tool/Runner.cs" }, "Consumer"), Project("Branch", "net11.0", new[] { "Branch/Branch.cs" }), Project("Broken", "net11.0", new[] { "Broken/Broken.cs" })))
        {
            // an error in a source nobody touched, which both frameworks of the consumer compile: it is one finding for both
            fixture.Dotnet.BuildAnswerFor = call => call.Project == "Consumer" ? CompileError(fixture, "Consumer/Common.cs", 3) : null;
            var common = fixture.RunToolchain(ReviewHarness.Changed("Library/Greeter.cs"));

            var finding = Assert.Single(common.Findings);
            Assert.Equal("Consumer/Common.cs", finding.File);
            Assert.Equal("Consumer", finding.Project);
            Assert.Equal(new[] { "net11.0", "net9.0" }, finding.TargetFramework!.Split(", ").Order(StringComparer.Ordinal));
            Assert.Equal(new[] { "Tool (net11.0)" }, FailedBuilds(common.Coverage));

            // a failure of one framework only: its downstream unit is skipped, the other framework is reviewed
            fixture.Dotnet.BuildAnswerFor = call => call.Project == "Consumer" && call.Framework == "net11.0" ? CompileError(fixture, "Consumer/Use.cs", 7) : null;
            var oneFramework = fixture.RunToolchain(ReviewHarness.Changed("Consumer/Use.cs"));

            Assert.Equal("net11.0", Assert.Single(oneFramework.Findings).TargetFramework);
            Assert.Equal(new[] { "Tool (net11.0)" }, FailedBuilds(oneFramework.Coverage));
            Assert.Equal(CoverageStatus.Reviewed, oneFramework.Coverage.Single(entry => entry.What == "Consumer (net9.0)").Status);
            Assert.Contains("prerequisite Consumer (net11.0)", oneFramework.Coverage.Single(entry => entry.What == "Tool (net11.0)").Reason, StringComparison.Ordinal);

            // a healthy branch is judged although another cannot restore its packages, and a block outweighs incomplete
            fixture.Dotnet.RestoreAnswer = project => project == "Broken" ? new ProcessResult(1, "error NU1101: Unable to find package Missing.Pkg. No packages exist with this id in source(s): x", string.Empty) : new ProcessResult(0, string.Empty, string.Empty);
            fixture.Dotnet.BuildAnswerFor = call => call.Project == "Branch" ? CompileError(fixture, "Branch/Branch.cs", 3) : null;
            var buildsBefore = fixture.Dotnet.Builds.Count;
            var review = fixture.Review(ReviewHarness.Changed("Branch/Branch.cs"), ReviewHarness.Changed("Broken/Broken.cs"));

            Assert.Equal(ExitCode.Blocked, review.ExitCode);
            Assert.Equal("Branch/Branch.cs", Assert.Single(review.Findings).File);
            Assert.Equal(new[] { "Broken (net11.0)" }, FailedBuilds(review.Coverage));
            Assert.Contains("never uses the network", review.Coverage.Single(entry => entry.What == "Broken (net11.0)").Reason, StringComparison.Ordinal);
            Assert.DoesNotContain(fixture.Dotnet.Builds.Skip(buildsBefore), call => call.Project == "Broken");
        }
    }

    [Fact]
    public void UnownedSourceLeavesCoverageIncomplete()
    {
        using(var fixture = new FakeBuildFixture(TreeFiles.NoRules))
        {
            TreeFiles.Write(fixture.Tree, "Stray/Stray.cs", TreeFiles.SourceText);

            var review = fixture.Review(ReviewHarness.Changed("Stray/Stray.cs"));

            Assert.Equal(ExitCode.Incomplete, review.ExitCode);
            var entry = Assert.Single(review.Coverage, candidate => candidate.Status == CoverageStatus.Failed);
            Assert.Equal("Stray/Stray.cs", entry.What);
            Assert.Contains("no project compiles this file", entry.Reason, StringComparison.Ordinal);
            Assert.Empty(review.Findings);
        }

        // the same beside a project that does own another changed file
        var graph = new SelectorGraph(Path.Combine(TestPaths.Temp, "cf-selector"));
        var plan = graph.Select(false, new[] { "M:Library/Greeter.cs", "M:Stray/Stray.cs" });

        Assert.Equal(new[] { "Stray/Stray.cs" }, plan.NotCovered);
        Assert.Contains(plan.Order, unit => unit.Label == "Library (net9.0)" && plan.ReviewedKeys.Contains(unit.Key));
    }

    [Theory]
    [MemberData(nameof(UnavailableSdks))]
    public void UnavailableSdkExplainsIncompleteReview(int exitCode, string error, string mustSay, string mustNotSay)
    {
        using(var fixture = new FakeBuildFixture(TreeFiles.NoRules, Project("Library", "net9.0", new[] { "Library/Greeter.cs" })))
        {
            fixture.Dotnet.VersionAnswer = new ProcessResult(exitCode, string.Empty, error);

            var review = fixture.Review(ReviewHarness.Changed("Library/Greeter.cs"));

            Assert.Equal(ExitCode.Incomplete, review.ExitCode);
            var reason = Assert.Single(review.Coverage, entry => entry.Status == CoverageStatus.Failed).Reason;
            Assert.Contains(mustSay, reason, StringComparison.Ordinal);
            Assert.DoesNotContain(mustNotSay, reason, StringComparison.Ordinal);
            Assert.Contains("Install", reason, StringComparison.Ordinal);
            Assert.Empty(fixture.Dotnet.Builds);
            Assert.Single(fixture.Dotnet.Requests);
        }
    }

    [Fact]
    public void ExternalIntermediateAssemblyIsUntouched()
    {
        string? decoy = null;
        using(var fixture = new FakeBuildFixture(TreeFiles.NoRules, root =>
        {
            // a sibling of the tree whose name starts with the tree's name: it is not inside the tree
            decoy = Path.Combine(root, "tree-other", "obj", "Library.dll");
            return new[] { new FakeProject("Library/Library.csproj", new FakeUnit("net9.0", new[] { "Library/Greeter.cs" }, new List<string>(), null, decoy)) };
        }))
        {
            TreeFiles.Write(fixture.Root, Path.Combine("tree-other", "obj", "Library.dll"), "sentinel");

            var result = fixture.RunToolchain(ReviewHarness.Changed("Library/Greeter.cs"));

            var entry = Assert.Single(result.Coverage);
            Assert.Equal(CoverageStatus.Failed, entry.Status);
            Assert.Contains("outside the private copy", entry.Reason, StringComparison.Ordinal);
            Assert.Contains("Library.dll", entry.Reason, StringComparison.Ordinal);
            Assert.Equal("sentinel", File.ReadAllText(decoy!));
            Assert.Empty(fixture.Dotnet.Builds);
        }
    }

    [Theory]
    [MemberData(nameof(MissingEvidence))]
    public void MissingBuildEvidenceCannotPass(bool timedOut, string output, string mustSay, string mustAlsoSay, bool foreignRoot)
    {
        using(var fixture = new FakeBuildFixture(TreeFiles.NoRules, Project("Library", "net9.0", new[] { "Library/Greeter.cs" })))
        {
            fixture.Dotnet.BuildAnswerFor = _ => foreignRoot ? ForeignRootError() : new BuildAnswer(timedOut ? -2 : 1, null, timedOut, output);

            var review = fixture.Review(ReviewHarness.Changed("Library/Greeter.cs"));

            Assert.Equal(ExitCode.Incomplete, review.ExitCode);
            var entry = Assert.Single(review.Coverage, candidate => candidate.Status == CoverageStatus.Failed);
            Assert.Equal("Library (net9.0)", entry.What);
            Assert.Contains(mustSay, entry.Reason, StringComparison.Ordinal);
            Assert.Contains(mustAlsoSay, entry.Reason, StringComparison.Ordinal);
            Assert.Empty(review.Findings);
        }
    }

    [Fact]
    public void AFileFromOutsideTheRepositoryIsLeftOutAndALogAboutNoFileOfItIsRefused()
    {
        // a source file that a package adds to the compile lies in the user's package folder, not in the tree
        var foreign = Path.Combine(TestPaths.Temp, "cf-packages", "runner", "Reporters.cs");
        using(var fixture = new FakeBuildFixture(TreeFiles.NoRules, Project("Library", "net9.0", new[] { "Library/Greeter.cs" })))
        {
            var log = new SarifLogText().Rule("CA1822", "warning", false).Result("CA1822", "warning", "Member can be static.", foreign, 3, 5).Closing("any", 1);
            fixture.Dotnet.BuildAnswerFor = _ => new BuildAnswer(0, log.ToString());

            var review = fixture.Review(ReviewHarness.Changed("Library/Greeter.cs"));

            Assert.Equal(ExitCode.Passed, review.ExitCode);
            Assert.Empty(review.Findings);
            Assert.DoesNotContain(review.Coverage, entry => entry.Status == CoverageStatus.Failed);

            var reader = new MeaningFactReader(Path.GetDirectoryName(Path.GetDirectoryName(fixture.FullPath("Library/Greeter.cs")))!);
            var own = "D|" + fixture.FullPath("Library/Greeter.cs") + "|3|5|type|Library.Greeter|public|class";
            var packaged = "D|" + foreign + "|1|1|type|Runner.Reporters|internal|class";

            var facts = reader.Read(new[] { own, packaged }, new[] { ClosingOf(own, packaged) }, "Library", 1);

            Assert.Equal("Library/Greeter.cs", Assert.Single(facts.Declarations).File.Replace(Path.DirectorySeparatorChar, '/'));
            var refusal = Assert.Throws<MeaningLogException>(() => reader.Read(new[] { packaged }, new[] { ClosingOf(packaged) }, "Library", 1));
            Assert.Contains("no file inside the private copy", refusal.Message, StringComparison.Ordinal);
        }
    }

    private static string ClosingOf(params string[] factLines)
    {
        var sorted = string.Join('\n', factLines.Order(StringComparer.Ordinal));
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sorted))).ToLowerInvariant();

        return string.Join("|", "END", "1", "Library", "1", factLines.Length.ToString(CultureInfo.InvariantCulture), checksum);
    }

    [Fact]
    public void ConditionalRuleUsesOnlyActualFileReadings()
    {
        var library = new FakeProject("Library/Library.csproj", new FakeUnit("net9.0", new[] { "Library/Greeter.cs" }, new List<string>(), new[] { "NET9_0", "FEATURE_X" }), new FakeUnit("net11.0", new[] { "Library/Greeter.cs" }, new List<string>(), new[] { "NET11_0" }));
        using(var fixture = new FakeBuildFixture(TreeFiles.BracedIfRule, library))
        {
            fixture.Dotnet.SymbolSeparator = ",";
            TreeFiles.Write(fixture.Tree, "Library/Greeter.cs", _sourceWithConditionalBreaches);

            var review = fixture.Review(ReviewHarness.Changed("Library/Greeter.cs"));

            Assert.Equal(ExitCode.Blocked, review.ExitCode);
            var found = review.Findings.OrderBy(finding => finding.Line).ToList();
            Assert.Equal(new int?[] { 8, 11, 14 }, found.Select(finding => finding.Line));
            Assert.All(found, finding => Assert.Equal("TEAM010", finding.RuleId));
            Assert.Equal(new string?[] { "net9.0", "net11.0", null }, found.Select(finding => finding.TargetFramework));
            Assert.Equal(new string?[] { "Library", "Library", null }, found.Select(finding => finding.Project));

            // the source parses for every reading, the inactive #error included, and the file counts as reviewed
            var file = review.Coverage.Single(entry => entry.What == "Library/Greeter.cs");
            Assert.Equal(CoverageStatus.Reviewed, file.Status);
        }
    }

    [Fact]
    public void RetryKeepsNewRuleFindingsAndRealCompileErrors()
    {
        using(var fixture = new FakeBuildFixture(TreeFiles.NoRules, Project("Library", "net9.0", new[] { "Library/Greeter.cs" })))
        {
            var greeter = fixture.FullPath("Library/Greeter.cs");
            fixture.Dotnet.BuildAnswerFor = call =>
            {
                if(call.LetThrough.Count == 0)
                {
                    var first = new SarifLogText().Rule("IDE0044", "suggestion", false).Result("IDE0044", "error", "Make field readonly.", greeter, 5, 5).Result("IDE0044", "error", "Make field readonly.", greeter, 12, 5).Closing("Library", 1);
                    return new BuildAnswer(1, first.ToString());
                }

                return CompileError(fixture, "Library/Greeter.cs", 7);
            };

            var changed = new ChangedFile("Library/Greeter.cs", "csharp", ChangeStatus.Modified, new List<LineRange> { LineRange.ForLines(10, 20) });
            var review = fixture.Review(changed);

            Assert.Equal(ExitCode.Blocked, review.ExitCode);
            Assert.Equal(new[] { "CS0103@7", "IDE0044@12" }, review.Findings.Select(finding => finding.RuleId + "@" + finding.Line).Order(StringComparer.Ordinal));
            Assert.All(review.Findings, finding => Assert.Equal(Severity.Block, finding.Severity));
            Assert.Equal(2, fixture.Dotnet.Builds.Count);
            Assert.Equal(new[] { "IDE0044" }, fixture.Dotnet.Builds[1].LetThrough);
        }
    }

    [Fact]
    public void ToolchainExceptionBecomesIncompleteCoverage()
    {
        using(var fixture = new FakeBuildFixture(TreeFiles.NoRules, Project("Library", "net9.0", new[] { "Library/Greeter.cs" })))
        {
            var inner = new CSharpLanguage(fixture.Dotnet, new SystemFileSystem(), new TestDataFolder(fixture.Data));
            var language = new ThrowingLanguage(inner);
            var harness = new ReviewHarness(new List<ILanguage> { language }, fixture.Data, new FolderSnapshots(fixture.Tree), new EmptyTreeGit());

            var review = harness.Review(ChangeSet.ForChanges("the change", "tree-before", "tree-after", new[] { ReviewHarness.Changed("Library/Greeter.cs") }, new List<string>(), string.Empty));

            Assert.Equal(ExitCode.Incomplete, review.ExitCode);
            Assert.Empty(review.Findings);
            var entry = Assert.Single(review.Coverage, candidate => candidate.Status == CoverageStatus.Failed);
            Assert.Equal(CoverageKind.Build, entry.Kind);
            Assert.Contains("the build could not be run", entry.Reason, StringComparison.Ordinal);
        }
    }
}
