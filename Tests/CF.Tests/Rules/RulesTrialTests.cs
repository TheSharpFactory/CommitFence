using CF.Core.Review;
using CF.Core.Rules.Settings;
using CF.Core.Structure;
using CF.Tests.Rules.Support;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Rules;

/// <summary>
/// What the catalog, the trial of a rule, the reading of the settings file and the patterns of a rule say.
/// </summary>
public sealed class RulesTrialTests
{
    private const string _member = """
        public void Send(Invoice invoice)
        {
            if(invoice.Paid) { return; }

            if(invoice.Late)
                return;

            if(invoice.Count > 0)
            {
                invoice.Count = 0;
                return;
            }
        }

        """;

    private const string _statements = "        if(a) { return; }\n        return;\n";

    private const string _taken = "namespace Sample;\n\npublic class Taken\n{\n    public int Run(int v)\n    {\n#if !FEATURE\n        return v;\n#endif\n    }\n}\n";

    private const string _hidden = "namespace Sample;\n\npublic class Hidden\n{\n    public int Run(int v)\n    {\n#if FEATURE\n        if(v > 0) { return 1; }\n#endif\n        return 0;\n    }\n}\n";

    private const string _visible = "namespace Sample;\n\npublic class Visible\n{\n    public int Run(int v)\n    {\n        if(v > 0) { return 1; }\n\n        return 0;\n    }\n}\n";

    public static TheoryData<bool, string, string> Trials { get; } = new TheoryData<bool, string, string>
    {
        new TheoryDataRow<bool, string, string>(false, _member, "stdin(3,5): breaks it|stdin(5,5): follows it|stdin(8,5): not covered|1 breaks it, 1 follows it, 1 not covered.") { TestDisplayName = "a member from standard input, with all three outcomes" },
        new TheoryDataRow<bool, string, string>(true, _statements, "Snippet.cs(1,9): breaks it|1 breaks it, 0 follows it, 0 not covered.") { TestDisplayName = "indented statements from a file, at their own column" }
    };

    public static TheoryData<string, string, string> Unavailable { get; } = new TheoryData<string, string, string>
    {
        new TheoryDataRow<string, string, string>("TEAM010", "void A() { if(x) { } ", "does not parse|(1,") { TestDisplayName = "an unclosed method" },
        new TheoryDataRow<string, string, string>("TEAM012", "void A() { }", "cannot be tried without a build") { TestDisplayName = "a rule that needs a build" }
    };

    public static TheoryData<string, string, string> Unreadable { get; } = new TheoryData<string, string, string>
    {
        new TheoryDataRow<string, string, string>("{ \"rules\": [ { \"id\": \"A\"\n \"language\": \"csharp\" } ] }", "\"language\"", "not valid JSON") { TestDisplayName = "malformed JSON" },
        new TheoryDataRow<string, string, string>("{\n    \"rules\": [\n        {\n            \"id\": \"A\",\n            \"colour\": \"red\"\n        }\n    ]\n}\n", "\"colour\"", "'colour' is not known in a rule") { TestDisplayName = "an unknown member of a rule" },
        new TheoryDataRow<string, string, string>("{\n    \"rules\": [\n        {\n            \"id\": \"A\",\n            \"paths\": [ null ]\n        }\n    ]\n}\n", "null", "is null") { TestDisplayName = "a null in a list" }
    };

    private static void AssertPlace(string text, string marker, SettingsException exception)
    {
        var offset = text.IndexOf(marker, StringComparison.Ordinal);
        var line = text.Substring(0, offset).Count(character => character == '\n') + 1;
        var column = offset - (text.LastIndexOf('\n', offset) + 1) + 1;
        Assert.Equal(line, exception.Line);
        Assert.Equal(column, exception.Column);
    }

