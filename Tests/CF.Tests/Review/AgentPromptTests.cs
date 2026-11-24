using CF.Core.Findings;
using CF.Core.Prompts;
using CF.Core.Review;
using CF.Tests.Rules.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// The text handed to an AI coding agent: the writer in the body, and the command line that prints it.
/// </summary>
public sealed class AgentPromptTests
{
    private const string _opening = "CommitFence, the code review that runs before a push in this repository, found ";
    private const string _closing = "Change the code so that it follows the rule, and change nothing else. Do not silence the rule: no pragma, no allow comment, no change to .editorconfig or commitfence.json.";
    private const string _visible = "namespace Sample;\n\npublic class Visible\n{\n    public int Run(int v)\n    {\n        if(v > 0) { return 1; }\n\n        return 0;\n    }\n}\n";

    [Theory]
    [InlineData("team rule", true, true, true)]
    [InlineData("file rule", false, true, true)]
    [InlineData("no file", false, false, false)]
    [InlineData("nothing found", false, false, false)]
    public void ThePromptHasOnlyThePartsTheFindingHas(string kind, bool hasSentence, bool hasWhere, bool hasCode)
    {
        if(kind == "nothing found")
        {
            var none = new List<Finding>();
            var noSources = new Dictionary<string, string>();
            var noRules = new Dictionary<string, PromptRule>();
            Assert.Equal("CommitFence found nothing to fix.", AgentPrompt.ForReview(none, noSources, noRules, false));
            Assert.Equal("CommitFence found nothing to fix, but it could not check everything.", AgentPrompt.ForReview(none, noSources, noRules, true));
            return;
        }

        var source = string.Join("\n", Enumerable.Range(1, 20).Select(number => "code of line " + number));
        var file = kind == "no file" ? null : "src/Billing/Totals.cs";
        var finding = new Finding("TEAM010", kind == "file rule" ? FindingSource.EditorConfig : FindingSource.CodeRule, Severity.Block, "csharp", null, null, file, file == null ? null : 12, 1, "No braces here", null, null);
        var rule = kind == "team rule" ? new PromptRule("No braces", "For if-statement, require braces is false.") : new PromptRule("No braces", null);

        var prompt = AgentPrompt.ForFinding(finding, rule, source);

        Assert.StartsWith(_opening + "this. Fix it.\n\nTEAM010: No braces\n", prompt, StringComparison.Ordinal);
        Assert.EndsWith("\n\n" + _closing, prompt, StringComparison.Ordinal);
        Assert.Contains("It blocks a push.\n", prompt, StringComparison.Ordinal);
        Assert.Contains("What was found: No braces here", prompt, StringComparison.Ordinal);
        Assert.Equal(hasSentence, prompt.Contains("The rule: For if-statement, require braces is false.\n", StringComparison.Ordinal));
        Assert.Equal(hasWhere, prompt.Contains("Where: src/Billing/Totals.cs, line 12\n", StringComparison.Ordinal));
        Assert.Equal(hasCode, prompt.Contains("The code (the line is marked):\n    10 | code of line 10\n    11 | code of line 11\n>   12 | code of line 12\n    13 | code of line 13\n    14 | code of line 14\n", StringComparison.Ordinal));
        Assert.Equal(hasSentence, prompt.Contains("The rule:", StringComparison.Ordinal));
        Assert.Equal(hasWhere, prompt.Contains("Where:", StringComparison.Ordinal));
        Assert.Equal(hasCode, prompt.Contains("The code", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatPromptPrintsThePromptAloneAndAnUnknownFormatIsRefused()
    {
        using(var fixture = RulesFixture.Canonical(RulesFixture.BracedIfRule))
        {
            fixture.Write("src/Visible.cs", _visible);

            var report = fixture.Commands.Run("review", "--files", "src/Visible.cs");
            var prompt = fixture.Commands.Run("review", "--files", "src/Visible.cs", "--format", "prompt");
            var unknown = fixture.Commands.Run("review", "--files", "src/Visible.cs", "--format", "json");

            Assert.Equal(report.ExitCode, prompt.ExitCode);
            Assert.Equal(ExitCode.Blocked, prompt.ExitCode);
            Assert.Empty(prompt.Error);
            Assert.Equal(_opening + "1 thing in 1 file. Fix it.\n\n1 of 1\nTEAM010: No braces around a single-line if\nThe rule: For if statement, where statements in body is 1 and body fits on one line is true, require braces is false.\nIt blocks a push.\nWhere: src/Visible.cs, line 7\nWhat was found: No braces around a single-line if\n\nThe code (the line is marked):\n    5 |     public int Run(int v)\n    6 |     {\n>   7 |         if(v > 0) { return 1; }\n    8 | \n    9 |         return 0;\n\n" + _closing + "\n", prompt.Output.Replace(Environment.NewLine, "\n", StringComparison.Ordinal));
            Assert.Equal(ExitCode.BadSettingsOrUsage, unknown.ExitCode);
            Assert.Empty(unknown.Output);
            Assert.Contains("text", unknown.Error, StringComparison.Ordinal);
            Assert.Contains("prompt", unknown.Error, StringComparison.Ordinal);
        }
    }
}