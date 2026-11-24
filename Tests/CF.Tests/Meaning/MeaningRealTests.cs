using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Meaning;

/// <summary>
/// The push that only the compiler can judge: a type changes folders, and callers nobody edited now break a layer direction.
/// </summary>
public sealed class MeaningRealTests
{
    private const string _settings = """
        {
            "rules": [],
            "structure": {
                "modules": {
                    "domain": [ "folder:src/Shop/Domain/**" ],
                    "infrastructure": [ "folder:src/Shop/Infrastructure/**" ],
                    "reports": [ "project:Shop.Reports" ]
                },
                "directions": {
                    "allow": [ "infrastructure -> domain", "reports -> domain" ]
                }
            }
        }

        """;

    private const string _calculator = "namespace Shop.Domain;\n\npublic class PriceCalculator\n{\n    public decimal Total(decimal amount)\n    {\n        return amount * 2;\n    }\n}\n";

    private const string _factory = "namespace Shop.Domain;\n\npublic static class PriceFactory\n{\n    public static PriceCalculator Create()\n    {\n        return new PriceCalculator();\n    }\n}\n";

    private const string _orders = "namespace Shop.Domain;\n\npublic class OrderService\n{\n    private readonly PriceCalculator _calculator = new PriceCalculator();\n\n    public decimal Place(decimal amount)\n    {\n        return _calculator.Total(amount);\n    }\n\n    public decimal PlaceInferred(decimal amount)\n    {\n        var calculator = PriceFactory.Create();\n        return calculator.Total(amount);\n    }\n}\n";

    private const string _monthly = "using Shop.Domain;\n\nnamespace Shop.Reports;\n\npublic class MonthlyReport\n{\n    public decimal Build(decimal amount)\n    {\n        var calculator = new PriceCalculator();\n        return calculator.Total(amount);\n    }\n}\n";

    private const string _legacy = "namespace Shop.Reports;\n\npublic class LegacyReport\n{\n    public string Run(string name)\n    {\n        return Shop.Infrastructure.SqlHelper.Quote(name);\n    }\n}\n";

    private const string _sql = "namespace Shop.Infrastructure;\n\npublic static class SqlHelper\n{\n    public static string Quote(string value)\n    {\n        return \"'\" + value + \"'\";\n    }\n}\n";

    // what a repository can do to keep our analyzer from working: switch analyzers off, silence and raise its notes, turn warnings into errors
    private const string _hostileProps = "<Project>\n  <PropertyGroup>\n    <RunAnalyzers>false</RunAnalyzers>\n    <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>\n    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n    <NoWarn>$(NoWarn);CFM001;CFM999</NoWarn>\n    <WarningsAsErrors>$(WarningsAsErrors);CFM001;CFM999</WarningsAsErrors>\n  </PropertyGroup>\n</Project>\n";

    private const string _hostileEditorConfig = "root = true\n\n[*.cs]\ndotnet_analyzer_diagnostic.severity = none\ndotnet_diagnostic.CFM001.severity = none\ndotnet_diagnostic.CFM999.severity = error\n";

    private static void WriteShop(string tree, bool isMoved)
    {
        TreeFiles.Write(tree, "src/Shop/Shop.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net11.0</TargetFramework>\n    <Nullable>enable</Nullable>\n    <ImplicitUsings>enable</ImplicitUsings>\n  </PropertyGroup>\n</Project>\n");
        TreeFiles.Write(tree, "src/Shop.Reports/Shop.Reports.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net11.0</TargetFramework>\n    <Nullable>enable</Nullable>\n    <ImplicitUsings>enable</ImplicitUsings>\n  </PropertyGroup>\n  <ItemGroup>\n    <ProjectReference Include=\"..\\Shop\\Shop.csproj\" />\n  </ItemGroup>\n</Project>\n");
        TreeFiles.Write(tree, "src/Shop/" + (isMoved ? "Infrastructure" : "Domain") + "/PriceCalculator.cs", _calculator);
        TreeFiles.Write(tree, "src/Shop/Domain/PriceFactory.cs", _factory);
        TreeFiles.Write(tree, "src/Shop/Domain/OrderService.cs", _orders);
        TreeFiles.Write(tree, "src/Shop/Infrastructure/SqlHelper.cs", _sql);
        TreeFiles.Write(tree, "src/Shop.Reports/MonthlyReport.cs", _monthly);
        TreeFiles.Write(tree, "src/Shop.Reports/LegacyReport.cs", _legacy);
        TreeFiles.Write(tree, "Directory.Build.props", _hostileProps);
        TreeFiles.Write(tree, ".editorconfig", _hostileEditorConfig);
        TreeFiles.Write(tree, "commitfence.json", _settings);
    }