    [Fact]
    public void CatalogDescribesTheRequestedArtifact()
    {
        using(var fixture = new RulesFixture("{ \"rules\": [] }"))
        {
            var result = fixture.Commands.Run("catalog", "csharp", "method");

            Assert.Equal(ExitCode.Passed, result.ExitCode);
            var lines = result.Output.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            var isOverride = Assert.Single(lines, line => line.TrimStart().StartsWith("isOverride", StringComparison.Ordinal));
            Assert.Contains("true or false", isOverride, StringComparison.Ordinal);
            Assert.Contains("reads the file", isOverride, StringComparison.Ordinal);
            var declaredAccess = Assert.Single(lines, line => line.TrimStart().StartsWith("declaredAccess", StringComparison.Ordinal));
            Assert.Contains("text", declaredAccess, StringComparison.Ordinal);
            Assert.Contains("needs the build", declaredAccess, StringComparison.Ordinal);
            Assert.DoesNotContain("if-statement", result.Output, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(Trials))]
    public void TrialReportsVerdictsAtOriginalPositions(bool fromFile, string code, string expected)
    {
        using(var fixture = RulesFixture.Canonical(RulesFixture.BracedIfRule))
        {
            var before = fixture.SettingsBytes();
            fixture.Write("Snippet.cs", code);

            var result = fromFile ? fixture.Commands.Run("rules", "try", "TEAM010", "Snippet.cs") : fixture.Commands.RunWithInput(code, "rules", "try", "TEAM010");

            Assert.Equal(ExitCode.Passed, result.ExitCode);
            foreach(var part in expected.Split('|'))
                Assert.Contains(part, result.Output, StringComparison.Ordinal);

            if(!fromFile)
            {
                Assert.Contains("braces is true", result.Output, StringComparison.Ordinal);
                Assert.Contains("statementsInBody is 2", result.Output, StringComparison.Ordinal);
            }

            Assert.Equal(before, fixture.SettingsBytes());
        }
    }

    [Theory]
    [MemberData(nameof(Unavailable))]
    public void UnavailableTrialReturnsIncomplete(string ruleId, string code, string expected)
    {
        using(var fixture = RulesFixture.Canonical(RulesFixture.BracedIfRule, RulesFixture.ClockRule))
        {
            var result = fixture.Commands.RunWithInput(code, "rules", "try", ruleId);

            Assert.Equal(ExitCode.Incomplete, result.ExitCode);
            foreach(var part in expected.Split('|'))
                Assert.Contains(part, result.Output, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(Unreadable))]
    public void SettingsReaderLocatesTheInvalidToken(string text, string marker, string expectedMessage)
    {
        var exception = Assert.Throws<SettingsException>(() => RulesFixture.Parse(text));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        AssertPlace(text, marker, exception);
    }

    [Fact]
    public void RulePatternsSelectTheIntendedFiles()
    {
        var files = new[] { "src/A.cs", "src/deep/B.cs", "other/C.cs", "srcOther/D.cs" };

        var recursive = files.Where(file => PathFilter.Matches(new[] { "src/**/*.cs" }, file)).ToList();
        var direct = files.Where(file => PathFilter.Matches(new[] { "src/*.cs" }, file)).ToList();

        Assert.Equal(new[] { "src/A.cs", "src/deep/B.cs" }, recursive);
        Assert.Equal(new[] { "src/A.cs" }, direct);
    }

    [Fact]
    public void UnreadConditionalCodeChangesCoverageAndVerdict()
    {
        using(var fixture = RulesFixture.Canonical(RulesFixture.BracedIfRule))
        {
            fixture.Write("src/Taken.cs", _taken);
            fixture.Write("src/Hidden.cs", _hidden);
            fixture.Write("src/Visible.cs", _visible);

            // a conditional section whose one branch is the one that is read leaves nothing unread
            var taken = fixture.Commands.Run("review", "--files", "src/Taken.cs");
            Assert.Equal(ExitCode.Passed, taken.ExitCode);
            Assert.DoesNotContain("read in part", taken.Output, StringComparison.Ordinal);

            // a breach behind a symbol is not seen, so a pass would be a pass nobody earned
            var hidden = fixture.Commands.Run("review", "--files", "src/Hidden.cs");
            Assert.Equal(ExitCode.Incomplete, hidden.ExitCode);
            Assert.Contains("src/Hidden.cs: incomplete: read in part", hidden.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("TEAM010", hidden.Output, StringComparison.Ordinal);
            Assert.Contains("0 of 1 files reviewed", hidden.Output, StringComparison.Ordinal);

            // a blocking finding elsewhere decides the verdict, and the partial file is still named
            var both = fixture.Commands.Run("review", "--files", "src/Hidden.cs", "src/Visible.cs");
            Assert.Equal(ExitCode.Blocked, both.ExitCode);
            Assert.Contains("src/Visible.cs(7,9): error TEAM010", both.Output, StringComparison.Ordinal);
            Assert.Contains("src/Hidden.cs: incomplete: read in part", both.Output, StringComparison.Ordinal);
            Assert.Contains("1 of 2 files reviewed", both.Output, StringComparison.Ordinal);
        }
    }
}
