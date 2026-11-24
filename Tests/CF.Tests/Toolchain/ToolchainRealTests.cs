using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Toolchain;

/// <summary>
/// The builds that cannot be played from a script: the installed SDK compiles real projects, so that the repository's
/// settings, the retry, the analyzer and the evidence are tried together.
/// </summary>
public sealed class ToolchainRealTests
{
    private const string _oldBreach = """
        namespace Consumer;

        public class Old
        {
            private int _count = 1;

            public int Count()
            {
                return _count;
            }
        }

        """;

    private const string _gate = """
        namespace Consumer;

        public class Gate
        {
            public System.DateTime Read()
            {
        #if REQUIRED_SYMBOL
                return System.DateTime.Now;
        #else
                return System.DateTime.MinValue;
        #endif
            }
        }

        """;

    private const string _clockRule = """
        {
            "rules": [
                {
                    "id": "TEAM012",
                    "language": "csharp",
                    "for": "usage",
                    "severity": "block",
                    "title": "Use IClock, not DateTime.Now",
                    "where": { "target": "System.DateTime.Now" },
                    "forbid": true
                }
            ]
        }

        """;

    [Fact]
    public void TeamBuildPreservesImportsSuppressionsAndMeaning()
    {
        using(var folder = new LinkedFolder())
        {
            var tree = Path.Combine(folder.Path, "tree");
            TreeFiles.Write(tree, ".editorconfig", "root = true\n\n[*.cs]\ndotnet_diagnostic.IDE0044.severity = error\ndotnet_diagnostic.CA1050.severity = suggestion\n");
            TreeFiles.Write(tree, "Directory.Build.props", "<Project>\n  <PropertyGroup>\n    <CustomAfterDirectoryBuildTargets>$(MSBuildThisFileDirectory)repository.targets</CustomAfterDirectoryBuildTargets>\n  </PropertyGroup>\n</Project>\n");
            TreeFiles.Write(tree, "repository.targets", "<Project>\n  <PropertyGroup>\n    <DefineConstants>$(DefineConstants);REQUIRED_SYMBOL</DefineConstants>\n  </PropertyGroup>\n</Project>\n");
            TreeFiles.Write(tree, "Library/Library.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net11.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n");
            TreeFiles.Write(tree, "Library/Greeter.cs", "namespace Library;\n\npublic static class Greeter\n{\n    private static int _calls = 1;\n\n    public static int One()\n    {\n        return _calls;\n    }\n}\n");
            TreeFiles.Write(tree, "Consumer/Consumer.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net11.0</TargetFramework>\n    <GenerateDocumentationFile>true</GenerateDocumentationFile>\n    <NoWarn>$(NoWarn);CS1591</NoWarn>\n    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n  </PropertyGroup>\n  <ItemGroup>\n    <ProjectReference Include=\"..\\Library\\Library.csproj\" />\n  </ItemGroup>\n</Project>\n");
            TreeFiles.Write(tree, "Consumer/Old.cs", _oldBreach);
            TreeFiles.Write(tree, "Consumer/Gate.cs", _gate);
            TreeFiles.Write(tree, "Consumer/Bad.cs", "public class Bad\n{\n}\n");
            TreeFiles.Write(tree, "commitfence.json", _clockRule);
            var before = TreeContents.OutsideBuildOutputs(tree);
            var data = Path.Combine(folder.Path, "data");
            var runner = new IsolatedProcessRunner(Path.Combine(folder.Path, "home"), Path.Combine(folder.Path, "packages"));
            var harness = ReviewHarness.ForCSharp(runner, data, new FolderSnapshots(tree), new EmptyTreeGit());

            var review = harness.Review(ChangeSet.ForChanges("the change", "tree-before", "tree-after", new[] { ReviewHarness.Changed("Consumer/Gate.cs", ChangeStatus.Added), ReviewHarness.Changed("Consumer/Bad.cs", ChangeStatus.Added) }, new List<string>(), string.Empty));

            // a wrong verdict says what the review saw: on another system that is the only evidence there is
            Assert.True(review.ExitCode == ExitCode.Blocked, $"the review ended {review.ExitCode}: " + string.Join("; ", review.Coverage.Select(entry => $"{entry.What}: {entry.Status} {entry.Reason}")) + " | " + string.Join("; ", review.Findings.Select(finding => $"{finding.RuleId} {finding.Severity} {finding.File}:{finding.Line}")));
            Assert.Equal(CoverageStatus.Reviewed, Assert.Single(review.Coverage, entry => entry.What == "Consumer (net11.0)").Status);
            Assert.DoesNotContain(review.Coverage, entry => entry.Status != CoverageStatus.Reviewed);

            // the symbol comes from the repository's own targets, the analyzer from CommitFence: together they make the breach visible
            var clock = Assert.Single(review.Findings, finding => finding.RuleId == "TEAM012");
            Assert.Equal("Consumer/Gate.cs", clock.File);
            Assert.Equal(Array.IndexOf(_gate.Split('\n'), "        return System.DateTime.Now;") + 1, clock.Line);
            Assert.Equal(Severity.Block, clock.Severity);

            // the old breaches and the suppressed warning stay out, a suggestion of the compiler arrives as a warning
            Assert.DoesNotContain(review.Findings, finding => finding.RuleId == "IDE0044" || finding.RuleId == "CS1591");
            var suggestion = Assert.Single(review.Findings, finding => finding.RuleId == "CA1050");
            Assert.Equal(Severity.Warn, suggestion.Severity);
            Assert.Equal("Consumer/Bad.cs", suggestion.File);

            // nothing of the review is left in the tree besides build outputs
            Assert.Equal(before, TreeContents.OutsideBuildOutputs(tree));
        }
    }

