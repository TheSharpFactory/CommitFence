using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Review;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// How what git wrote about a change becomes the lines a review judges: which declarations the change reaches, a diff
/// that holds lines that look like headers, and a file git took for binary that is read again as text.
/// </summary>
public sealed class DiffReadingTests
{
    private const string _documentedMethod = """
        /// <summary>
        /// Says it.
        /// </summary>
        public int Documented()
        {
            return 1;
        }
        """;

    private const string _undocumentedMethod = """
        public int Documented()
        {
            return 1;
        }
        """;

    private const string _otherBad = """
        public int Other(int y)
        {
            if(y > 0)
            {
                return 1;
            }

            return 0;
        }
        """;

    [Theory]
    [InlineData("ContainingTypeHeaderChanges")]
    [InlineData("DocumentationIsDeleted")]
    [InlineData("MethodBodyChangesBesideAnUntouchedSibling")]
    [InlineData("DocumentedMethodIsInsertedAfterABlankLine")]
    public void ReviewReportsOnlyDeclarationsReachedByTheChange(string scenario)
    {
        using(var harness = new ReviewHarness())
        {
            var expected = Arrange(harness, scenario);

            var result = harness.Service().ReviewChanges(harness.RepositoryFolder, ReviewScope.ForRange("old", "new"), null);

            Assert.Equal(expected, result.Findings.OrderBy(finding => finding.RuleId, StringComparer.Ordinal).ThenBy(finding => finding.Line).Select(finding => (finding.RuleId, finding.File, finding.Line)).ToList());
            Assert.Equal(expected.Count == 0 ? ReviewOutcome.Clean : ReviewOutcome.Blocked, result.Outcome);
        }
    }

    [Fact]
    public void HeaderLikeSourceInsideHunkKeepsItsFileAndRange()
    {
        var diff = string.Join('\n', "diff --git a/src/Foo.cs b/src/Foo.cs", "index 1111111..2222222 100644", "--- a/src/Foo.cs", "+++ b/src/Foo.cs", "@@ -3 +3 @@", "--- an old line that starts with two dashes", "+++ a new line that starts with two pluses", "@@ -10 +10 @@", "-before", "+after", string.Empty);

        var files = UnifiedDiffParser.Parse(diff, path => "csharp");

        var file = Assert.Single(files);
        Assert.Equal("src/Foo.cs", file.Path);
        Assert.Equal(ChangeStatus.Modified, file.Status);
        Assert.Equal(new[] { (3, 3), (10, 10) }, file.ChangedLines.Select(range => (range.StartLine, range.EndLine)).ToArray());
    }

    [Fact]
    public void TextRetryKeepsRenameAndTouchedLines()
    {
        const string destination = "src b/Foo.cs";
        var oldText = ReviewSources.Class("Foo", ReviewSources.SignBad);
        var newText = ReviewSources.Class("Foo", ReviewSources.SignBad, _otherBad);
        using(var harness = new ReviewHarness())
        {
            harness.Fake.AddCommit("old", new Dictionary<string, string> { ["commitfence.json"] = ReviewSources.Settings(ReviewSources.Team010), ["old/Foo.cs"] = oldText, ["Foo.cs"] = oldText });
            harness.Fake.AddCommit("new", new Dictionary<string, string> { ["commitfence.json"] = ReviewSources.Settings(ReviewSources.Team010), [destination] = newText, ["Foo.cs"] = oldText }, "old");
            harness.Fake.DiffAnswer = (before, after) => string.Join('\n', "diff --git a/old/Foo.cs b/src b/Foo.cs", "similarity index 90%", "rename from old/Foo.cs", "rename to " + destination, "Binary files a/old/Foo.cs and b/src b/Foo.cs differ", string.Empty);
            harness.Fake.TextDiffAnswer = names => "diff --git a/old/Foo.cs b/src b/Foo.cs\nsimilarity index 90%\nrename from old/Foo.cs\nrename to " + destination + "\n--- a/old/Foo.cs\n+++ b/" + destination + "\n" + HunksOf(oldText, newText);

            var result = harness.Service().ReviewChanges(harness.RepositoryFolder, ReviewScope.ForRange("old", "new"), null);

            var request = Assert.Single(harness.Fake.TextRequests);
            Assert.Equal(new[] { destination, "old/Foo.cs" }, Assert.Single(request));
            var finding = Assert.Single(result.Findings);
            Assert.Equal("TEAM010", finding.RuleId);
            Assert.Equal(destination, finding.File);
            Assert.Equal(ReviewSources.LineOf(newText, "if(y > 0)"), finding.Line);
        }
    }

