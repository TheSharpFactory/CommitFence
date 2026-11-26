using CF.Adapters.Machine;
using CF.Adapters.Storage;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CF.Languages.CSharp;
using CF.Languages.CSharp.Reader;
using CF.Tests.Rules.Support;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Rules;

/// <summary>Checks comparison semantics and the ways a team reads, edits and runs new rule conditions.</summary>
public sealed class RuleConditionsTests
{
    private static readonly string[] _parameterBounds = { "parameterCount is at least 1", "parameterCount is at most 5" };

    [Theory]
    [InlineData("--require", "parameterCount>=1", "parameterCount!=3", "move one to where.")]
    [InlineData("--require", "parameterCount>=1", "parameterCount>=2", "move one to where.")]
    [InlineData("--require", "parameterCount=1", "parameterCount<=5", "move one to where.")]
    [InlineData("--where", "parameterCount<=5", "parameterCount<=6", "move one to require.")]
    public void RefusesTwoConditionsOnOnePropertyThatARangeDoesNotHold(string clause, string first, string second, string ending)
    {
        using var fixture = new RulesFixture("""{"rules":[]}""");
        var before = fixture.SettingsBytes();

        var result = fixture.Commands.Run("rules", "add", "LIMIT", "--for", "method", "--title", "Parameter count", clause, first, clause, second);

        Assert.Equal(ExitCode.BadSettingsOrUsage, result.ExitCode);
        Assert.Contains("A clause can hold one condition per property, or \"is at least\" with \"is at most\" as a range. Join the other conditions on parameterCount into one, or " + ending, result.Error, StringComparison.Ordinal);
        Assert.Equal(before, fixture.SettingsBytes());
    }

    [Theory]
    [InlineData(Comparison.Is, "one", "one", true)]
    [InlineData(Comparison.IsOneOf, "one,two", "two", true)]
    [InlineData(Comparison.IsNot, "one,two", "three", true)]
    [InlineData(Comparison.IsNot, "one,two", "two", false)]
    [InlineData(Comparison.AtMost, "40", "40", true)]
    [InlineData(Comparison.AtMost, "40", "41", false)]
    [InlineData(Comparison.AtLeast, "1", "1", true)]
    [InlineData(Comparison.AtLeast, "1", "0", false)]
    [InlineData(Comparison.Matches, "*Controller", "OrderController", true)]
    [InlineData(Comparison.Matches, "*Controller", "Ordercontroller", false)]
    [InlineData(Comparison.Matches, "*Controller", "OrderControllerTests", false)]
    [InlineData(Comparison.Matches, "a?c", "a.c", true)]
    [InlineData(Comparison.Matches, "a?c", "ac", false)]
    [InlineData(Comparison.Matches, "a.?", "abc", false)]
    [InlineData(Comparison.Matches, "**", "", true)]
    [InlineData(Comparison.Matches, "ab**?d*e", "ab1cd2e", true)]
    [InlineData(Comparison.Matches, "*a??b*c?d*", "aaabccdx", true)]
    [InlineData(Comparison.Matches, "*a??b*c?d*", "aaabccx", false)]
    [InlineData(Comparison.Matches, "*01234567890123456789012345678901234567890123456789012345678901234567?x*", "prefix01234567890123456789012345678901234567890123456789012345678901234567yx", true)]
    public void ComparesValues(Comparison comparison, string operands, string actual, bool expected)
    {
        var values = operands.Split(',').Select(operand => Value(comparison, operand)).ToList();
        var condition = new RuleCondition("property", comparison, values);

        Assert.Equal(expected, condition.IsMetBy(Value(comparison, actual)));
    }

