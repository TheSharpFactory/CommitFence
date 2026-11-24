using System.Text;
using CF.Adapters.Storage;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Tests.Rules.Support;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Rules;

/// <summary>
/// The commands that change the rules of a team: a change reaches the next review, nothing unrelated is lost, and a change
/// that cannot be made leaves the file as it was.
/// </summary>
public sealed class RulesEditingTests
{
    private const string _three = """
        namespace Sample;

        public class Three
        {
            public int Run(int value)
            {
                if(value > 0) { return 1; }

                if(value < 0)
                    return -1;

                if(value == 0)
                {
                    value = 1;
                    return value;
                }

                return 0;
            }
        }

        """;

    private const string _methods = """
        public class Keys : KeysBase
        {
            protected override void OnKeyDown(int key)
            {
            }

            protected void Helper()
            {
            }
        }

        """;

    private const string _untidySettings = "{\"structure\":{\"directions\":{\"forbid\":[\"alpha -> beta\"],\"allow\":[\"beta -> alpha\"]},\"modules\":{\"alpha\":[\"namespace:Alpha\"],\"beta\":[\"namespace:Beta\",\"folder:src/beta/**\"]}},\r\n\"rules\":[{\"for\":\"if-statement\",\"id\":\"TEAM010\",\"language\":\"csharp\",\"severity\":\"block\",\"title\":\"No braces\",\"where\":{\"statementsInBody\":1},\"require\":{\"braces\":false}},\r\n{\"id\":\"TEAM011\",\"language\":\"csharp\",\"for\":\"method\",\"severity\":\"block\",\"title\":\"Tidy > ünïcode\",\"where\":{\"isOverride\":false},\"require\":{\"writtenAccess\":[\"public\",\"private\"]},\"paths\":[\"src/**\"],\"why\":\"because\"}],\r\n\"requires\":\"1.0.0.0\"}\r\n";

    public static TheoryData<string, string> InvalidEdits { get; } = new TheoryData<string, string>
    {
        new TheoryDataRow<string, string>("rules|disable|NOPE", "NOPE") { TestDisplayName = "disable a rule that is not there" },
        new TheoryDataRow<string, string>("rules|add|X1|--for|if-statement|--title|t|--require|braces=banana", "true or false") { TestDisplayName = "a value of the wrong type" },
        new TheoryDataRow<string, string>("rules|add|TEAM010|--for|if-statement|--title|t|--require|braces=true", "already used") { TestDisplayName = "an id that is taken" },
        new TheoryDataRow<string, string>("rules|set|TEAM010|--for|method", "statementsInBody") { TestDisplayName = "a type that breaks a condition of the rule" },
        new TheoryDataRow<string, string>("rules|copy|TEAM010|bad id", "is not a rule id") { TestDisplayName = "a copy under an id that is not an id" }
    };

    private static int LineOf(string text, string marker)
    {
        return Array.FindIndex(text.Split('\n'), line => line.Contains(marker, StringComparison.Ordinal)) + 1;
    }

    private static CommandResult Review(RulesFixture fixture, params string[] files)
    {
        return fixture.Commands.Run(new[] { "review", "--files" }.Concat(files).Concat(new[] { "--settings", "commitfence.json" }).ToArray());
    }

    private static List<string> Describe(IEnumerable<RuleCondition> conditions)
    {
        return conditions.Select(condition => condition.PropertyName + "=" + string.Join(",", condition.ExpectedValues)).ToList();
    }