    private static int LineOf(string text, string marker)
    {
        return Array.FindIndex(text.Split('\n'), line => line.Contains(marker, StringComparison.Ordinal)) + 1;
    }

    [Fact]
    public void MovingTypeAcrossFolderBoundaryBlocksUntouchedCallers()
    {
        using(var folder = new LinkedFolder())
        {
            var before = Path.Combine(folder.Path, "before");
            var after = Path.Combine(folder.Path, "after");
            WriteShop(before, false);
            WriteShop(after, true);
            var beforeContents = TreeContents.OutsideBuildOutputs(before);
            var afterContents = TreeContents.OutsideBuildOutputs(after);
            var snapshots = new FolderSnapshots(after);
            snapshots.Add("tree-before", before);
            snapshots.Add("tree-after", after);
            var runner = new IsolatedProcessRunner(Path.Combine(folder.Path, "home"), Path.Combine(folder.Path, "packages"));
            var harness = ReviewHarness.ForCSharp(runner, Path.Combine(folder.Path, "data"), snapshots, new EmptyTreeGit());
            var moved = new ChangedFile("src/Shop/Infrastructure/PriceCalculator.cs", "csharp", ChangeStatus.Renamed, new List<LineRange> { LineRange.ForWholeFile() }, "src/Shop/Domain/PriceCalculator.cs");

            var review = harness.Review(ChangeSet.ForChanges("the move", "tree-before", "tree-after", new[] { moved }, new List<string>(), string.Empty));

            // nothing but the callers' own lines: the field, the call and the inferred local of one class, the return type of the factory, the local of the other project
            // a wrong verdict says what the review saw: on another system that is the only evidence there is
            Assert.True(review.ExitCode == ExitCode.Blocked, $"the review ended {review.ExitCode}: " + string.Join("; ", review.Coverage.Select(entry => $"{entry.What}: {entry.Status} {entry.Reason}")) + " | " + string.Join("; ", review.Findings.Select(finding => $"{finding.RuleId} {finding.Severity} {finding.File}:{finding.Line}")));
            var found = review.Findings.Select(finding => $"{finding.File}:{finding.Line}").Order(StringComparer.Ordinal).ToList();
            var expected = new[]
            {
                "src/Shop/Domain/OrderService.cs:" + LineOf(_orders, "private readonly PriceCalculator"),
                "src/Shop/Domain/OrderService.cs:" + LineOf(_orders, "_calculator.Total"),
                "src/Shop/Domain/OrderService.cs:" + LineOf(_orders, "var calculator = PriceFactory"),
                "src/Shop/Domain/PriceFactory.cs:" + LineOf(_factory, "public static PriceCalculator"),
                "src/Shop.Reports/MonthlyReport.cs:" + LineOf(_monthly, "var calculator")
            };
            Assert.Equal(expected.Order(StringComparer.Ordinal), found);
            Assert.All(review.Findings, finding => Assert.Equal("ARCH001", finding.RuleId));
            Assert.Contains(review.Findings, finding => finding.Message.Contains("domain must not use infrastructure", StringComparison.Ordinal));

            // the violation that was already there is not the push's, and no rule was left unrun
            Assert.DoesNotContain(review.Findings, finding => finding.File!.EndsWith("LegacyReport.cs", StringComparison.Ordinal));
            Assert.Empty(review.Skipped);
            Assert.DoesNotContain(review.Coverage, entry => entry.Status != CoverageStatus.Reviewed);
            Assert.Equal(new[] { "Shop (net11.0)", "Shop.Reports (net11.0)" }, review.Coverage.Where(entry => entry.Kind == CoverageKind.Build).Select(entry => entry.What).Order(StringComparer.Ordinal));

            // both trees are left as they were, and the private copy ends on the tree under review
            Assert.Equal(beforeContents, TreeContents.OutsideBuildOutputs(before));
            Assert.Equal(afterContents, TreeContents.OutsideBuildOutputs(after));
            Assert.Equal("tree-after", snapshots.Moves[^1]);
        }
    }
}
