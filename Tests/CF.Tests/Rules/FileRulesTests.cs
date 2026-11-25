using CF.Adapters.Machine;
using CF.Core.Code;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Languages.CSharp.EditorConfig;
using CF.Languages.CSharp.Toolchain;
using CF.Tests.Rules.Support;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Rules;

/// <summary>
/// The rules that the repository's .editorconfig sets for the compiler, as the commands list, show and refuse to change them.
/// </summary>
public sealed class FileRulesTests
{
    private const string _editorConfig = """
        root = true

        [*.vb]
        dotnet_diagnostic.VB0001.severity = error

        [*.{cs,vb}]
        dotnet_diagnostic.CA1000.severity = warning
        dotnet_diagnostic.IDE0005.severity = suggestion
        dotnet_diagnostic.ZZ1002.severity = none # trailing title
        # comment above title
        dotnet_diagnostic.ZZ1003.severity = silent
        # the comment above loses to the trailing one
        dotnet_diagnostic.ZZ1004.severity = warning ; trailing wins
        DOTNET_DIAGNOSTIC.ca1050.SEVERITY=Error
        dotnet_diagnostic.CA1052.severity: none
        dotnet_diagnostic.ZZ1007.severity = warning
        dotnet_diagnostic.ZZ1007.severity = error

        [*]
        dotnet_diagnostic.CA1000.severity = error

        [src/**.cs]
        dotnet_diagnostic.ZZ1005.severity = error

        [tests/**.cs]
        dotnet_diagnostic.ZZ1006.severity = error

        """;

    private static readonly string _settings = "{ \"rules\": [ " + RulesFixture.BracedIfRule + " ] }";

    public static TheoryData<bool> Sdks { get; } = new TheoryData<bool> { true, false };

    public static TheoryData<bool, string> Configs { get; } = new TheoryData<bool, string>
    {
        new TheoryDataRow<bool, string>(false, "no .editorconfig") { TestDisplayName = "there is no .editorconfig" },
        new TheoryDataRow<bool, string>(true, ".editorconfig") { TestDisplayName = "the .editorconfig cannot be read" }
    };

    private static RulesFixture CreateFixture(FakeSdk sdk, bool isSdkAvailable)
    {
        var fixture = new RulesFixture(_settings, sdk.Runner(isSdkAvailable));
        fixture.Write(".editorconfig", _editorConfig);
        fixture.Write("src/x.cs", "class X { }\n");
        fixture.Write("sub/.editorconfig", "root = false\n");

        return fixture;
    }

    private static int LineOf(string marker, int occurrence = 1)
    {
        var lines = _editorConfig.Split('\n');
        return Enumerable.Range(0, lines.Length).Where(index => lines[index].Contains(marker, StringComparison.Ordinal)).Skip(occurrence - 1).First() + 1;
    }

    private static string ListLine(string output, string id)
    {
        return Assert.Single(output.Split('\n'), line => line.StartsWith(id + " ", StringComparison.Ordinal));
    }