    [Fact]
    public void SavedEditsChangeTheNextReview()
    {
        using(var fixture = RulesFixture.Canonical(RulesFixture.BracedIfRule))
        {
            fixture.Write("Three.cs", _three);
            fixture.Write("Methods.cs", _methods);
            var commands = fixture.Commands;
            var helper = LineOf(_methods, "Helper");
            var keyDown = LineOf(_methods, "OnKeyDown");

            var original = Review(fixture, "Three.cs");

            Assert.Equal(ExitCode.Blocked, original.ExitCode);
            Assert.Contains($"Three.cs({LineOf(_three, "value > 0")},9): error TEAM010", original.Output, StringComparison.Ordinal);
            Assert.DoesNotContain($"Three.cs({LineOf(_three, "value < 0")},", original.Output, StringComparison.Ordinal);

            // a new rule: its filter and requirement are shown back, and the next review applies it to the method that breaks it
            Assert.Equal(ExitCode.Passed, commands.Run("rules", "add", "TEAM016", "--for", "method", "--title", "Public methods only", "--where", "isOverride=false", "--require", "writtenAccess=public", "--why", "Keep the surface small").ExitCode);
            var shown = commands.Run("rules", "show", "TEAM016").Output;
            Assert.Contains("isOverride", shown, StringComparison.Ordinal);
            Assert.Contains("writtenAccess", shown, StringComparison.Ordinal);
            Assert.Contains("Keep the surface small", shown, StringComparison.Ordinal);
            var added = Review(fixture, "Methods.cs");
            Assert.Equal(ExitCode.Blocked, added.ExitCode);
            Assert.Contains($"Methods.cs({helper},", added.Output, StringComparison.Ordinal);
            Assert.DoesNotContain($"Methods.cs({keyDown},", added.Output, StringComparison.Ordinal);

            // the filter changes and the finding moves to the other method
            Assert.Equal(ExitCode.Passed, commands.Run("rules", "set", "TEAM016", "--where", "isOverride=true").ExitCode);
            var moved = Review(fixture, "Methods.cs");
            Assert.Contains($"Methods.cs({keyDown},", moved.Output, StringComparison.Ordinal);
            Assert.DoesNotContain($"Methods.cs({helper},", moved.Output, StringComparison.Ordinal);

            // a warning does not stop the push
            Assert.Equal(ExitCode.Passed, commands.Run("rules", "severity", "TEAM016", "warn").ExitCode);
            var warned = Review(fixture, "Methods.cs");
            Assert.Equal(ExitCode.Passed, warned.ExitCode);
            Assert.Contains("warning TEAM016", warned.Output, StringComparison.Ordinal);

            // off: nothing is found, the listings say it is off, and a rule that needs a build still leaves the review incomplete
            Assert.Equal(ExitCode.Passed, commands.Run("rules", "disable", "TEAM016").ExitCode);
            Assert.Equal(ExitCode.Passed, commands.Run("rules", "add", "TEAM017", "--for", "usage", "--title", "No clock", "--where", "target=System.DateTime.Now", "--forbid").ExitCode);
            Assert.Contains(commands.Run("rules", "list").Output.Split('\n'), line => line.Contains("TEAM016", StringComparison.Ordinal) && line.Contains("(off)", StringComparison.Ordinal));
            Assert.Contains("off", commands.Run("rules", "show", "TEAM016").Output, StringComparison.Ordinal);
            var disabled = Review(fixture, "Methods.cs");
            Assert.Equal(ExitCode.Incomplete, disabled.ExitCode);
            Assert.DoesNotContain("TEAM016", disabled.Output, StringComparison.Ordinal);
            Assert.Contains("not run: TEAM017", disabled.Output, StringComparison.Ordinal);

            Assert.Equal(ExitCode.Passed, commands.Run("rules", "enable", "TEAM016").ExitCode);
            Assert.Contains("warning TEAM016", Review(fixture, "Methods.cs").Output, StringComparison.Ordinal);

            // removed, the rules are the original ones again and a review applies only them
            Assert.Equal(ExitCode.Passed, commands.Run("rules", "remove", "TEAM016").ExitCode);
            Assert.Equal(ExitCode.Passed, commands.Run("rules", "remove", "TEAM017").ExitCode);
            Assert.Equal(new[] { "TEAM010" }, fixture.RuleIds());
            var removed = Review(fixture, "Methods.cs");
            Assert.Equal(ExitCode.Passed, removed.ExitCode);
            Assert.DoesNotContain("TEAM016", removed.Output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EditingPreservesUnrelatedSettingsAndNeighbourFiles()
    {
        using(var fixture = new RulesFixture(_untidySettings))
        {
            fixture.Write("commitfence.json.new", "KEEP");
            var before = fixture.Settings();

            var result = fixture.Commands.Run("rules", "severity", "TEAM011", "warn");

            // everything but the one value is what it was: the requirement, the other rule, the modules and both lists of directions
            Assert.Equal(ExitCode.Passed, result.ExitCode);
            var expected = new RuleSettings(before.Requires, before.Rules.Select(rule => rule.Id == "TEAM011" ? rule.WithSeverity(Severity.Warn) : rule).ToList(), before.Structure);
            Assert.Equal(new SettingsWriter("\n").Write(expected), new SettingsWriter("\n").Write(fixture.Settings()));
            Assert.Equal("Tidy > ünïcode", fixture.Settings().FindRule("TEAM011")!.Title);

            // the file keeps its line ends, and nothing was left or touched beside it
            var text = fixture.SettingsText();
            Assert.Contains("\r\n", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.Equal("KEEP", File.ReadAllText(Path.Combine(fixture.Folder, "commitfence.json.new"), Encoding.UTF8));
            Assert.Equal(new[] { "commitfence.json", "commitfence.json.new" }, fixture.FilesBesideSettings());
        }
    }

    [Fact]
    public async Task ConcurrentEditorsRetainBothSuccessfulChanges()
    {
        using(var fixture = RulesFixture.Canonical(RulesFixture.BracedIfRule, RulesFixture.MethodRule))
        {
            // two programs with their own services, one data folder and the product's own lock: the first holds the lock when the second asks
            var realLock = new FileSettingsLock(fixture.Commands.DataFolder);
            var first = new GatedLock(realLock, true);
            var second = new GatedLock(realLock, false);
            var firstCommands = new CommandHarness(fixture.Folder, null, first);
            var secondCommands = new CommandHarness(fixture.Folder, null, second);

            var disabling = Task.Run(() => firstCommands.Run("rules", "disable", "TEAM010"), TestContext.Current.CancellationToken);
            Assert.True(first.Held.Wait(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken));
            var warning = Task.Run(() => secondCommands.Run("rules", "severity", "TEAM011", "warn"), TestContext.Current.CancellationToken);
            Assert.True(second.Requested.Wait(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken));
            first.Release.Set();

            Assert.Equal(ExitCode.Passed, (await disabling).ExitCode);
            Assert.Equal(ExitCode.Passed, (await warning).ExitCode);
            var settings = fixture.Settings();
            Assert.False(settings.FindRule("TEAM010")!.Enabled);
            Assert.Equal(Severity.Warn, settings.FindRule("TEAM011")!.Severity);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidEdits))]
    public void InvalidEditLeavesSettingsIntact(string command, string mustSay)
    {
        using(var fixture = RulesFixture.Canonical(RulesFixture.BracedIfRule, RulesFixture.MethodRule))
        {
            var before = fixture.SettingsBytes();

            var result = fixture.Commands.Run(command.Split('|'));

            Assert.Equal(ExitCode.BadSettingsOrUsage, result.ExitCode);
            Assert.Equal(string.Empty, result.Output);
            Assert.Contains(mustSay, result.Error, StringComparison.Ordinal);
            Assert.Equal(before, fixture.SettingsBytes());
            Assert.Equal(new[] { "commitfence.json" }, fixture.FilesBesideSettings());
        }
    }

    [Fact]
    public void InsertAndCopyPreserveRuleOrderAndContents()
    {
        var legacy = RulesFixture.BracedIfRule.Replace("TEAM010", "odd id", StringComparison.Ordinal);
        using(var fixture = RulesFixture.Canonical(legacy, RulesFixture.MethodRule))
        {
            var commands = fixture.Commands;

            Assert.Equal(ExitCode.Passed, commands.Run("rules", "add", "TEAM020", "--for", "if-statement", "--title", "Inserted", "--require", "braces=true", "--before", "TEAM011").ExitCode);
            Assert.Equal(ExitCode.Passed, commands.Run("rules", "disable", "TEAM011").ExitCode);
            Assert.Equal(ExitCode.Passed, commands.Run("rules", "copy", "TEAM011", "TEAM014").ExitCode);

            // the id with a space is still listed, the new rule sits where it was asked, the copy right after its original
            Assert.Equal(new[] { "odd id", "TEAM020", "TEAM011", "TEAM014" }, fixture.RuleIds());
            var original = fixture.Settings().FindRule("TEAM011")!;
            var copy = fixture.Settings().FindRule("TEAM014")!;
            Assert.Equal("Copy of " + original.Title, copy.Title);
            Assert.False(copy.Enabled);
            Assert.Equal(original.ArtifactType, copy.ArtifactType);
            Assert.Equal(original.Severity, copy.Severity);
            Assert.Equal(Describe(original.Where), Describe(copy.Where));
            Assert.Equal(Describe(original.Require), Describe(copy.Require));
            Assert.Equal(ExitCode.Passed, commands.Run("rules", "list").ExitCode);
        }
    }
}