    private static string HunksOf(string oldText, string newText)
    {
        var diff = UnifiedDiffMaker.Between(new Dictionary<string, string> { ["x"] = oldText }, new Dictionary<string, string> { ["x"] = newText });
        return diff.Substring(diff.IndexOf("@@", StringComparison.Ordinal));
    }

    /// <summary>
    /// Builds the repository of the scenario and returns the findings the change reaches: the rule, the file and the line.
    /// </summary>
    private static List<(string RuleId, string? File, int? Line)> Arrange(ReviewHarness harness, string scenario)
    {
        switch(scenario)
        {
            case "ContainingTypeHeaderChanges":
                return ArrangeHeader(harness);
            case "DocumentationIsDeleted":
                AddChange(harness, ReviewSources.Documented, ReviewSources.Class("Doc", _documentedMethod), ReviewSources.Class("Doc", _undocumentedMethod));
                return new List<(string, string?, int?)> { ("DOC1", "src/Doc.cs", ReviewSources.LineOf(ReviewSources.Class("Doc", _undocumentedMethod), "public int Documented")) };
            case "MethodBodyChangesBesideAnUntouchedSibling":
                return ArrangeBody(harness);
            default:
                return ArrangeInsertion(harness);
        }
    }

    private static List<(string RuleId, string? File, int? Line)> ArrangeHeader(ReviewHarness harness)
    {
        const string protectedMethod = """
            /// <summary>
            /// The size.
            /// </summary>
            protected int Size()
            {
                return 1;
            }
            """;
        var before = ReviewSources.ClassWith("public class Box", protectedMethod);
        var after = ReviewSources.ClassWith("internal class Box", protectedMethod);
        var line = ReviewSources.LineOf(after, "protected int Size");
        var declarations = new List<MeaningDeclaration>
        {
            new MeaningDeclaration("src/Doc.cs", new SourcePoint(3, 1), "class", "Box", "Demo", "internal", string.Empty),
            new MeaningDeclaration("src/Doc.cs", new SourcePoint(line, 5), "method", "Size", "Demo", "protected", "internal")
        };
        var answers = new MeaningAnswers("csharp", declarations, new List<MeaningUse>(), new List<string>(), new List<string>(), new List<string>());
        var toolchain = new FakeToolchain { Answer = request => new ToolchainResult(new List<CF.Core.Findings.Finding>(), new List<CF.Core.Findings.Coverage>(), new List<string>(), answers, new List<FileReading>()) };
        harness.Languages = new List<ILanguage> { new TestLanguage(toolchain, new FakeMeaningSource(answers)) };
        AddChange(harness, string.Join(",", ReviewSources.Team011Written, ReviewSources.Team011), before, after);

        return new List<(string, string?, int?)> { ("TEAM011", "src/Doc.cs", line), ("TEAM011S", "src/Doc.cs", line) };
    }

    private static List<(string RuleId, string? File, int? Line)> ArrangeBody(ReviewHarness harness)
    {
        var before = ReviewSources.Class("Wide", MethodReturning("First", 1), MethodReturning("Second", 2));
        var after = ReviewSources.Class("Wide", MethodReturning("First", 11), MethodReturning("Second", 2));
        AddChange(harness, ReviewSources.Documented, before, after);

        return new List<(string, string?, int?)> { ("DOC1", "src/Doc.cs", ReviewSources.LineOf(after, "public int First")) };
    }

    private static List<(string RuleId, string? File, int? Line)> ArrangeInsertion(ReviewHarness harness)
    {
        const string inserted = """
            /// <summary>
            /// A new method above the old ones.
            /// </summary>
            public int Zero()
            {
                return 0;
            }
            """;
        var before = ReviewSources.Class("Wide", MethodReturning("First", 1), MethodReturning("Second", 2));
        var after = ReviewSources.Class("Wide", inserted, MethodReturning("First", 1), MethodReturning("Second", 2));
        AddChange(harness, ReviewSources.Documented, before, after);

        return new List<(string, string?, int?)>();
    }

    private static string MethodReturning(string name, int value)
    {
        return $"public int {name}()\n{{\n    return {value};\n}}";
    }

    private static void AddChange(ReviewHarness harness, string rules, string before, string after)
    {
        var settings = "{ \"rules\": [" + rules + "] }\n";
        harness.Fake.AddCommit("old", new Dictionary<string, string> { ["commitfence.json"] = settings, ["src/Doc.cs"] = before });
        harness.Fake.AddCommit("new", new Dictionary<string, string> { ["commitfence.json"] = settings, ["src/Doc.cs"] = after }, "old");
    }
}
