using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Tests.Meaning.Support;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Meaning;

/// <summary>
/// What a review says when the evidence about meaning is missing, and what a review of named files leaves unsaid.
/// </summary>
public sealed class MeaningReviewTests
{
    private static string RulesAndLayers()
    {
        return """
            {
                "rules": [
                    {
                        "id": "TEAM010",
                        "language": "csharp",
                        "for": "if-statement",
                        "severity": "block",
                        "title": "No braces around a single-line if",
                        "where": { "statementsInBody": 1, "bodyFitsOnOneLine": true },
                        "require": { "braces": false }
                    },
                    {
                        "id": "TEAM011",
                        "language": "csharp",
                        "for": "method",
                        "severity": "block",
                        "title": "Only public and private methods inside an internal class",
                        "where": { "containingType": "class", "containingTypeDeclaredAccess": "internal", "isOverride": false },
                        "require": { "declaredAccess": ["public", "private"] }
                    },
                    {
                        "id": "TEAM012",
                        "language": "csharp",
                        "for": "usage",
                        "severity": "block",
                        "title": "Use IClock, not DateTime.Now",
                        "where": { "target": "System.DateTime.Now" },
                        "forbid": true
                    }
                ],
                "structure": {
                    "modules": { "domain": [ "folder:src/Domain/**" ], "infrastructure": [ "folder:src/Infrastructure/**" ] },
                    "directions": { "allow": [ "infrastructure -> domain" ] }
                }
            }
            """;
    }

    private static string LayerSettings()
    {
        return MeaningSettings.Layers(
            new Dictionary<string, string[]> { ["reports"] = new[] { "namespace:Shop.Reports" }, ["infrastructure"] = new[] { "namespace:Shop.Infrastructure" } },
            "infrastructure -> reports");
    }

    [Fact]
    public void MissingClosingEntryMakesStructureReviewIncomplete()
    {
        var good = new FakeProject("Good/Good.csproj", new FakeUnit("net11.0", new[] { "Good/Secret.cs", "Good/Report.cs" }, new List<string>()));
        var bad = new FakeProject("Bad/Bad.csproj", new FakeUnit("net11.0", new[] { "Bad/Other.cs" }, new List<string>()));
        using(var fixture = new FakeBuildFixture(LayerSettings(), good, bad))
        {
            // the project Good breaks a layer direction; the project Bad has facts in its log and no closing entry of the analyzer
            fixture.Dotnet.BuildAnswerFor = call =>
            {
                var isGood = call.Project == "Good";
                var sources = isGood
                    ? new Dictionary<string, string> { ["Good/Secret.cs"] = "namespace Shop.Infrastructure;\n\npublic class Secret\n{\n    public int Value()\n    {\n        return 1;\n    }\n}\n", ["Good/Report.cs"] = "namespace Shop.Reports;\n\npublic class Report\n{\n    public int Run()\n    {\n        return new Shop.Infrastructure.Secret().Value();\n    }\n}\n" }
                    : new Dictionary<string, string> { ["Bad/Other.cs"] = "namespace Shop.Other;\n\npublic class Other\n{\n    public int Value()\n    {\n        return 2;\n    }\n}\n" };
                var analysis = MeaningCompiler.Analyze(call.Project, fixture.Tree, sources);
                Assert.NotEmpty(analysis.Facts);

                return new BuildAnswer(0, analysis.LogText(isGood));
            };

            var review = fixture.Review(ReviewHarness.Changed("Good/Report.cs"), ReviewHarness.Changed("Bad/Other.cs"));

            Assert.Equal(ExitCode.Incomplete, review.ExitCode);
            var entry = Assert.Single(review.Coverage, candidate => candidate.Kind == CoverageKind.Build && candidate.Status == CoverageStatus.Failed);
            Assert.Equal("Bad (net11.0)", entry.What);
            Assert.Contains("closing entry", entry.Reason, StringComparison.Ordinal);
            Assert.Contains(review.Skipped, sentence => sentence.StartsWith("ARCH001 was not run", StringComparison.Ordinal));
            Assert.DoesNotContain(review.Findings, finding => finding.RuleId == "ARCH001");
        }
    }

    [Fact]
    public void FileReviewNamesUnrunRulesAndPrioritizesBlockingFindings()
    {
        using(var folder = new TestFolder())
        {
            TreeFiles.Write(folder.Path, "commitfence.json", RulesAndLayers());
            TreeFiles.Write(folder.Path, "src/Domain/Clean.cs", "namespace Shop.Domain;\n\npublic class Clean\n{\n    public int Value(int input)\n    {\n        return input;\n    }\n}\n");
            TreeFiles.Write(folder.Path, "src/Domain/Braced.cs", "namespace Shop.Domain;\n\npublic class Braced\n{\n    public int Value(int input)\n    {\n        if(input > 0) { return 1; }\n\n        return 0;\n    }\n}\n");
            var commands = new CommandHarness(folder.Path);

            var clean = commands.Run("review", "--files", "src/Domain/Clean.cs", "--settings", "commitfence.json");
            var braced = commands.Run("review", "--files", "src/Domain/Braced.cs", "--settings", "commitfence.json");

            // nothing found is still not a pass: three rules need a build that a review of named files does not run, and it says which
            Assert.Equal(ExitCode.Incomplete, clean.ExitCode);
            Assert.DoesNotContain(" error ", clean.Output, StringComparison.Ordinal);
            foreach(var output in new[] { clean.Output, braced.Output })
            {
                foreach(var unrun in new[] { "TEAM011", "TEAM012", "ARCH001" })
                    Assert.Contains($"not run: {unrun}", output, StringComparison.Ordinal);
            }

            // a finding that blocks outweighs the rules that were not run
            Assert.Equal(ExitCode.Blocked, braced.ExitCode);
            Assert.Contains("Braced.cs(7,9): error TEAM010", braced.Output, StringComparison.Ordinal);
        }
    }
}