    [Theory]
    [InlineData(""" "lines": 40 """, "lines is 40", null)]
    [InlineData(""" "lines": [3, 40] """, "lines is one of 3, 40", null)]
    [InlineData(""" "lines": { "atMost": 40 } """, "lines is at most 40", null)]
    [InlineData(""" "lines": { "atLeast": 1, "atMost": 40 } """, "lines is at least 1|lines is at most 40", null)]
    [InlineData(""" "declaredAccess": { "matches": "pub*" } """, "declaredAccess matches pub*", null)]
    [InlineData(""" "name": { "not": "Run" } """, "name is not Run", null)]
    [InlineData(""" "name": { "not": ["Run"] } """, "name is none of Run", null)]
    [InlineData(""" "declaredAccess": { "not": ["internal", "protected"] } """, "declaredAccess is none of internal, protected", null)]
    [InlineData(""" "lines": { "below": 40 } """, "is not known", "\"below\"")]
    [InlineData(""" "lines": { "atMost": 40, "atMost": 20 } """, "given twice", "\"atMost\"")]
    [InlineData(""" "name": { "atMost": 40 } """, "cannot use 'atMost'", "\"atMost\"")]
    [InlineData(""" "isOverride": { "not": false } """, "cannot use 'not'", "\"not\"")]
    [InlineData(""" "lines": { "atMost": 4, "atLeast": 5 } """, "'atLeast' above 'atMost'", "\"atLeast\"")]
    [InlineData(""" "name": { "not": [] } """, "is empty", "\"not\"")]
    [InlineData(""" "name": { "matches": 40 } """, "takes text", "\"matches\"")]
    [InlineData(""" "lines": { "atMost": [40] } """, "takes a whole number", "\"atMost\"")]
    [InlineData(""" "lines": { "not": 3, "atMost": 8 } """, "Give one comparison", "\"lines\"")]
    [InlineData(""" "name": {} """, "Give one comparison", "\"name\"")]
    public void ReadsConditionFormsAndLocatesRefusals(string entry, string expected, string? invalidKey)
    {
        const string prefix = "      ";
        var text = """
            {"rules":[{"id":"R","language":"csharp","for":"method","severity":"block","title":"Rule","require":{
            """ + "\n" + prefix + entry + "\n}}]}";
        if(invalidKey == null)
        {
            var rule = Assert.Single(RulesFixture.Parse(text).Rules);
            Assert.Equal(expected.Split('|'), rule.Require.Select(RuleSentence.Condition));
        }
        else
        {
            var error = Assert.Throws<SettingsException>(() => RulesFixture.Parse(text));
            Assert.Contains(expected, error.Message, StringComparison.Ordinal);
            Assert.Equal(2, error.Line);
            Assert.Equal(prefix.Length + entry.LastIndexOf(invalidKey, StringComparison.Ordinal) + 1, error.Column);
        }
    }

