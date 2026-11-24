using System.Text.Json;
using CF.Core.Findings;
using CF.Languages.CSharp.Toolchain;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Toolchain;

/// <summary>
/// What the toolchain decides before and after the build: which units a change makes necessary, and what a compiler's log
/// says about severity, place and suppression.
/// </summary>
public sealed class ToolchainSelectionTests
{
    public static TheoryData<bool, string, string, string, string> Changes { get; } = CreateChanges();

    private static TheoryData<bool, string, string, string, string> CreateChanges()
    {
        const string all = "Library (net9.0);Consumer (net9.0);Consumer (net11.0);Tool (net11.0);Other (net11.0)";
        const string downstream = "Consumer (net9.0);Consumer (net11.0);Tool (net11.0)";
        return new TheoryData<bool, string, string, string, string>
        {
            Row("owned source: its owner and everything that depends on it", false, "M:Library/Greeter.cs", "Library (net9.0);" + downstream, string.Empty, string.Empty),
            Row("deleted project: every remaining unit", false, "D:Library/Library.csproj", all, string.Empty, string.Empty),
            Row("deleted linked source outside every project: every unit", false, "D:Linked/Gone.cs", all, string.Empty, string.Empty),
            Row("text inside a project: its owner and its dependents", false, "M:Consumer/notes.txt", downstream, "Library (net9.0)", string.Empty),
            Row("custom props outside the projects: every unit", false, "M:build/Custom.props", all, string.Empty, string.Empty),
            Row("Directory.Build.targets of a sibling folder: every unit", false, "M:Consumer/Directory.Build.targets", all, string.Empty, string.Empty),
            Row("resource renamed to outside: the project it left", false, "R:Consumer/Strings.resx->Assets/new.resx", downstream, "Library (net9.0)", "Assets/new.resx=False;Consumer/Strings.resx=True"),
            Row("root README: nothing", false, "M:README.md", string.Empty, string.Empty, "README.md=False"),
            Row("whole review: a prerequisite is only built, what depends on the target and an unrelated unit are left out", true, "M:Consumer/Use.cs|M:build/Extra.props", "Consumer (net9.0);Consumer (net11.0)", "Library (net9.0)", string.Empty),
            Row("whole review: a source shared by two units reviews both owners", true, "M:Shared/Common.cs", "Library (net9.0);Other (net11.0)", string.Empty, string.Empty),
        };
    }

    private static TheoryDataRow<bool, string, string, string, string> Row(string name, bool isWholeReview, string changes, string reviewed, string builtOnly, string concerns)
    {
        return new TheoryDataRow<bool, string, string, string, string>(isWholeReview, changes, reviewed, builtOnly, concerns) { TestDisplayName = name };
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public void AffectedUnitsFollowChangedInputs(bool isWholeReview, string changes, string reviewed, string builtOnly, string concerns)
    {
        var graph = new SelectorGraph(Path.Combine(TestPaths.Temp, "cf-selector"));

        var plan = graph.Select(isWholeReview, changes.Split('|'));

        var reviewedLabels = plan.Order.Where(unit => plan.ReviewedKeys.Contains(unit.Key)).Select(unit => unit.Label).Order(StringComparer.Ordinal).ToList();
        var builtOnlyLabels = plan.Order.Where(unit => !plan.ReviewedKeys.Contains(unit.Key)).Select(unit => unit.Label).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(Split(reviewed), reviewedLabels);
        Assert.Equal(Split(builtOnly), builtOnlyLabels);
        Assert.Empty(plan.NotCovered);
        foreach(var concern in Split(concerns))
        {
            var parts = concern.Split('=');
            Assert.Equal(bool.Parse(parts[1]), ChangeSelector.IsConcern(ToolchainPaths.Combine(graph.Tree, parts[0]), graph.ProjectFolders));
        }
    }

    [Fact]
    public void SarifPreservesSeverityLocationAndSuppression()
    {
        var graph = new SelectorGraph(Path.Combine(TestPaths.Temp, "cf-selector"));
        var unit = graph.Graph.Units.First(candidate => candidate.Label == "Consumer (net11.0)");
        var use = ToolchainPaths.Combine(graph.Tree, "Consumer/Use.cs");
        var text = new SarifLogText()
            .Rule("CS0103", "error", true)
            .Rule("IDE0044", "suggestion", false)
            .Result("CS0103", "error", "The name 'x' does not exist.", use, 7, 9)
            .Result("IDE0044", "error", "Make field readonly.", use, 3, 5)
            .Result("CA1050", "warning", "Declare types in namespaces.", use, 1, 1)
            .Result("IDE0051", "note", "Remove the unused member.", use, 5, 5)
            .Result("IDE0060", "warning", "Remove the unused parameter.", null, 0, 0)
            .Result("CA1822", "warning", "Mark members as static.", use, 9, 9, true)
            .Closing("Consumer", 1)
            .ToString();
        var run = JsonSerializer.Deserialize(text, SarifJsonContext.Default.SarifLogDto)!.Runs![0];

        var findings = new SarifFindingBuilder(graph.Tree).Build(run, unit);

        Assert.Equal(new[] { "CS0103", "IDE0044", "CA1050", "IDE0051", "IDE0060" }, findings.Select(finding => finding.RuleId));
        Assert.Equal(new[] { Severity.Block, Severity.Block, Severity.Warn, Severity.Warn, Severity.Warn }, findings.Select(finding => finding.Severity));
        Assert.All(findings, finding => Assert.Equal("Consumer", finding.Project));
        Assert.All(findings, finding => Assert.Equal("net11.0", finding.TargetFramework));
        Assert.Equal(new[] { 7, 3, 1, 5 }, findings.Where(finding => finding.File != null).Select(finding => finding.Line!.Value));
        Assert.All(findings.Where(finding => finding.File != null), finding => Assert.Equal("Consumer/Use.cs", finding.File));
        Assert.Null(findings[4].File);
        Assert.Null(findings[4].Line);

        // a compiler error that is an error by default belongs to the build as a whole: no changed line is needed to keep it
        Assert.Equal(FindingSource.Compiler, findings[0].Source);
        Assert.Null(findings[0].Reach);
        Assert.Equal(FindingSource.EditorConfig, findings[1].Source);
        Assert.NotNull(findings[1].Reach);
    }

    private static List<string> Split(string text)
    {
        return text.Length == 0 ? new List<string>() : text.Split(';').Order(StringComparer.Ordinal).ToList();
    }
}