    private static ReviewResult WholeReview(ReviewHarness harness, IReadOnlyList<ChangedFile> files)
    {
        return harness.Review(ChangeSet.ForWholeReview("the whole tree", "tree-before", "tree-after", files, string.Empty));
    }

    [Fact]
    public void WholeReviewTakesAnUnchangedDependentFromWhatWasKeptUntilAnEditorConfigChangesIt()
    {
        using(var folder = new LinkedFolder())
        {
            var tree = Path.Combine(folder.Path, "tree");
            TreeFiles.Write(tree, "Library/Library.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net11.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n");
            TreeFiles.Write(tree, "Library/Greeter.cs", "namespace Library;\n\npublic static class Greeter\n{\n    public static int One()\n    {\n        return 1;\n    }\n}\n");
            TreeFiles.Write(tree, "Consumer/Consumer.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net11.0</TargetFramework>\n  </PropertyGroup>\n  <ItemGroup>\n    <ProjectReference Include=\"..\\Library\\Library.csproj\" />\n  </ItemGroup>\n</Project>\n");
            TreeFiles.Write(tree, "Consumer/Use.cs", "namespace Consumer;\n\npublic static class Use\n{\n    public static int Go()\n    {\n        return Library.Greeter.One();\n    }\n}\n");
            TreeFiles.Write(tree, "Consumer/Sub/Hidden.cs", "namespace Consumer;\n\nclass Hidden\n{\n}\n");
            TreeFiles.Write(tree, "commitfence.json", TreeFiles.NoRules);
            var files = new List<string> { "Library/Greeter.cs", "Consumer/Use.cs", "Consumer/Sub/Hidden.cs", "Library/Library.csproj", "Consumer/Consumer.csproj" }.Select(path => ReviewHarness.Changed(path)).ToList();
            var runner = new IsolatedProcessRunner(Path.Combine(folder.Path, "home"), Path.Combine(folder.Path, "packages"));
            var harness = ReviewHarness.ForCSharp(runner, Path.Combine(folder.Path, "data"), new FolderSnapshots(tree), new EmptyTreeGit());

            var first = WholeReview(harness, files);
            var second = WholeReview(harness, files);
            TreeFiles.Write(tree, "Consumer/Sub/.editorconfig", "[*.cs]\ndotnet_diagnostic.IDE0040.severity = warning\n");
            var third = WholeReview(harness, files);

            Assert.Equal(CoverageStatus.Reviewed, Assert.Single(first.Coverage, entry => entry.What == "Consumer (net11.0)").Status);
            Assert.DoesNotContain(first.Coverage, entry => entry.Reason.Contains("kept", StringComparison.Ordinal));
            Assert.Contains("kept", Assert.Single(second.Coverage, entry => entry.What == "Consumer (net11.0)").Reason, StringComparison.Ordinal);
            Assert.Equal(first.Findings.Select(finding => finding.RuleId + "@" + finding.File).Order(StringComparer.Ordinal), second.Findings.Select(finding => finding.RuleId + "@" + finding.File).Order(StringComparer.Ordinal));
            Assert.DoesNotContain(first.Findings, finding => finding.RuleId == "IDE0040");

            // the file the compiler was given changed, so the project was compiled again and the new warning is there
            Assert.Equal(string.Empty, Assert.Single(third.Coverage, entry => entry.What == "Consumer (net11.0)").Reason);
            var warning = Assert.Single(third.Findings, finding => finding.RuleId == "IDE0040");
            Assert.Equal("Consumer/Sub/Hidden.cs", warning.File);
        }
    }