    [Fact]
    public void WritesBothBoundsAsOnePropertyAndReadsThemAfterAnEdit()
    {
        using var fixture = new RulesFixture("""{"rules":[]}""");
        var commands = fixture.Commands;

        var added = commands.Run("rules", "add", "LIMIT", "--for", "method", "--title", "Parameter count",
            "--where", "name~*Controller", "--require", "parameterCount>=1", "--require", "parameterCount<=5");
        Assert.Equal(ExitCode.Passed, added.ExitCode);
        Assert.Contains("\"parameterCount\": { \"atLeast\": 1, \"atMost\": 5 }", fixture.SettingsText(), StringComparison.Ordinal);

        var changed = commands.Run("rules", "set", "LIMIT", "--where", "name!=Test,Demo");
        Assert.Equal(ExitCode.Passed, changed.ExitCode);
        var rule = Assert.Single(fixture.Settings().Rules);
        Assert.Equal("name is none of Test, Demo", RuleSentence.Condition(Assert.Single(rule.Where)));
        Assert.Equal(_parameterBounds, rule.Require.Select(RuleSentence.Condition));
        Assert.Contains("parameter count is at least 1 and parameter count is at most 5", commands.Run("rules", "list").Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Order.cs", "class Order<T> {}", "class", "isNamedLikeFile", "true")]
    [InlineData("Order.Tests.cs", "class Order {}", "class", "isNamedLikeFile", "false")]
    [InlineData("Order.cs", "interface IOrder { class Order {} }", "class", "isNested", "true")]
    [InlineData("Order.cs", "namespace Orders; class Order {}", "class", "isNested", "false")]
    [InlineData("Order.cs", "class Order { void Run() { return; } }", "method", "nestingDepth", "0")]
    [InlineData("Order.cs", "class Order { void Run() { if(a) {} else if(b) {} else while(c) Run(); } }", "method", "nestingDepth", "2")]
    [InlineData("Order.cs", "class Order { void Run() { try {} catch { while(a) Run(); } finally {} } }", "method", "nestingDepth", "2")]
    [InlineData("Order.cs", "class Order { void Run() { F(() => { if(a) while(b) Run(); }); void Local() { if(a) while(b) Run(); } } }", "method", "nestingDepth", "0")]
    [InlineData("Order.Tests.cs", "// header\n\nnamespace Orders; record Order; interface IOrder {}", "file", "name", "Order.Tests.cs")]
    [InlineData("Order.cs", "namespace A { class Order { record Nested; } namespace B { record Test; } } struct S {} interface I {} enum E {} delegate void D();", "file", "typesInFile", "6")]
    [InlineData("Order.cs", "// no declarations", "file", "typesInFile", "0")]
    public void ReadsNewCSharpPropertiesAndFindsFileRulesAtLineOne(string name, string source, string type, string property, string expected)
    {
        var file = new CodeFile("src/" + name, "csharp", source);
        var artifacts = new CSharpCodeReader().Read(file);
        var artifact = Assert.Single(artifacts, candidate => candidate.Type == type);

        Assert.Equal(expected, artifact.FindProperty(property)?.Value.ToString());
        if(type == "file")
        {
            var rule = Assert.Single(RulesFixture.Parse("""{"rules":[{"id":"F","language":"csharp","for":"file","severity":"warn","title":"File rule","forbid":true}]}""").Rules);
            var finding = Assert.Single(RuleEvaluator.Evaluate(rule, artifacts));
            Assert.Equal(1, finding.Line);
            Assert.Single(ReachFilter.Keep([finding], new ChangedFile(file.Path, file.Language, ChangeStatus.Modified, [LineRange.ForLines(3, 3)])));
            Assert.Single(ReachFilter.Keep([finding], new ChangedFile(file.Path, file.Language, ChangeStatus.Modified, [LineRange.ForRemovalAfter(0)])));
            Assert.Empty(ReachFilter.Keep([finding], new ChangedFile(file.Path, file.Language, ChangeStatus.Modified, [])));
        }
    }

    [Theory]
    [InlineData("", "method", "lines<=40", "1.0.0.6")]
    [InlineData(""" "requires": "1.0.0.5", """, "method", "nestingDepth=2", "1.0.0.6")]
    [InlineData("", "class", "isNested=false", "1.0.0.6")]
    [InlineData("", "class", "isNamedLikeFile=true", "1.0.0.6")]
    [InlineData("", "file", "name=Order.cs", "1.0.0.6")]
    [InlineData(""" "requires": "2.0", """, "file", "typesInFile<=1", "2.0")]
    [InlineData("", "method", "lines=40", null)]
    public void WritingNewFeaturesRaisesTheVersionFloorOnlyWhenNeeded(string header, string type, string condition, string? requires)
    {
        using var fixture = new RulesFixture("{" + header + """ "rules": [] }""");

        var added = fixture.Commands.Run("rules", "add", "R", "--for", type, "--title", "Rule", "--require", condition);

        Assert.Equal(ExitCode.Passed, added.ExitCode);
        Assert.Equal(requires, fixture.Settings().Requires);
        Assert.Equal(ExitCode.Passed, fixture.Commands.Run("rules", "remove", "R").ExitCode);
        Assert.Equal(requires, fixture.Settings().Requires);
    }

    [Theory]
    [InlineData("class", """ "require":{"isNamedLikeFile":true} """)]
    [InlineData("file", """ "where":{"name":{"matches":"*.cs"}},"require":{"typesInFile":{"atLeast":1}} """)]
    public void TryRuleNeedsAFileNameForConditionsThatReadIt(string artifact, string conditions)
    {
        const string source = "class Order {}";
        const string refusal = "This rule reads the file's name, so it cannot be tried on pasted code. Try it on a file with commitfence rules try NAME <file>.";
        using var fixture = new RulesFixture($$"""{"rules":[{"id":"NAME","language":"csharp","for":"{{artifact}}","severity":"block","title":"File name",{{conditions}}}]}""");
        var data = fixture.Commands.DataFolder;
        var files = new SystemFileSystem();
        var languages = new List<ILanguage> { new CSharpLanguage(new FakeDotnet(fixture.Folder), files, data) };
        var service = new RuleService(files, new FileSettingsLock(data), languages);

        var fromPaste = service.TryRule(fixture.SettingsPath, "NAME", source, null);
        var fromFile = service.TryRule(fixture.SettingsPath, "NAME", source, "src/Order.cs");

        Assert.Equal(TryStatus.NeedsFileName, fromPaste.Status);
        Assert.Empty(fromPaste.Verdicts);
        Assert.Equal(refusal, fromPaste.Reason);
        Assert.Equal(TryStatus.Tried, fromFile.Status);
        Assert.Equal(TryOutcome.FollowsIt, Assert.Single(fromFile.Verdicts).Outcome);
    }

    private static PropertyValue Value(Comparison comparison, string text)
    {
        return comparison is Comparison.AtLeast or Comparison.AtMost
            ? PropertyValue.FromNumber(long.Parse(text, System.Globalization.CultureInfo.InvariantCulture))
            : PropertyValue.FromText(text);
    }
}