    private static void AssertLine(string output, string id, params string[] parts)
    {
        var line = ListLine(output, id);
        foreach(var part in parts)
            Assert.Contains(part, line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("*.cs:error,tests/*.cs:warning", 2, "4")]
    [InlineData("*.cs:error,tests/*.cs:warning,*.g.cs:none,*.old.cs:none", 2, "4,6,8")]
    [InlineData("*.cs:none,tests/*.cs:error", 2, "4")]
    [InlineData("tests/*.cs:suggestion", null, "2")]
    [InlineData("*.cs:error,tests/*.cs:error", 2, "")]
    public void ScopedSettingsKeepTheMainAndApplicableExceptions(string settings, int? main, string exceptions)
    {
        using var fixture = new RulesFixture(_settings);
        var rule = ScopedRule(fixture, settings);
        Assert.Equal(main, rule.Line);
        Assert.Equal(exceptions, string.Join(",", rule.Exceptions.Select(place => place.Line)));
    }

    [Fact]
    public void LastMatchingPlaceDecidesThePath()
    {
        using var fixture = new RulesFixture(_settings);
        var rule = ScopedRule(fixture, "*.cs:error,tests/*.cs:warning,*.g.cs:error");
        Assert.Equal(2, rule.ForPath("src/C.cs")?.Line);
        Assert.Equal(4, rule.ForPath("tests/C.cs")?.Line);
        Assert.Equal(6, rule.ForPath("tests/C.g.cs")?.Line);
    }

    [Fact]
    public void CommandsDescribeEachPlaceInTheFile()
    {
        using var sdk = new FakeSdk();
        using var fixture = new RulesFixture(_settings, sdk.Runner(false));
        ScopedRule(fixture, "*.cs:error,tests/*.cs:warning,*.g.cs:none");
        var list = fixture.Commands.Run("rules", "list");
        var detail = fixture.Commands.Run("rules", "show", "ZZ1000");
        Assert.Equal(ExitCode.Passed, list.ExitCode);
        Assert.Equal(ExitCode.Passed, detail.ExitCode);
        Assert.Equal(new List<string>
        {
            ".editorconfig: 1 rules", "ZZ1000  on              blocks  line 2",
            "        warns for tests/*.cs, line 4", "        off for *.g.cs, line 6"
        }, list.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).TakeLast(4));
        Assert.Equal(new List<string>
        {
            "ZZ1000", "  setting:  'error'", "  severity: block",
            "  status:   blocks for all C# files, 2 exceptions, applied by the build",
            "  file:     .editorconfig, line 2",
            "  source:   set in .editorconfig; the product does not change that file",
            "  lines:    3 lines set this rule; a later line overrides an earlier one",
            "            blocks for all C# files, line 2 ('error')",
            "            warns for tests/*.cs, line 4 ('warning')",
            "            off for *.g.cs, line 6 ('none')"
        }, detail.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void ListMapsTheMainSettingAndEachException()
    {
        using var fixture = new RulesFixture("""{"strictness":"strict","rules":[]}""");
        ScopedRule(fixture, "*.cs:error,tests/*.cs:warning,*.g.cs:none");

        var result = fixture.Commands.Run("rules", "list");

        Assert.Equal(ExitCode.Passed, result.ExitCode);
        AssertLine(result.Output, "ZZ1000", "on", "blocks", "line 2");
        Assert.Contains("blocks for tests/*.cs, line 4", result.Output, StringComparison.Ordinal);
        Assert.Contains("off for *.g.cs, line 6", result.Output, StringComparison.Ordinal);
    }

    private static FileRule ScopedRule(RulesFixture fixture, string settings)
    {
        fixture.Write(".editorconfig", string.Join("\n", settings.Split(',').Select(pair => pair.Split(':')).Select(parts => $"[{parts[0]}]\ndotnet_diagnostic.ZZ1000.severity = {parts[1]}")));
        fixture.Write("tests/C.g.cs", string.Empty);
        fixture.Write("tests/C.old.cs", string.Empty);
        var reader = new EditorConfigRules(new SystemFileSystem(), new DotnetTool(new FakeDotnet(TestPaths.Temp)));
        return Assert.Single(reader.Read(fixture.Folder).Rules);
    }

    [Theory]
    [MemberData(nameof(Sdks))]
    public void ListingUsesTheLastApplicableSettingAndBestAvailableTitle(bool isSdkAvailable)
    {
        using(var sdk = new FakeSdk())
        using(var fixture = CreateFixture(sdk, isSdkAvailable))
        {
            var result = fixture.Commands.Run("rules", "list");

            Assert.Equal(ExitCode.Passed, result.ExitCode);

            // the team's rule first, then the file's rules, in the order of their ids as plain character codes
            var lines = result.Output.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            Assert.StartsWith("TEAM010", lines[0], StringComparison.Ordinal);
            Assert.Contains(".editorconfig: 9 rules", lines);
            var ids = lines.Where(line => line.Length > 0 && char.IsLetter(line[0]) && !line.StartsWith("TEAM010", StringComparison.Ordinal)).Select(line => line.Split(' ')[0]).ToList();
            Assert.Equal(new[] { "CA1000", "CA1052", "IDE0005", "ZZ1002", "ZZ1003", "ZZ1004", "ZZ1005", "ZZ1007", "ca1050" }, ids);

            // a section that matches no file, or no file of the repository, lists nothing; the later of two sections and of two lines wins, with its own line
            Assert.DoesNotContain("VB0001", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("ZZ1006", result.Output, StringComparison.Ordinal);
            AssertLine(result.Output, "CA1000", "on", "blocks", $"line {LineOf("CA1000", 2)}");
            AssertLine(result.Output, "ZZ1007", "on", "blocks", $"line {LineOf("ZZ1007", 2)}");
            AssertLine(result.Output, "ZZ1005", "not set");
            AssertLine(result.Output, "ca1050", "blocks");
            AssertLine(result.Output, "CA1052", "off");
            AssertLine(result.Output, "IDE0005", "warns");

            // the title: the SDK's when there is one, else the comment after the setting, else the comment above it
            AssertLine(result.Output, "ZZ1002", "trailing title");
            AssertLine(result.Output, "ZZ1003", "comment above title");
            AssertLine(result.Output, "ZZ1004", "trailing wins");
            Assert.Equal(isSdkAvailable, ListLine(result.Output, "CA1000").Contains("Do not declare static members on generic types", StringComparison.Ordinal));
            Assert.Equal(isSdkAvailable, ListLine(result.Output, "IDE0005").Contains("Remove unnecessary using directives", StringComparison.Ordinal));
            Assert.DoesNotContain("older title", result.Output, StringComparison.Ordinal);

            // a file of the same name in a sub-folder is not read, and the listing says so
            Assert.Contains("1 .editorconfig file in sub-folders is not read.", lines);
        }
    }

    [Fact]
    public void SeverityVocabularyDescribesTheEffect()
    {
        var described = new[] { "error", "Warning", "suggestion", "none", "silent", "default", "bogus", string.Empty }.Select(word =>
        {
            var effect = Strictness.Standard.EffectOf(word);
            return $"{word}:{FileRulePlace.StateOf(word)}:{effect}";
        }).ToList();

        Assert.Equal(
            new[] { "error:On:Block", "Warning:On:Warn", "suggestion:On:Warn", "none:Off:", "silent:Off:", "default:OwnDefault:", "bogus:NotUnderstood:", ":NotUnderstood:" },
            described);
    }

    [Theory]
    [MemberData(nameof(Configs))]
    public void ListDistinguishesMissingFromUnreadableConfig(bool isDenied, string expected)
    {
        using(var fixture = new RulesFixture(_settings, null, null, isDenied ? new DeniedFileSystem(new SystemFileSystem(), ".editorconfig") : null))
        {
            if(isDenied)
                fixture.Write(".editorconfig", "[*.cs]\ndotnet_diagnostic.CA1000.severity = error\n");

            var result = fixture.Commands.Run("rules", "list");

            if(isDenied)
            {
                Assert.Equal(ExitCode.BadSettingsOrUsage, result.ExitCode);
                Assert.Equal(string.Empty, result.Output);
                Assert.Contains("cannot be read", result.Error, StringComparison.Ordinal);
                Assert.Contains(expected, result.Error, StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal(ExitCode.Passed, result.ExitCode);
                Assert.Contains("TEAM010", result.Output, StringComparison.Ordinal);
                Assert.Contains(expected, result.Output, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void CompilerRuleIsDiscoverableAndReadOnly()
    {
        using(var sdk = new FakeSdk())
        using(var fixture = CreateFixture(sdk, true))
        {
            var settingsBefore = fixture.SettingsBytes();
            var editorConfigBefore = File.ReadAllBytes(Path.Combine(fixture.Folder, ".editorconfig"));
            var winningLine = LineOf("CA1000", 2);

            // found by its id in any case, with the file and the line that decide it and the title the SDK gives it
            var shown = fixture.Commands.Run("rules", "show", "ca1000");

            Assert.Equal(ExitCode.Passed, shown.ExitCode);
            Assert.Contains("Do not declare static members on generic types", shown.Output, StringComparison.Ordinal);
            Assert.Contains($".editorconfig, line {winningLine}", shown.Output, StringComparison.Ordinal);
            Assert.Contains("'error'", shown.Output, StringComparison.Ordinal);

            // every way to change it is refused where the setting is, and nothing is written
            var attempts = new[]
            {
                new[] { "rules", "disable", "CA1000" },
                new[] { "rules", "severity", "CA1000", "block" },
                new[] { "rules", "set", "CA1000", "--title", "x" },
                new[] { "rules", "remove", "CA1000" },
                new[] { "rules", "copy", "CA1000", "TEAM099" },
                new[] { "rules", "try", "CA1000" }
            };
            foreach(var attempt in attempts)
            {
                var refused = fixture.Commands.Run(attempt);

                Assert.Equal(ExitCode.BadSettingsOrUsage, refused.ExitCode);
                Assert.Equal(string.Empty, refused.Output);
                Assert.Contains($".editorconfig, line {winningLine}", refused.Error, StringComparison.Ordinal);
            }

            Assert.Equal(settingsBefore, fixture.SettingsBytes());
            Assert.Equal(editorConfigBefore, File.ReadAllBytes(Path.Combine(fixture.Folder, ".editorconfig")));
        }
    }
}