    [Fact]
    public void OfflineRestoreIgnoresConfiguredAndAdditionalFeeds()
    {
        using(var folder = new LinkedFolder())
        using(var configured = new LoopbackFeed())
        using(var additional = new LoopbackFeed())
        {
            var tree = Path.Combine(folder.Path, "tree");
            TreeFiles.Write(tree, "NuGet.config", $"<configuration>\n  <packageSources>\n    <clear />\n    <add key=\"configured\" value=\"{configured.Address}\" />\n  </packageSources>\n</configuration>\n");
            TreeFiles.Write(tree, "Library/Library.csproj", $"<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net11.0</TargetFramework>\n    <RestoreAdditionalProjectSources>{additional.Address}</RestoreAdditionalProjectSources>\n    <NuGetAudit>false</NuGetAudit>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"Missing.Package\" Version=\"1.0.0\" />\n  </ItemGroup>\n</Project>\n");
            TreeFiles.Write(tree, "Library/Greeter.cs", TreeFiles.SourceText);
            TreeFiles.Write(tree, "commitfence.json", TreeFiles.NoRules);
            var runner = new IsolatedProcessRunner(Path.Combine(folder.Path, "home"), Path.Combine(folder.Path, "packages"));

            // the control: an ordinary restore of this fixture does reach both feeds, so that no connection below means something
            var ordinary = runner.Run(new ProcessRequest("dotnet", new[] { "restore", Path.Combine(tree, "Library", "Library.csproj"), "-nologo" }, tree, new Dictionary<string, string?>(), null, TimeSpan.FromMinutes(5)));

            Assert.NotEqual(0, ordinary.ExitCode);
            Assert.True(configured.Connections > 0, "the configured feed was never asked: " + ordinary.Output);
            Assert.True(additional.Connections > 0, "the additional feed was never asked: " + ordinary.Output);
            configured.Forget();
            additional.Forget();
            var harness = ReviewHarness.ForCSharp(runner, Path.Combine(folder.Path, "data"), new FolderSnapshots(tree), new EmptyTreeGit());

            var review = harness.Review(ChangeSet.ForChanges("the change", "tree-before", "tree-after", new[] { ReviewHarness.Changed("Library/Greeter.cs") }, new List<string>(), string.Empty));

            Assert.Equal(0, configured.Connections);
            Assert.Equal(0, additional.Connections);
            Assert.Equal(ExitCode.Incomplete, review.ExitCode);
            var entry = Assert.Single(review.Coverage, candidate => candidate.Status == CoverageStatus.Failed);
            Assert.Equal("Library (net11.0)", entry.What);
            Assert.Contains("Missing.Package", entry.Reason, StringComparison.Ordinal);
            Assert.Contains("while online", entry.Reason, StringComparison.Ordinal);
        }
    }
}
